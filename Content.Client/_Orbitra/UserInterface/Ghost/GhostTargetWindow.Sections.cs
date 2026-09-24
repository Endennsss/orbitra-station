using System.Numerics;
using Content.Client._Orbitra.Lobby;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed partial class GhostTargetWindow
{
    private readonly OrbitraJournalTabs _orbitraSections = new();
    private bool _orbitraAntagonists;
    private readonly float[] _orbitraSectionScroll = new float[2];

    private void InitializeOrbitraSections()
    {
        _orbitraSections.AddSection("station", Loc.GetString("orbitra-ghost-station"));
        _orbitraSections.AddSection("antagonists", Loc.GetString("orbitra-ghost-antagonists"));
        _orbitraSections.Select("station");
        _orbitraSections.Selected += SelectOrbitraSection;
        var container = GhostScroll.Parent!;
        container.AddChild(_orbitraSections);
        _orbitraSections.SetPositionInParent(_orbitraSummary.GetPositionInParent());
    }

    private void SelectOrbitraSection(string section)
    {
        var antagonists = section == "antagonists";
        if (antagonists == _orbitraAntagonists)
            return;

        _orbitraSectionScroll[_orbitraAntagonists ? 1 : 0] = GhostScroll.VScroll;
        _orbitraAntagonists = antagonists;
        _orbitraSections.Select(section);
        FilterOrbitraTargets();
        // Размер прокрутки пересчитывается после смены видимости групп.
        UserInterfaceManager.DeferAction(() =>
        {
            if (!Disposed)
                GhostScroll.SetScrollValue(new Vector2(0, _orbitraSectionScroll[_orbitraAntagonists ? 1 : 0]));
        });
    }

    private void ResetOrbitraSections()
    {
        _orbitraAntagonists = false;
        Array.Clear(_orbitraSectionScroll);
        _orbitraSections.Select("station");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _orbitraSections.Selected -= SelectOrbitraSection;
        base.Dispose(disposing);
    }
}
