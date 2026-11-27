using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Снимает выбор врача при смене области, органа или отмене подтверждения.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraSurgeryDeselectMessage(NetEntity patient) : BoundUserInterfaceMessage
{
    public readonly NetEntity Patient = patient;
}
