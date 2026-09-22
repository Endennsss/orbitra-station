using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGui
{
    private Content.Client._Orbitra.UserInterface.OrbitraVisibility _orbitraProfilesMotion = default!;
    private Content.Client._Orbitra.UserInterface.OrbitraVisibility _orbitraToolsMotion = default!;
    public event Action? OrbitraExitRequested;

    /// <summary>Closes a local menu first, otherwise requests the normal guarded editor exit.</summary>
    public void HandleOrbitraEscape()
    {
        if (OrbitraCharacterPanel.Visible || OrbitraToolsPanel.Visible)
        {
            CloseOrbitraMenus();
            return;
        }
        OrbitraExitRequested?.Invoke();
    }

    private void CloseOrbitraMenus()
    {
        OrbitraCharacterToggle.Pressed = false;
        OrbitraToolsToggle.Pressed = false;
        _orbitraProfilesMotion.SetShown(false);
        _orbitraToolsMotion.SetShown(false);
        OrbitraMenuDismiss.Visible = false;
    }

    private void InitializeOrbitraSetup(HumanoidProfileEditor editor)
    {
        _orbitraProfilesMotion = new(OrbitraCharacterPanel);
        _orbitraToolsMotion = new(OrbitraToolsPanel);
        editor.AttachOrbitraSetupControls(CloseButton, OrbitraToolsContents);
        OrbitraMenuDismiss.OnKeyBindDown += args =>
        {
            if (args.Function != Robust.Shared.Input.EngineKeyFunctions.UIClick)
                return;
            CloseOrbitraMenus();
            args.Handle();
        };
        OrbitraCharacterToggle.OnToggled += args =>
        {
            _orbitraProfilesMotion.SetShown(args.Pressed);
            if (args.Pressed)
            {
                OrbitraToolsToggle.Pressed = false;
                _orbitraToolsMotion.SetShown(false);
            }
            OrbitraMenuDismiss.Visible = OrbitraCharacterToggle.Pressed || OrbitraToolsToggle.Pressed;
        };
        OrbitraToolsToggle.OnToggled += args =>
        {
            _orbitraToolsMotion.SetShown(args.Pressed);
            if (args.Pressed)
            {
                OrbitraCharacterToggle.Pressed = false;
                _orbitraProfilesMotion.SetShown(false);
            }
            OrbitraMenuDismiss.Visible = OrbitraCharacterToggle.Pressed || OrbitraToolsToggle.Pressed;
        };
        OnResized += () =>
        {
            OrbitraSetupTitle.Visible = BackgroundPanel.Width >= 1100;
            OrbitraCharacterPanel.SetWidth = Math.Min(340, Math.Max(200, Width - 32));
            OrbitraToolsPanel.MaxHeight = Math.Max(100, Height - 100);
            OrbitraCharacterPanel.MaxHeight = OrbitraToolsPanel.MaxHeight;
        };
    }

    private void RefreshOrbitraCharacterTitle()
    {
        var name = _preferencesManager.Preferences?.SelectedCharacter?.Name;
        OrbitraCharacterToggle.Text = Loc.GetString("orbitra-editor-selected-profile",
            ("name", string.IsNullOrWhiteSpace(name) ? Loc.GetString("orbitra-lobby-unnamed") : name));
        OrbitraCharacterToggle.ToolTip = OrbitraCharacterToggle.Text;
        CloseOrbitraMenus();
        Content.Client._Orbitra.UserInterface.OrbitraEditorStyles.Apply(Characters);
    }
}
