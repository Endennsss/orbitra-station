using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Показывает статус. Загрузка данных и повторные попытки выполняются экраном-владельцем.</summary>
public sealed class OrbitraStatusPanel : BoxContainer
{
    private readonly RichTextLabel _title = new();
    private readonly RichTextLabel _description = new();
    /// <summary>Локализованный заголовок без разметки для пустого состояния.</summary>
    public string Title { set => _title.SetMessage(value); }
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
