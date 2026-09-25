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
    [DataField] public int Energy;
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(3);
    [DataField] public EntProtoId? Result;
    [DataField] public bool Structure;
    [DataField] public bool Repair;
    [DataField] public int RepairAmount = 50;
}

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
public sealed class OrbitraRatvarUiState(int energy, int tier, int converts, bool busy) : BoundUserInterfaceState
{
    public Dictionary<string, string> Unavailable = [];
    public readonly int Energy = energy;
    public readonly int Tier = tier;
    public readonly int Converts = converts;
    public readonly bool Busy = busy;
}
