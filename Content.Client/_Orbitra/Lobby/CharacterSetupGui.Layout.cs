using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGui
{
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
        OrbitraCharacterToggle.Pressed = OrbitraCharacterPanel.Visible = false;
        OrbitraToolsToggle.Pressed = OrbitraToolsPanel.Visible = false;
        OrbitraMenuDismiss.Visible = false;
    }

    private void InitializeOrbitraSetup(HumanoidProfileEditor editor)
    {
        Content.Client._Orbitra.Lobby.OrbitraMotion.AttachReveal(OrbitraCharacterPanel, 0.10f);
        Content.Client._Orbitra.Lobby.OrbitraMotion.AttachReveal(OrbitraToolsPanel, 0.10f);
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
            OrbitraCharacterPanel.Visible = args.Pressed;
            if (args.Pressed)
                OrbitraToolsToggle.Pressed = OrbitraToolsPanel.Visible = false;
            OrbitraMenuDismiss.Visible = OrbitraCharacterPanel.Visible || OrbitraToolsPanel.Visible;
        };
        OrbitraToolsToggle.OnToggled += args =>
        {
            OrbitraToolsPanel.Visible = args.Pressed;
            if (args.Pressed)
                OrbitraCharacterToggle.Pressed = OrbitraCharacterPanel.Visible = false;
            OrbitraMenuDismiss.Visible = OrbitraCharacterPanel.Visible || OrbitraToolsPanel.Visible;
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
        OrbitraCharacterToggle.Pressed = OrbitraCharacterPanel.Visible = false;
        OrbitraToolsToggle.Pressed = OrbitraToolsPanel.Visible = false;
        OrbitraMenuDismiss.Visible = false;
        Content.Client._Orbitra.Lobby.OrbitraEditorStyles.Apply(Characters);
    }
}
