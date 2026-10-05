using Content.Shared._Orbitra.NightVision;
using Content.Shared.NightVision;
using Content.Shared.Overlays;
using Content.Shared.PowerCell;
using Content.Server.NightVision;

namespace Content.Server._Orbitra.NightVision;

/// <summary>Связывает ночное зрение ПНВ с зарядом установленной батарейки.</summary>
public sealed partial class OrbitraNightVisionPowerSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private NightVisionSystem _nightVision = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleNightVisionEvent>(OnToggle, after: [typeof(SharedNightVisionSystem)]);
        SubscribeLocalEvent<OrbitraNightVisionDeviceComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
    }

    private void OnToggle(ToggleNightVisionEvent args)
    {
        if (args.Action.Comp.Container is not { } uid ||
            !TryComp<OrbitraNightVisionDeviceComponent>(uid, out _) ||
            !TryComp<NightVisionComponent>(uid, out var nightVision))
            return;

        if (nightVision.Enabled && !_powerCell.HasDrawCharge(uid, user: args.Performer))
        {
            _nightVision.SetEnabled((uid, nightVision), false, args.Performer);
            return;
        }

        _powerCell.SetDrawEnabled(uid, nightVision.Enabled);
    }

    private void OnPowerCellEmpty(Entity<OrbitraNightVisionDeviceComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (!TryComp<NightVisionComponent>(ent.Owner, out var nightVision) || !nightVision.Enabled)
            return;

        _nightVision.SetEnabled((ent.Owner, nightVision), false);
        _powerCell.SetDrawEnabled(ent.Owner, false);
    }
}
