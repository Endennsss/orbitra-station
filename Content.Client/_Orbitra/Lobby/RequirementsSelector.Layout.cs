using System.Linq;
using System.Numerics;
using Robust.Shared.Utility;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI.Roles;

public sealed partial class RequirementsSelector
{
    private bool _orbitraLayoutApplied;
    private (string, int)[] _orbitraItems = [];
    private OptionButton? _orbitraCompact;
    private BoxContainer? _orbitraHeading;
    private RichTextLabel? _orbitraReason;
    private FormattedMessage? _orbitraRequirements;
    private bool _orbitraLocked;

    internal void ApplyOrbitraLayout()
    {
        if (_orbitraLayoutApplied)
            return;
        _orbitraLayoutApplied = true;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;
        TitleLabel.MinWidth = 0;
        TitleLabel.ClipText = true;
        TitleLabel.HorizontalExpand = true;
        OptionsContainer.SetWidth = float.NaN;
        OptionsContainer.HorizontalExpand = true;
        OptionsContainer.Orientation = LayoutOrientation.Vertical;
        _options.MaxWidth = 440;
        _options.HorizontalAlignment = HAlignment.Left;
        _orbitraCompact = new OptionButton { Name = "OrbitraCompactPriority", Visible = false, MaxWidth = 440 };
        foreach (var (text, id) in _orbitraItems)
            _orbitraCompact.AddItem(Loc.GetString(text), id);
        _orbitraCompact.SelectId(_options.SelectedId);
        _orbitraCompact.OnItemSelected += args =>
        {
            Select(args.Id);
            OnSelected?.Invoke(args.Id);
        };
        OptionsContainer.AddChild(_orbitraCompact);
        _orbitraReason = new RichTextLabel { Visible = _orbitraLocked, HorizontalExpand = true };
        if (_orbitraRequirements != null)
            _orbitraReason.SetMessage(_orbitraRequirements);
        AddChild(_orbitraReason);
        var heading = new BoxContainer { SeparationOverride = 8 };
        foreach (var child in Children.ToArray())
        {
            if (child == OptionsContainer || child == _orbitraReason)
                continue;
            child.Orphan();
            heading.AddChild(child);
        }
        AddChild(heading);
        heading.SetPositionFirst();
        _orbitraHeading = heading;
    }

    internal void AttachOrbitraEquipment(Button button)
    {
        ApplyOrbitraLayout();
        button.Margin = new Thickness(0);
        _orbitraHeading!.AddChild(button);
    }

    private void SetOrbitraRequirements(FormattedMessage? reason)
    {
        _orbitraRequirements = reason;
        _orbitraLocked = reason != null;
        if (_orbitraReason == null)
            return;
        _orbitraReason.Visible = _orbitraLocked;
        if (reason != null)
            _orbitraReason.SetMessage(reason);
        InvalidateMeasure();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_orbitraLayoutApplied)
        {
            var compact = availableSize.X < 520;
            _lockStripe.Visible = false;
            _options.Visible = !compact && !_orbitraLocked;
            _options.SetWidth = Math.Min(440, availableSize.X);
            _orbitraCompact!.Visible = compact && !_orbitraLocked;
        }
        return base.MeasureOverride(availableSize);
    }
}
