using Content.Shared._Orbitra.Surgery;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    // Перехватываем выбранную операцию до обычного применения донора, бинта или неверного инструмента.
    private bool TryUseSurgicalTool(EntityUid user, EntityUid tool, EntityUid target)
    {
        if (!TryComp<OrbitraSurgerySelectionComponent>(user, out var selected) || selected.Patient != target)
            return false;

        var ev = new OrbitraSurgeryToolUsedEvent(tool);
        RaiseLocalEvent(user, ref ev);
        return true;
    }
}
