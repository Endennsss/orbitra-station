using Content.Client._Orbitra.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed partial class GhostTargetWindow
{
    private bool _orbitraAwaiting;
    private bool _orbitraLoadFailed;
    private float _orbitraWait;
    private uint _orbitraRequestRound;
    public event Action? OrbitraRetryRequested;

    public bool BeginOrbitraRequest()
    {
        if (_orbitraAwaiting || !IsOpen || OrbitraEntryWindow.IsClosing(this))
            return false;
        _orbitraAwaiting = true;
        _orbitraLoadFailed = false;
        _orbitraWait = 0;
        _orbitraRequestRound = IoCManager.Resolve<IEntityManager>()
            .System<Content.Client.Ghost.GhostSystem>().OrbitraRoundGeneration;
        _orbitraStatus.Visible = true;
        _orbitraStatus.SetStatus(Loc.GetString("orbitra-ghost-loading"));
        GhostScroll.Visible = false;
        return true;
    }

    public bool AcceptOrbitraResponse()
    {
        if (!IsOpen || OrbitraEntryWindow.IsClosing(this) || _orbitraRequestRound !=
            IoCManager.Resolve<IEntityManager>().System<Content.Client.Ghost.GhostSystem>().OrbitraRoundGeneration)
            return false;
        _orbitraAwaiting = false;
        _orbitraLoadFailed = false;
        GhostScroll.Visible = true;
        return true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_orbitraAwaiting)
            return;
        if (!IsOpen || OrbitraEntryWindow.IsClosing(this) || _orbitraRequestRound !=
            IoCManager.Resolve<IEntityManager>().System<Content.Client.Ghost.GhostSystem>().OrbitraRoundGeneration)
        {
            _orbitraAwaiting = false;
            return;
        }
        _orbitraWait += args.DeltaSeconds;
        if (_orbitraWait < 10)
            return;
        _orbitraAwaiting = false;
        _orbitraLoadFailed = true;
        _orbitraStatus.SetStatus(Loc.GetString("orbitra-ghost-load-error"), action: Loc.GetString("orbitra-ghost-retry"));
    }
}
