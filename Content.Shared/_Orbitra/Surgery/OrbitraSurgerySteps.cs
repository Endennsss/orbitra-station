namespace Content.Shared._Orbitra.Surgery;

/// <summary>Последовательности игровых инструментов общие для подсказок окна и проверки на сервере.</summary>
public static class OrbitraSurgerySteps
{
    private static readonly string[] Fracture = ["Retractor", "OrbitraBoneSetter", "Cautery"];
    private static readonly string[] InternalBleeding = ["Scalpel", "Hemostat", "Cautery"];
    private static readonly string[] Shrapnel = ["Retractor", "Hemostat", "Cautery"];
    private static readonly string[] Amputate = ["Scalpel", "Saw", "Cautery"];
    private static readonly string[] Reattach = ["Hemostat", "DetachedBody"];

    public static string[] Get(SurgeryProcedure procedure) => procedure switch
    {
        SurgeryProcedure.Fracture => Fracture,
        SurgeryProcedure.InternalBleeding => InternalBleeding,
        SurgeryProcedure.Shrapnel => Shrapnel,
        SurgeryProcedure.Amputate => Amputate,
        SurgeryProcedure.Reattach => Reattach,
        _ => [],
    };
}
