using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client._Orbitra.Lobby;

/// <summary>A lobby button opting into shared, accessibility-aware color transitions.</summary>
public sealed class OrbitraLobbyButton : Button
{
    public OrbitraLobbyButton()
    {
        OrbitraMotion.AttachButton(this);
    }
}
