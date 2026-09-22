using Content.Client.Overlays;
using Content.Shared.GameTicking;
using Content.Shared.NightVision;
using Content.Shared.Overlays;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.NightVision;

/// <inheritdoc/>
public sealed partial class NightVisionSystem : SharedNightVisionSystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    private NightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new NightVisionOverlay();
        InitializeOrbitraNightVision(); // Orbitra-Edit - отдельное оформление приборов.
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        HideOrbitraNightVision(); // Orbitra-Edit - не переносим прогрев на другого наблюдателя.
        RefreshOverlay(args.Entity);
    }

    [SubscribeLocalEvent]
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        Deactivate(_player.LocalEntity);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Orbitra edit start - обновление прибора приходит на предмет, а не на владельца.
        if (_player.LocalEntity is { } viewer)
            RefreshOverlay(viewer);
        // Orbitra edit end
    }

    [SubscribeLocalEvent]
    private void OnCompEquip(Entity<NightVisionComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Target);
    }

    [SubscribeLocalEvent]
    private void OnCompEquip(Entity<NightVisionComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Target);
    }

    [SubscribeNetworkEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        HideOrbitraNightVision(); // Orbitra-Edit - сброс даже без прикреплённой сущности.
        var localPlayer = _player.LocalSession?.AttachedEntity;
        if (localPlayer != null)
            Deactivate(localPlayer.Value);
    }

    private void Update(EntityUid entity, List<Entity<NightVisionComponent>> entities)
    {
        if (entity != _player.LocalSession?.AttachedEntity)
            return;

        // Orbitra edit start - прежний выбор выделен для тестирования; сохраняем UID источника.
        var selected = Content.Client._Orbitra.NightVision.OrbitraNightVisionPresentation.SelectSource(entity, entities);
        var nvision = selected?.Comp;
        // Orbitra edit end

        // There is no active night vision components, so we disable the overlay.
        if (nvision == null)
        {
            Deactivate(entity);
            return;
        }

        // Orbitra added start - новый эффект только у выбранного прибора с маркером.
        if (TryShowOrbitraNightVision(entity, selected!.Value))
            return;
        // Orbitra added end

        _overlay.SetParameters(nvision.OverlayColor, nvision.LightingColor, nvision.NoiseAmount, nvision.NoiseMultiplier);

        if (!_overlayMan.HasOverlay<NightVisionOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void Deactivate(EntityUid? ent)
    {
        if (ent != _player.LocalSession?.AttachedEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
        HideOrbitraNightVision(); // Orbitra-Edit - выключаем оба прохода прибора без задержки.
    }

    protected override void RefreshOverlay(EntityUid target)
    {
        if (target != _player.LocalSession?.AttachedEntity)
            return;

        var ev = new RefreshNightVisionEvent();
        RaiseLocalEvent(target, ref ev);

        if (ev.Entities.Count > 0)
            Update(target, ev.Entities);
        else
            Deactivate(target);
    }
}
