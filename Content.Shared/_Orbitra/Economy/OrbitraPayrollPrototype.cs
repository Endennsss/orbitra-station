using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Economy;

/// <summary>Стартовый баланс и тарифы экипажа на одну смену.</summary>
[Prototype]
public sealed partial class OrbitraPayrollPrototype : IPrototype
{
    public static readonly ProtoId<OrbitraPayrollPrototype> Station = "OrbitraStationPayroll";

    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public TimeSpan Interval = TimeSpan.FromMinutes(15);
    [DataField] public OrbitraPayrollGrade DefaultGrade = new();
    [DataField] public List<OrbitraPayrollGrade> Grades = new();

    public OrbitraPayrollGrade GetGrade(ProtoId<JobPrototype> job)
    {
        foreach (var grade in Grades)
        {
            if (grade.Jobs.Contains(job))
                return grade;
        }

        return DefaultGrade;
    }
}

[DataDefinition]
public sealed partial class OrbitraPayrollGrade
{
    [DataField] public int StartingMinimum = 45;
    [DataField] public int StartingMaximum = 75;
    [DataField] public int Salary = 180;
    [DataField] public HashSet<ProtoId<JobPrototype>> Jobs = new();
}
