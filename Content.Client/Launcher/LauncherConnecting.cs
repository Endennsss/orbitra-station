using System;
using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Launcher
{
    public sealed partial class LauncherConnecting : Robust.Client.State.State
    {
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IClientNetManager _clientNetManager = default!;
        [Dependency] private IGameController _gameController = default!;
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IClipboardManager _clipboard = default!;
        [Dependency] private ILogManager _logManager = default!;

        private LauncherConnectingGui? _control;
        private ISawmill _sawmill = default!;

        private Page _currentPage;
        private string? _connectFailReason;

        public string? Address => _disconnectInfo.DirectTarget?.Address ?? _gameController.LaunchState.Ss14Address ?? _gameController.LaunchState.ConnectAddress; // Orbitra-Edit

        public string? ConnectFailReason
        {
            get => _connectFailReason;
            private set
            {
                _connectFailReason = value;
                ConnectFailReasonChanged?.Invoke(value);
            }
        }

        public string? LastDisconnectReason => _disconnectInfo.LastNetDisconnectedArgs?.Reason ?? _baseClient.LastDisconnectReason; // Orbitra-Edit

        public Page CurrentPage
        {
            get => _currentPage;
            private set
            {
                _currentPage = value;
                // Orbitra added start - новая попытка сбрасывает результат предыдущего рейса.
                if (value == Page.Connecting)
                {
                    WasDocked = false;
                    ConnectFailReason = null;
                }
                // Orbitra added end
                PageChanged?.Invoke(value);
            }
        }

        public ClientConnectionState ConnectionState => _clientNetManager.ClientConnectState;

        public event Action<Page>? PageChanged;
        public event Action<string?>? ConnectFailReasonChanged;
        public event Action<ClientConnectionState>? ConnectionStateChanged;
        public event Action<NetConnectFailArgs>? ConnectFailed;

        protected override void Startup()
        {
            _control = new LauncherConnectingGui(this, _random, _prototypeManager, _cfg, _clipboard);

            _sawmill = _logManager.GetSawmill("launcher-ui");

            CurrentPage = Page.Connecting; // Orbitra-Edit - экран прочитает начальную страницу при входе в дерево.
            _userInterfaceManager.StateRoot.AddChild(_control);

            _clientNetManager.ConnectFailed += OnConnectFailed;
            _clientNetManager.ClientConnectStateChanged += OnConnectStateChanged;
        }

        protected override void Shutdown()
        {
            _clientNetManager.ConnectFailed -= OnConnectFailed;
            _clientNetManager.ClientConnectStateChanged -= OnConnectStateChanged;
            // Orbitra edit start - выход из дерева снимает подписки экрана.
            _control?.Orphan();
            _control = null;
            // Orbitra edit end
        }

        private void OnConnectFailed(object? _, NetConnectFailArgs args)
        {
            if (args.RedialFlag && !IsDirectConnection) // Orbitra-Edit
            {
                // We've just *attempted* to connect and we've been told we need to redial, so do it.
                // Result deliberately discarded.
                Redial();
            }
            ConnectFailReason = args.Reason;
            CurrentPage = Page.ConnectFailed;
            ConnectFailed?.Invoke(args);
        }

        private void OnConnectStateChanged(ClientConnectionState state)
        {
            ConnectionStateChanged?.Invoke(state);
        }

        public void RetryConnect()
        {
            // Orbitra added start - повторяем подключение к адресу из главного меню.
            if (_disconnectInfo.DirectTarget is { } target)
            {
                CurrentPage = Page.Connecting;
                _baseClient.ConnectToServer(target.Host, target.Port);
                return;
            }
            // Orbitra added end
            if (_gameController.LaunchState.ConnectEndpoint != null)
            {
                // Orbitra edit start - быстрый отказ сервера может прийти прямо при вызове.
                CurrentPage = Page.Connecting;
                _baseClient.ConnectToServer(_gameController.LaunchState.ConnectEndpoint);
                // Orbitra edit end
            }
        }

        public bool Redial()
        {
            try
            {
                if (_gameController.LaunchState.Ss14Address != null)
                {
                    _gameController.Redial(_gameController.LaunchState.Ss14Address);
                    return true;
                }
                else
                {
                    _sawmill.Info($"Redial not possible, no Ss14Address");
                }
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Redial exception: {ex}");
            }
            return false;
        }

        public void Exit()
        {
            // Orbitra added start - после ошибки прямого подключения можно исправить адрес в меню.
            if (CanReturnToMenu)
            {
                _stateManager.RequestStateChange<MainMenu.MainScreen>();
                return;
            }
            // Orbitra added end
            _gameController.Shutdown("Exit button pressed");
        }

        public void SetDisconnected()
        {
            CurrentPage = Page.Disconnected;
        }

        public enum Page : byte
        {
            Connecting,
            ConnectFailed,
            Disconnected,
        }
    }
}
