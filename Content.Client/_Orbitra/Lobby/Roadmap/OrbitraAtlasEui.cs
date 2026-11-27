using System.Linq;
using Content.Client.Eui;
using Content.Client.Lobby;
using Content.Shared._Orbitra.Roadmap;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Orbitra.Lobby.Roadmap;

public sealed partial class OrbitraAtlasController : UIController, IOnStateExited<LobbyState>
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IStateManager _state = default!;
    internal OrbitraAtlasEui? Active;

    public void OpenAtlas()
    {
        if (_state.CurrentState is not LobbyState)
            return;
        if (Active?.BringToFront() != true)
            _console.ExecuteCommand("roadmap");
    }

    public void OnStateExited(LobbyState state) => Active?.Hide();
}

[UsedImplicitly]
public sealed partial class OrbitraAtlasEui : BaseEui
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IStateManager _state = default!;
    private OrbitraAtlasWindow? _window;
    private OrbitraAtlasEditorWindow? _editor;
    private Guid? _pendingId;
    private bool _queuedEdit;
    private Guid? _queuedEntry;
    private OrbitraRoadmapState _snapshot = new() { Loading = true };
    private OrbitraAtlasController Controller => _ui.GetUIController<OrbitraAtlasController>();

    public override void Opened()
    {
        if (_state.CurrentState is not LobbyState)
            return;
        Controller.Active = this;
        _window = new OrbitraAtlasWindow();
        _window.EditRequested += Edit;
        _window.OnClose += RequestClose;
        _window.Display(_snapshot);
        _window.OpenCentered();
    }

    internal bool BringToFront()
    {
        if (_window?.IsOpen != true)
            return false;
        if (_editor?.IsOpen == true)
            _editor.MoveToFront();
        else
            _window.MoveToFront();
        return true;
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not OrbitraRoadmapState snapshot || _window == null)
            return;
        _snapshot = snapshot;
        if (!snapshot.CanEdit)
        {
            _pendingId = null;
            _queuedEdit = false;
            CloseEditor();
        }
        _window.Display(snapshot);
        _editor?.Display(snapshot);
        if (_pendingId == snapshot.ReplyTo)
        {
            _pendingId = null;
            if (_queuedEdit)
            {
                _queuedEdit = false;
                Edit(_queuedEntry);
            }
        }
    }

    private void Edit(Guid? id)
    {
        if (!_snapshot.CanEdit)
            return;
        if (_editor?.IsOpen == true)
        {
            _editor.MoveToFront();
            return;
        }
        if (_pendingId != null)
        {
            // Закрытая форма могла ещё не получить подтверждение своей записи.
            _queuedEntry = id;
            _queuedEdit = true;
            return;
        }
        var entry = id == null ? null : _snapshot.Entries.FirstOrDefault(item => item.Id == id);
        if (id != null && entry == null)
            return;
        _editor = new OrbitraAtlasEditorWindow(id ?? Guid.NewGuid(), entry);
        _editor.Submitted += Submit;
        _editor.OnClose += CloseEditor;
        _editor.Display(_snapshot);
        _editor.OpenCentered();
    }

    private void Submit(AtlasChange change)
    {
        if (_window?.IsOpen == true && _snapshot.CanEdit)
        {
            _pendingId = change.RequestId;
            SendMessage(change);
        }
    }

    private void CloseEditor()
    {
        if (_editor == null)
            return;
        _editor.Submitted -= Submit;
        _editor.OnClose -= CloseEditor;
        _editor.Close();
        _editor = null;
    }

    private void RequestClose()
    {
        SendMessage(new CloseEuiMessage());
        Hide();
    }

    internal void Hide()
    {
        CloseEditor();
        _snapshot = new();
        if (_window != null)
        {
            _window.OnClose -= RequestClose;
            _window.EditRequested -= Edit;
            _window.Close();
            _window = null;
        }
        if (Controller.Active == this)
            Controller.Active = null;
    }

    public override void Closed() => Hide();
}
