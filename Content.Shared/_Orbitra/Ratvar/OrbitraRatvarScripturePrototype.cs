using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Server-validated scripture costs and result, shared with the tablet catalogue.</summary>
[Prototype]
public sealed partial class OrbitraRatvarScripturePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField] public int Tier = 1;
    /// <summary>Catalogue group; does not grant permission to use the scripture.</summary>
    [DataField] public OrbitraRatvarScriptureCategory Category;
    [DataField] public int Energy;
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(3);
    /// <summary>Extra invocation time per existing bound shell of the same kind.</summary>
    [DataField] public TimeSpan DelayPerShell;
    [DataField] public EntProtoId? Result;
    [DataField] public bool Structure;
    [DataField] public bool Repair;
    [DataField] public int RepairAmount = 50;
    /// <summary>Число живых участников своего культа, включая читающего.</summary>
    [DataField] public int Invokers = 1;
    /// <summary>Радиус помощи при чтении писания.</summary>
    [DataField] public float InvokerRange = 1.5f;
    /// <summary>Minimum spacing between structures created by this scripture.</summary>
    [DataField] public float ExclusiveRange;
    /// <summary>Optional tablet empowerment instead of spawning an entity.</summary>
    [DataField] public OrbitraRatvarEmpowerment Empowerment;
    [DataField] public TimeSpan TargetWindow = TimeSpan.FromSeconds(8);
    [DataField] public TimeSpan TargetDelay;
    [DataField] public float TargetRange = 7f;
    [DataField] public TimeSpan EffectDuration = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan MuteDuration = TimeSpan.FromSeconds(12);
    [DataField] public float HealingFraction = 0.6f;
    [DataField] public float BacklashFraction = 0.5f;
    [DataField] public int BacklashCap = 80;
}

public enum OrbitraRatvarEmpowerment : byte { None, Kindle, Manacles, Compromise, Vanguard }

public enum OrbitraRatvarScriptureCategory : byte { Equipment, Structures, Spells, Power, Constructs, Traps }

[Serializable, NetSerializable]
public enum OrbitraRatvarUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class OrbitraRatvarScriptureMessage(string scripture) : BoundUserInterfaceMessage
{
    public readonly string Scripture = scripture;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCommunicateMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarUiState(int energy, int tier, int converts, bool busy,
    float incomeRate = 0, float expenseRate = 0) : BoundUserInterfaceState
{
    public Dictionary<string, string> Unavailable = [];
    public readonly int Energy = energy;
    public readonly int Tier = tier;
    public readonly int Converts = converts;
    public readonly bool Busy = busy;
    public readonly float IncomeRate = incomeRate;
    public readonly float ExpenseRate = expenseRate;
}
