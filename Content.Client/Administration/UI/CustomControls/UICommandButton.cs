using System;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;

namespace Content.Client.Administration.UI.CustomControls
{
    public sealed class UICommandButton : CommandButton
    {
        public Type? WindowType { get; set; }
        private BaseWindow? _window; // Orbitra-Edit - поддерживает FancyWindow без изменения команды.

        // Orbitra-Edit - дочерний инструмент не переживает уничтожение владельца.
        protected override void Dispose(bool disposing)
        {
            if (HasStyleClass("OrbitraEditorControl"))
                _window?.Dispose();
            _window = null;
            base.Dispose(disposing);
        }

        protected override void Execute(ButtonEventArgs obj)
        {
            if (WindowType == null)
                return;
            // Orbitra-Edit - явно оформленные инструменты сохраняют размер и состояние при повторном вызове.
            if (!HasStyleClass("OrbitraEditorControl") || _window is null || _window.Disposed)
                _window = (BaseWindow) IoCManager.Resolve<IDynamicTypeFactory>().CreateInstance(WindowType);
            _window?.OpenCentered();
        }
    }
}
