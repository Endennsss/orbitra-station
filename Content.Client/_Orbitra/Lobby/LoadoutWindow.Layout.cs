using System.Numerics;
using Content.Client._Orbitra.Lobby;

namespace Content.Client.Lobby.UI.Loadouts;

public sealed partial class LoadoutWindow
{
    private void InitializeOrbitraLoadout()
    {
        var size = UserInterfaceManager.RootControl.Size;
        SetSize = Vector2.Min(new Vector2(760, 700), Vector2.Max(Vector2.Zero, size - new Vector2(32)));
        OrbitraEntryWindow.Attach(this);
        OrbitraEditorStyles.Apply(this);
    }
}
