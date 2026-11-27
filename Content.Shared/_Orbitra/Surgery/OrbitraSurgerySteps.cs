using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Одни и те же инструменты и переходы доступа используют окно стола и серверный обработчик взаимодействия.</summary>
[Prototype]
public sealed partial class OrbitraSurgeryProfilePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Dictionary<SurgeryProcedure, string[]> Steps = new();
    [DataField(required: true)] public string[] SurfaceAccess = [];
    [DataField(required: true)] public Dictionary<string, SurgeryAccess> OpensTo = new();
}

public static class OrbitraSurgerySteps
{
    public const string DonorOrgan = "DonorOrgan";
    public const string Prosthesis = "Prosthesis";
    public static readonly ProtoId<OrbitraSurgeryProfilePrototype> Profile = "OrbitraStandardSurgery";

    public static string[] Get(IPrototypeManager prototypes, SurgeryProcedure procedure, SurgeryZone zone)
    {
        var profile = prototypes.Index(Profile);
        if (procedure == SurgeryProcedure.OpenAccess && zone == SurgeryZone.Head)
            return profile.SurfaceAccess;

        return profile.Steps.TryGetValue(procedure, out var steps) ? steps : [];
    }

    public static bool Allows(SurgeryProcedure procedure, SurgeryAccess access, bool dressed) => procedure switch
    {
        SurgeryProcedure.OpenAccess => access != SurgeryAccess.Open,
        SurgeryProcedure.CloseAccess => access != SurgeryAccess.Closed,
        SurgeryProcedure.DressAccess => access != SurgeryAccess.Closed && !dressed,
        SurgeryProcedure.Reattach or SurgeryProcedure.InstallProsthesis or SurgeryProcedure.RemoveProsthesis => true,
        _ => access == SurgeryAccess.Open,
    };
}
