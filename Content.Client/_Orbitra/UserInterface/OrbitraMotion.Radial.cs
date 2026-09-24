using Content.Shared.CCVar;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.UserInterface;

internal sealed partial class OrbitraMotion
{
    private readonly List<OrbitraRadialSector> _radialSurfaces = new();

    internal static void AnimateRadial(OrbitraRadialSector sector)
    {
        var runner = GetRunner();
        if (runner._configuration.GetCVar(CCVars.ReducedMotion) || !CanAnimate(sector))
        {
            sector.FinishSurface();
            return;
        }
        if (!runner._radialSurfaces.Contains(sector))
            runner._radialSurfaces.Add(sector);
    }

    internal static void RemoveRadial(OrbitraRadialSector sector)
    {
        foreach (var child in IoCManager.Resolve<IUserInterfaceManager>().RootControl.Children)
        {
            if (child is OrbitraMotion runner)
                runner._radialSurfaces.Remove(sector);
        }
        sector.FinishSurface();
    }

    private void AdvanceRadial(float seconds, bool reduced)
    {
        for (var i = _radialSurfaces.Count - 1; i >= 0; i--)
        {
            if (_radialSurfaces[i].AdvanceSurface(seconds, reduced))
                _radialSurfaces.RemoveAt(i);
        }
    }
}
