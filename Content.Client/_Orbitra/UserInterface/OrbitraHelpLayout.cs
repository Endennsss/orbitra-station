using System.Linq;
using Content.Client.Administration.UI.Bwoink;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Wraps entry-screen AHelp actions without modifying the in-round administration layout.</summary>
internal static class OrbitraHelpLayout
{
    public static void Attach(BwoinkControl control)
    {
        if (control.PopOut.Parent is not BoxContainer bar)
            return;
        var actions = new WrapContainer { SeparationOverride = 8, CrossSeparationOverride = 8 };
        foreach (var child in bar.Children.ToArray())
        {
            child.Orphan();
            if (child.GetType() == typeof(Control))
            {
                child.Dispose();
                continue;
            }
            actions.AddChild(child);
        }
        bar.SetHeight = float.NaN;
        bar.AddChild(actions);
    }
}
