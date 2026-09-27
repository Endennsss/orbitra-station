using Content.Server._Orbitra.ThermalVision;
using Content.Shared._Orbitra.ThermalVision;
using Content.Shared.Actions;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Revokes borrowed effects on unequip, body loss, deletion and cult membership loss.</summary>
public sealed partial class OrbitraRatvarEquipmentSystem : EntitySystem
{
    [Dependency] private OrbitraRatvarRuleSystem _cult = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private OrbitraThermalVisionSystem _thermal = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRatvarEquipmentComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<OrbitraRatvarEquipmentComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<OrbitraRatvarEquipmentComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<OrbitraRatvarEquipmentComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraRatvarEquipmentComponent, OrbitraThermalVisionAttemptEvent>(OnThermalAttempt);
        SubscribeLocalEvent<ActiveOrbitraRatvarSpectaclesComponent, GetVisMaskEvent>(OnVision);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
    }

    private void OnEquipAttempt(Entity<OrbitraRatvarEquipmentComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Slot == ent.Comp.Slot && !_cult.TryGetCult(args.EquipTarget, out _))
        {
            args.Cancel();
            args.Reason = "orbitra-ratvar-cache-denied";
        }
    }
    private void OnEquipped(Entity<OrbitraRatvarEquipmentComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot == ent.Comp.Slot) ent.Comp.Wearer = args.EquipTarget;
    }
    private void OnUnequipped(Entity<OrbitraRatvarEquipmentComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != ent.Comp.Slot) return;
        Stop(ent);
        ent.Comp.Wearer = null;
    }
    private void OnShutdown(Entity<OrbitraRatvarEquipmentComponent> ent, ref ComponentShutdown args) => Stop(ent);
    private void OnDetached(PlayerDetachedEvent args) => StopForBody(args.Entity);
    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (args.Mind.OwnedEntity is { } body) StopForBody(body);
    }
    private void OnThermalAttempt(Entity<OrbitraRatvarEquipmentComponent> ent, ref OrbitraThermalVisionAttemptEvent args)
    {
        if (!CanWear(ent, args.User)) args.Cancelled = true;
    }
    private void OnVision(Entity<ActiveOrbitraRatvarSpectaclesComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running &&
            TryComp<OrbitraRatvarEquipmentComponent>(ent.Comp.Source, out var gear) &&
            CanWear((ent.Comp.Source, gear), ent) && gear.Active)
            args.VisibilityMask |= (int) VisibilityFlags.Ghost;
    }

    /// <summary>Authority follows the current controlled body, never a stale equipment marker.</summary>
    public bool CanWear(Entity<OrbitraRatvarEquipmentComponent> ent, EntityUid user) =>
        !TerminatingOrDeleted(ent) && !EntityManager.IsQueuedForDeletion(ent) &&
        ent.Comp.Wearer == user && _inventory.TryGetSlotEntity(user, ent.Comp.Slot, out var item) && item == ent.Owner &&
        _cult.TryGetCult(user, out _) && TryComp<MobStateComponent>(user, out var mob) && mob.CurrentState != MobState.Dead &&
        TryComp<ActorComponent>(user, out var actor) && actor.PlayerSession.AttachedEntity == user;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        var query = EntityQueryEnumerator<OrbitraRatvarEquipmentComponent>();
        while (query.MoveNext(out var uid, out var gear))
        {
            if (gear.Wearer is not { } wearer) continue;
            if (!CanWear((uid, gear), wearer))
            {
                Stop((uid, gear));
                if (!_cult.TryGetCult(wearer, out _)) _inventory.TryUnequip(wearer, gear.Slot, silent: true, force: true);
                continue;
            }
            if (!gear.Active) Start((uid, gear), wearer);
            if (gear.Cloak && gear.OwnsStealth && HasComp<StealthComponent>(wearer))
            {
                var fraction = Math.Clamp((_timing.CurTime - gear.EquippedAt).TotalSeconds /
                    Math.Max(0.01, gear.FadeTime.TotalSeconds), 0, 1);
                var target = (float) (1 + (gear.Visibility - 1) * fraction);
                if (Math.Abs(_stealth.GetVisibility(wearer) - target) > 0.001f) _stealth.SetVisibility(wearer, target);
            }
            if (gear.Spectacles && TryComp<BlindableComponent>(wearer, out var eyes))
            {
                var strain = EnsureComp<OrbitraRatvarEyeStrainComponent>(wearer);
                if (strain.NextDamage <= _timing.CurTime)
                {
                    strain.NextDamage = _timing.CurTime + gear.EyeDamageInterval;
                    if (strain.Applied < gear.EyeDamageCap && eyes.EyeDamage < gear.EyeDamageCap)
                    {
                        _blindable.AdjustEyeDamage((wearer, eyes), 1);
                        strain.Applied++;
                    }
                }
            }
        }
        var recovery = EntityQueryEnumerator<OrbitraRatvarEyeStrainComponent>();
        while (recovery.MoveNext(out var uid, out var strain))
        {
            if (strain.RecoverAt is not { } deadline || deadline > _timing.CurTime) continue;
            if (TryComp<BlindableComponent>(uid, out var eyes))
                _blindable.AdjustEyeDamage((uid, eyes), -Math.Min(strain.Applied, eyes.EyeDamage));
            RemCompDeferred<OrbitraRatvarEyeStrainComponent>(uid);
        }
    }

    private void Start(Entity<OrbitraRatvarEquipmentComponent> ent, EntityUid wearer)
    {
        ent.Comp.Active = true;
        ent.Comp.EquippedAt = _timing.CurTime;
        if (ent.Comp.Cloak && !HasComp<StealthComponent>(wearer))
        {
            AddComp<StealthComponent>(wearer);
            ent.Comp.OwnsStealth = true;
            _stealth.SetVisibility(wearer, 1);
        }
        if (!ent.Comp.Spectacles) return;
        EnsureComp<ActiveOrbitraRatvarSpectaclesComponent>(wearer).Source = ent.Owner;
        _eye.RefreshVisibilityMask(wearer);
        var strain = EnsureComp<OrbitraRatvarEyeStrainComponent>(wearer);
        if (strain.NextDamage == TimeSpan.Zero) strain.NextDamage = _timing.CurTime + ent.Comp.EyeDamageInterval;
        strain.RecoverAt = null;
        if (TryComp<OrbitraThermalVisionComponent>(ent, out var thermal))
        {
            if (!thermal.Enabled) _thermal.TryToggle((ent, thermal), wearer);
            _actions.RemoveAction(wearer, thermal.ActionEntity);
        }
    }

    private void StopForBody(EntityUid body)
    {
        var query = EntityQueryEnumerator<OrbitraRatvarEquipmentComponent>();
        while (query.MoveNext(out var uid, out var gear))
            if (gear.Wearer == body) Stop((uid, gear));
    }

    private void Stop(Entity<OrbitraRatvarEquipmentComponent> ent)
    {
        if (!ent.Comp.Active || ent.Comp.Wearer is not { } wearer) return;
        ent.Comp.Active = false;
        if (ent.Comp.OwnsStealth && !TerminatingOrDeleted(wearer)) RemComp<StealthComponent>(wearer);
        ent.Comp.OwnsStealth = false;
        if (ent.Comp.Spectacles && !TerminatingOrDeleted(wearer))
        {
            RemComp<ActiveOrbitraRatvarSpectaclesComponent>(wearer);
            _eye.RefreshVisibilityMask(wearer);
            if (TryComp<OrbitraRatvarEyeStrainComponent>(wearer, out var strain))
                strain.RecoverAt = _timing.CurTime + ent.Comp.RecoveryDelay;
            if (TryComp<OrbitraThermalVisionComponent>(ent, out var thermal)) _thermal.Disable((ent, thermal));
        }
    }
}
