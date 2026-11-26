using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Surgery;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    private readonly Dictionary<SurgeryOrgan, OrbitraButton> _organButtons = new();
    private SurgeryOrgan? _organ;
    private OrbitraOrganSystem _organs = default!;

    private void InitializeOrgans()
    {
        _organs = _entity.System<OrbitraOrganSystem>();
        foreach (var kind in OrbitraOrganSystem.Kinds)
        {
            var button = new OrbitraButton { ToggleMode = true, ClipText = true, HorizontalExpand = true };
            button.OnPressed += _ =>
            {
                if (_state?.ActiveProcedure != null)
                    return;
                _organ = _organ == kind ? null : kind;
                _procedure = null;
                ClearFeedback();
                RefreshSelection();
            };
            OrganButtons.AddChild(button);
            _organButtons.Add(kind, button);
        }
    }

    private string OrganName(SurgeryOrgan kind) => _loc.GetString($"orbitra-surgery-organ-{kind.ToString().ToLowerInvariant()}");
    private string ConditionName(SurgeryOrganCondition condition) =>
        _loc.GetString($"orbitra-surgery-organ-condition-{condition.ToString().ToLowerInvariant()}");

    private string OrganReading(SurgeryOrgan kind, SurgeryOrganCondition condition) =>
        _loc.GetString("orbitra-surgery-organ-reading", ("organ", OrganName(kind)), ("condition", ConditionName(condition)));

    private void UpdateOrgans()
    {
        OrganButtons.Visible = _zone == SurgeryZone.Torso && _state?.Organs.Count > 0;
        if (!OrganButtons.Visible)
            return;

        foreach (var (kind, button) in _organButtons)
        {
            button.Visible = _state!.Organs.TryGetValue(kind, out var condition);
            button.Disabled = _state.FieldTable || _state.ActiveProcedure != null;
            button.Text = OrganReading(kind, condition);
            button.ToolTip = button.Text;
            button.Pressed = _organ == kind;
        }
    }
}
