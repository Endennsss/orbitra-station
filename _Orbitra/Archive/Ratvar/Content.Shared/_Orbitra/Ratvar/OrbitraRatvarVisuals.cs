using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarVisuals : byte
{
    Active,
    Ark,
    Defending,
    Powered,
    Prism,
    Lens,
    Enchanting,
    Trap,
}

[Serializable, NetSerializable]
public enum OrbitraRatvarMechanismLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum OrbitraRatvarMarauderLayers : byte
{
    Shield,
}
