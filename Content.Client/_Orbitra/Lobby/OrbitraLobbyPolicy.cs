using System.Linq;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Pure navigation and responsive layout rules shared by the lobby and tests.</summary>
public static class OrbitraLobbyPolicy
{
    public static int? NextSlot(IEnumerable<int> slots, int selected, int step)
    {
        var ordered = slots.Order().ToArray();
        if (ordered.Length <= 1 || step == 0)
            return null;
        var index = Array.IndexOf(ordered, selected);
        if (index < 0)
            return ordered[0];
        return ordered[(index + Math.Sign(step) + ordered.Length) % ordered.Length];
    }

    public static bool DockInformation(float width) => width >= 1200;
    public static bool DockChat(float width) => width >= 900;
}
