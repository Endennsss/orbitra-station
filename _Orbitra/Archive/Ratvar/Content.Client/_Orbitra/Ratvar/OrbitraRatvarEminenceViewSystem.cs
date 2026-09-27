using System.Numerics;
using Content.Client.Movement.Systems;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Maths;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Matches the selected body's camera framing without changing the controlled entity.</summary>
public sealed partial class OrbitraRatvarEminenceViewSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(ContentEyeSystem));
        UpdatesBefore.Add(typeof(EyeSystem));
        SubscribeLocalEvent<OrbitraRatvarEminenceViewComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<OrbitraRatvarEminenceViewComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<EyeComponent>(ent, out var eye)) return;
        _eye.SetZoom(ent, Vector2.One, eye);
        _eye.SetRotation(ent, Angle.Zero, eye);
        _eye.SetOffset(ent, Vector2.Zero, eye);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_player.LocalEntity is not { } avatar || !HasComp<OrbitraRatvarEminenceViewComponent>(avatar) ||
            !TryComp<EyeComponent>(avatar, out var eye) || !TryComp<EyeComponent>(eye.Target, out var target)) return;
        _eye.SetZoom(avatar, target.Zoom, eye);
        _eye.SetRotation(avatar, target.Rotation, eye);
        _eye.SetOffset(avatar, target.Offset, eye);
    }
}
