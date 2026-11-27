using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared._Orbitra.Roadmap;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Roadmap;

public sealed class OrbitraLeftLobbyEvent(ICommonSession session) : EntityEventArgs
{
    public readonly ICommonSession Session = session;
}

public sealed partial class OrbitraAtlasSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    private readonly Dictionary<ICommonSession, OrbitraAtlasEui> _views = new();
    private readonly SemaphoreSlim _writes = new(1);
    private string _serverId = "";
    private OrbitraAtlasDocument? _document;
    private long _version;
    private bool _loading;
    private bool _loadFailed;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.ServerId, SetServerId, true);
        _admins.OnPermsChanged += OnPermissionsChanged;
        SubscribeLocalEvent<OrbitraLeftLobbyEvent>(ev =>
        {
            if (_views.TryGetValue(ev.Session, out var view))
                view.Close();
        });
    }

    public override void Shutdown()
    {
        _admins.OnPermsChanged -= OnPermissionsChanged;
        base.Shutdown();
    }

    private void SetServerId(string id)
    {
        foreach (var view in _views.Values.ToArray())
            view.Close();
        _serverId = id;
        _document = null;
        _version = 0;
        _loading = _loadFailed = false;
    }

    private bool InLobby(ICommonSession session) => _ticker.LobbyEnabled &&
        _ticker.PlayerGameStatuses.TryGetValue(session.UserId, out var state) &&
        state is PlayerGameStatus.ReadyToPlay or PlayerGameStatus.NotReadyToPlay;

    private bool CanEdit(ICommonSession session) =>
        _admins.HasAdminFlag(session, AdminFlags.Host) || _admins.HasAdminFlag(session, AdminFlags.RoadmapEdit);

    internal OrbitraAtlasEui? TryOpen(ICommonSession session)
    {
        if (!InLobby(session))
            return null;
        if (_views.TryGetValue(session, out var existing))
            return existing;

        var view = new OrbitraAtlasEui(this);
        _views.Add(session, view);
        _eui.OpenEui(view, session);
        if (_document == null && !_loading)
            Load();
        return view;
    }

    internal void Forget(OrbitraAtlasEui view) => _views.Remove(view.Player);

    private bool Current(OrbitraAtlasEui view) => _views.GetValueOrDefault(view.Player) == view && InLobby(view.Player);

    private void OnPermissionsChanged(AdminPermsChangedEventArgs ev)
    {
        if (_views.TryGetValue(ev.Player, out var view))
            view.StateDirty();
    }

    internal OrbitraRoadmapState Read(OrbitraAtlasEui view)
    {
        if (!Current(view))
            return new();
        var editor = CanEdit(view.Player);
        return new OrbitraRoadmapState
        {
            CanEdit = editor && _document != null,
            Entries = _document?.Read(editor) ?? [],
            Loading = _document == null && !_loadFailed,
            Notice = _loadFailed ? "orbitra-atlas-db-error" : view.Notice,
            ReplyTo = view.ReplyTo,
            Success = view.Success,
        };
    }

    // После конфликта в БД перечитываем документ, прежде чем сверять версию конкретной записи.
    private async Task Refresh(string key)
    {
        var (version, json) = await _db.GetOrbitraRoadmapAsync(key);
        if (key != _serverId || version < _version)
            return;
        _document = version == 0 ? new OrbitraAtlasDocument([]) :
            JsonSerializer.Deserialize<OrbitraAtlasDocument>(json) ?? throw new JsonException("Empty roadmap document");
        _version = version;
    }

    private async void Load()
    {
        var key = _serverId;
        _loading = true;
        _loadFailed = false;
        try
        {
            await Refresh(key);
        }
        catch (Exception e)
        {
            if (_serverId == key)
                _loadFailed = true;
            Log.Error($"Could not load lobby roadmap: {e}");
        }
        finally
        {
            if (_serverId == key)
            {
                _loading = false;
                Notify(false);
            }
        }
    }

    private void Notify(bool editorsOnly)
    {
        foreach (var (session, view) in _views)
        {
            if (!editorsOnly || CanEdit(session))
                view.StateDirty();
        }
    }

    internal async Task<string?> Change(OrbitraAtlasEui view, AtlasChange change)
    {
        if (!Current(view) || !CanEdit(view.Player))
            return "orbitra-atlas-no-access";

        await _writes.WaitAsync();
        try
        {
            var key = _serverId;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (!Current(view) || !CanEdit(view.Player))
                    return "orbitra-atlas-no-access";
                if (_document == null)
                    return "orbitra-atlas-db-error";
                var error = _document.TryChange(change, out var next);
                if (error != null)
                    return error;

                var expected = _version;
                if (await _db.TrySaveOrbitraRoadmapAsync(key, expected, JsonSerializer.Serialize(next)))
                {
                    if (key == _serverId)
                    {
                        _document = next;
                        _version = expected + 1;
                        Notify(change.Action == AtlasAction.Save);
                    }
                    return null;
                }
                await Refresh(key);
                Notify(false);
            }
            return "orbitra-atlas-conflict";
        }
        catch (Exception e)
        {
            Log.Error($"Could not save lobby roadmap: {e}");
            return "orbitra-atlas-db-error";
        }
        finally
        {
            _writes.Release();
        }
    }
}
