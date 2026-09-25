using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Ghost.Widgets;

public sealed partial class GhostGui
{
    private void InitializeOrbitraGhostBar()
    {
        var old = ReturnToBodyButton.Parent!;
        var buttons = old.Children.ToArray();
        var flow = new WrapContainer { SeparationOverride = 8 };
        foreach (var button in buttons)
        {
            button.Orphan();
            flow.AddChild(button);
        }
        old.Orphan();
        old.Dispose();
        var panel = new PanelContainer();
        panel.AddStyleClass("OrbitraGhostBar");
        panel.AddChild(flow);
        AddChild(panel);
        OrbitraEditorStyles.Apply(panel);
    }
}
