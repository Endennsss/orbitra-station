using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Passive status presentation; data loading and retries belong to the owning screen.</summary>
public sealed class OrbitraStatusPanel : BoxContainer
{
    private readonly RichTextLabel _title = new();
    private readonly RichTextLabel _description = new();
    public Button ActionButton { get; } = new OrbitraButton { Visible = false };

    public OrbitraStatusPanel()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = OrbitraUiMetrics.Small;
        _title.AddStyleClass("LabelHeading");
        _description.AddStyleClass("OrbitraLobbyMuted");
        AddChild(_title);
        AddChild(_description);
        AddChild(ActionButton);
        ActionButton.AddStyleClass(OrbitraButtonStyles.Secondary);
        OrbitraMotion.AttachButton(ActionButton);
    }

    public void SetStatus(string title, string? description = null, string? action = null)
    {
        _title.SetMessage(title);
        _description.SetMessage(description ?? "");
        _description.Visible = !string.IsNullOrEmpty(description);
        ActionButton.Text = action;
        ActionButton.Visible = action != null;
    }
}
