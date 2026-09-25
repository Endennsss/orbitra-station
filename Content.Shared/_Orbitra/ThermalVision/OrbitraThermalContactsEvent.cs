using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.ThermalVision;

/// <summary>Server-authorized sprite reference. Does not force the target into the client's PVS.</summary>
[Serializable, NetSerializable]
public readonly record struct OrbitraThermalContact(NetEntity Target);

/// <summary>Full replacement of thermal contacts, sent only to the authorized wearer's session.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraThermalContactsEvent(NetEntity device, NetEntity wearer, MapId map,
    OrbitraThermalContact[] contacts, bool active) : EntityEventArgs
{
    public readonly NetEntity Device = device;
    public readonly NetEntity Wearer = wearer;
    public readonly MapId Map = map;
    public readonly OrbitraThermalContact[] Contacts = contacts;
    public readonly bool Active = active;
}
