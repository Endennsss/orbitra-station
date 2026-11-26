using Content.Shared._Orbitra.Surgery;
using Content.Shared.MedicalScanner;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerControl
{
    private NetEntity? _orbitraPatient;
    private static readonly SurgeryInjury[] OrbitraInjuries = Enum.GetValues<SurgeryInjury>();

    private void PopulateOrbitraTrauma(HealthAnalyzerUiState state)
    {
        // Прогноз клиента может обновить общие показатели раньше, чем придёт снимок с сервера.
        if (state.OrbitraTrauma == null && _orbitraPatient == state.TargetEntity)
            return;

        _orbitraPatient = state.TargetEntity;
        OrbitraTraumaContainer.RemoveAllChildren();
        var organFindings = state.OrbitraOrgans is { } organs &&
            (organs.ContainsValue(SurgeryOrganCondition.Damaged) || organs.ContainsValue(SurgeryOrganCondition.Nonfunctional) ||
            organs.ContainsValue(SurgeryOrganCondition.Missing));
        OrbitraTraumaContainer.Visible = state.OrbitraTrauma is { Count: > 0 } || organFindings;
        if (!OrbitraTraumaContainer.Visible || state.OrbitraTrauma is not { } readings)
            return;

        OrbitraTraumaContainer.AddChild(new Label { Text = Loc.GetString("orbitra-trauma-analyzer-title") });
        if (organFindings)
        {
            foreach (var (kind, condition) in state.OrbitraOrgans!)
            {
                if (condition != SurgeryOrganCondition.Healthy)
                {
                    OrbitraTraumaContainer.AddChild(new Label { Text = Loc.GetString("orbitra-surgery-organ-reading",
                        ("organ", Loc.GetString($"orbitra-surgery-organ-{kind.ToString().ToLowerInvariant()}")),
                        ("condition", Loc.GetString($"orbitra-surgery-organ-condition-{condition.ToString().ToLowerInvariant()}"))) });
                }
            }
        }
        foreach (var (zone, injuries) in readings)
        {
            var findings = new List<string>();
            foreach (var injury in OrbitraInjuries)
            {
                if ((injuries & injury) != 0)
                    findings.Add(Loc.GetString($"orbitra-surgery-injury-{injury.ToString().ToLowerInvariant()}"));
            }
            OrbitraTraumaContainer.AddChild(new RichTextLabel
            {
                Text = Loc.GetString("orbitra-trauma-analyzer-zone",
                    ("zone", Loc.GetString($"orbitra-surgery-zone-{zone.ToString().ToLowerInvariant()}")),
                    ("findings", string.Join(", ", findings))),
                MaxWidth = 300,
            });
        }
    }
}
