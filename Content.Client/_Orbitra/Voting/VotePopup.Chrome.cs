using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Voting.UI;

public sealed partial class VotePopup
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = Math.Min(availableSize.X, 400);
        return new Vector2(width, base.MeasureOverride(new Vector2(width, availableSize.Y)).Y);
    }

    private void ApplyOrbitraChrome()
    {
        OrbitraControlTheme.Apply(this);
        OrbitraKeyboardNavigation.Attach(this);
    }

    private sealed class VoteOptionButton : OrbitraButton
    {
        private readonly RichTextLabel _caption = new() { MouseFilter = MouseFilterMode.Ignore };

        public VoteOptionButton(ButtonGroup group)
        {
            ToggleMode = true;
            Group = group;
            HorizontalExpand = true;
            Label.Visible = false;
            AddChild(_caption);
            AddStyleClass(OrbitraButtonStyles.Secondary);
        }

        public void SetCaption(string text)
        {
            if (Text == text)
                return;

            Text = text;
            _caption.SetMessage(text);
        }
    }
}
