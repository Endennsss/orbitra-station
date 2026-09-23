using Content.Client._Orbitra.UserInterface;

namespace Content.Client.Options.UI;

public sealed partial class OptionsTabControlRow
{
    private void ShowOrbitraFeedback(OrbitraNotificationKind kind, string message)
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not OptionsMenu menu) continue;
            menu.ShowOrbitraFeedback(kind, message);
            return;
        }
    }
}

public sealed partial class OptionsMenu
{
    internal void ShowOrbitraFeedback(OrbitraNotificationKind kind, string message) => OrbitraFeedback.Show(kind, message);
}
