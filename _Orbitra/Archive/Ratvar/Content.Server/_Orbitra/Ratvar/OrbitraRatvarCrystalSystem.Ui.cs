using System.Linq;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Humanoid;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarCrystalSystem
{
    // Только локально доступный кристалл своего культа раскрывает список точек.
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextUiUpdate;

    private void InitializeUi()
    {
        SubscribeLocalEvent<OrbitraRatvarCrystalComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        Subs.BuiEvents<OrbitraRatvarCrystalComponent>(OrbitraRatvarCrystalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<OrbitraRatvarCrystalProjectMessage>(OnProjectMessage);
            subs.Event<OrbitraRatvarCrystalRenameMessage>(OnRenameMessage);
        });
    }

    private void OnUiOpenAttempt(Entity<OrbitraRatvarCrystalComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanAccessCrystal(ent, args.User, out _)) args.Cancel();
    }

    private void OnUiOpened(Entity<OrbitraRatvarCrystalComponent> ent, ref BoundUIOpenedEvent args) => UpdateInterface(ent, args.Actor);

    private void OnProjectMessage(Entity<OrbitraRatvarCrystalComponent> ent, ref OrbitraRatvarCrystalProjectMessage args)
    {
        if (CanAccessCrystal(ent, args.Actor, out _) && TryGetEntity(args.Destination, out var target) && target is { } destination)
            TryProject(ent, args.Actor, destination);
        UpdateInterface(ent, args.Actor);
    }

    private void OnRenameMessage(Entity<OrbitraRatvarCrystalComponent> ent, ref OrbitraRatvarCrystalRenameMessage args)
    {
        TryRenameCrystal(ent, args.Actor, args.Name);
        UpdateInterface(ent, args.Actor);
    }

    /// <summary>Renames only a locally accessible owned crystal; markup is never interpreted.</summary>
    public bool TryRenameCrystal(Entity<OrbitraRatvarCrystalComponent> ent, EntityUid user, string name)
    {
        if (!CanAccessCrystal(ent, user, out _) || string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
            return false;
        ent.Comp.Label = name.Trim();
        return true;
    }

    /// <summary>Builds a private snapshot without expanding PVS or resolving remote inventories.</summary>
    public OrbitraRatvarCrystalUiState? BuildCrystalState(Entity<OrbitraRatvarCrystalComponent> source, EntityUid user)
    {
        if (!CanAccessCrystal(source, user, out var cult)) return null;
        var points = new List<OrbitraRatvarCrystalDestination>();
        foreach (var uid in _crystals)
        {
            if (uid == source.Owner || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !TryComp<OrbitraRatvarStructureComponent>(uid, out var structure) || structure.Rule != cult.Owner ||
                !TryComp<OrbitraRatvarCrystalComponent>(uid, out var crystal)) continue;
            var reason = !ValidCrystal(uid, cult) ? "orbitra-ratvar-crystal-point-invalid" :
                crystal.Projection != null ? "orbitra-ratvar-crystal-point-busy" : "orbitra-ratvar-crystal-point-ready";
            var position = _transform.GetGridOrMapTilePosition(uid);
            points.Add(new OrbitraRatvarCrystalDestination(GetNetEntity(uid),
                string.IsNullOrWhiteSpace(crystal.Label) ? Name(uid) : crystal.Label,
                position.X, position.Y, reason));
        }
        return new OrbitraRatvarCrystalUiState(source.Comp.Label, points.ToArray());
    }

    private bool CanAccessCrystal(EntityUid source, EntityUid user, out Entity<OrbitraRatvarRuleComponent> cult)
    {
        cult = default;
        return Living(user) && !HasComp<ActiveOrbitraRatvarProjectionComponent>(user) &&
            TryComp<HumanoidProfileComponent>(user, out var profile) && profile.Species == "Human" &&
            _mind.TryGetMind(user, out _, out var mind) && mind.OwnedEntity == user && mind.VisitingEntity == null &&
            !_container.IsEntityInContainer(user) && _rule.TryGetCult(user, out cult) && ValidCrystal(source, cult) &&
            _actionBlocker.CanInteract(user, source) && _interaction.InRangeUnobstructed(user, source);
    }

    private void UpdateInterface(Entity<OrbitraRatvarCrystalComponent> source, EntityUid user)
    {
        var state = BuildCrystalState(source, user);
        if (state == null)
        {
            _ui.CloseUi(source.Owner, OrbitraRatvarCrystalUiKey.Key, user);
            return;
        }
        if (_ui.TryGetUiState<OrbitraRatvarCrystalUiState>(source.Owner, OrbitraRatvarCrystalUiKey.Key, out var previous) &&
            SameState(previous, state)) return;
        _ui.SetUiState(source.Owner, OrbitraRatvarCrystalUiKey.Key, state);
    }

    private void UpdateInterfaces()
    {
        if (_timing.CurTime < _nextUiUpdate) return;
        _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var uid in _crystals)
        {
            if (!TryComp<OrbitraRatvarCrystalComponent>(uid, out var crystal) ||
                !_ui.IsUiOpen(uid, OrbitraRatvarCrystalUiKey.Key)) continue;
            // Закрытие интерфейса меняет список подписчиков во время обхода.
            foreach (var actor in _ui.GetActors(uid, OrbitraRatvarCrystalUiKey.Key).ToArray())
                UpdateInterface((uid, crystal), actor);
        }
    }

    private static bool SameState(OrbitraRatvarCrystalUiState left, OrbitraRatvarCrystalUiState right)
    {
        if (left.Name != right.Name || left.Destinations.Length != right.Destinations.Length) return false;
        for (var i = 0; i < left.Destinations.Length; i++)
            if (left.Destinations[i] != right.Destinations[i]) return false;
        return true;
    }
}
