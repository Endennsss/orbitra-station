using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarVisuals : byte
{
    Active,
    Ark,
    Defending,
}

[Serializable, NetSerializable]
public enum OrbitraRatvarMarauderLayers : byte
{
    Shield,
}
