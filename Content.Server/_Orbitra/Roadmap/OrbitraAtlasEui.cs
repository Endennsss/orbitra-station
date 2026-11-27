using Content.Server.EUI;
using Content.Shared._Orbitra.Roadmap;
using Content.Shared.Eui;

namespace Content.Server._Orbitra.Roadmap;

public sealed class OrbitraAtlasEui(OrbitraAtlasSystem atlas) : BaseEui
{
    internal Guid ReplyTo;
    internal string? Notice;
    internal bool Success;
    private bool _busy;
    private bool _closed;

    public override void Opened() => StateDirty();

    public override void Closed()
    {
        _closed = true;
        atlas.Forget(this);
    }

    public override EuiStateBase GetNewState() => atlas.Read(this);

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (_closed || _busy || msg is not AtlasChange change)
            return;
        _busy = true;
        try
        {
            var error = await atlas.Change(this, change);
            if (_closed)
                return;
            ReplyTo = change.RequestId;
            Success = error == null;
            Notice = error;
            StateDirty();
        }
        finally
        {
            _busy = false;
        }
    }
}
