namespace Content.Shared._Orbitra.Surgery;

/// <summary>Последовательности игровых инструментов общие для подсказок окна и проверки на сервере.</summary>
public static class OrbitraSurgerySteps
{
    private static readonly string[] Fracture = ["Retractor", "OrbitraBoneSetter", "Cautery"];
    private static readonly string[] InternalBleeding = ["Scalpel", "Hemostat", "Cautery"];
    private static readonly string[] Shrapnel = ["Retractor", "Hemostat", "Cautery"];
    private static readonly string[] Amputate = ["Scalpel", "Saw", "Cautery"];
    public const string Prosthesis = "Prosthesis";
    private static readonly string[] InstallProsthesis = ["Drill", Prosthesis];
    private static readonly string[] RemoveProsthesis = ["Retractor", "Drill"];
    private static readonly string[] Reattach = ["Hemostat", "DetachedBody"];

    public const string DonorOrgan = "DonorOrgan";
    private static readonly string[] RepairOrgan = ["Scalpel", "Hemostat", "Cautery"];
    private static readonly string[] ExtractOrgan = ["Scalpel", "Retractor", "Hemostat"];
    private static readonly string[] TransplantOrgan = ["Retractor", "Hemostat", DonorOrgan];

    public static string[] Get(SurgeryProcedure procedure) => procedure switch
    {
        SurgeryProcedure.RepairOrgan => RepairOrgan,
        SurgeryProcedure.ExtractOrgan => ExtractOrgan,
        SurgeryProcedure.TransplantOrgan => TransplantOrgan,
        SurgeryProcedure.Fracture => Fracture,
        SurgeryProcedure.InternalBleeding => InternalBleeding,
        SurgeryProcedure.Shrapnel => Shrapnel,
        SurgeryProcedure.Amputate => Amputate,
        SurgeryProcedure.Reattach => Reattach,
        SurgeryProcedure.InstallProsthesis => InstallProsthesis,
        SurgeryProcedure.RemoveProsthesis => RemoveProsthesis,
        _ => [],
    };
}
