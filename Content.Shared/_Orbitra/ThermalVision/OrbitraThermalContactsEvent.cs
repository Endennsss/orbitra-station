using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.ThermalVision;

/// <summary>Ссылка на разрешённую сервером цель, которая не расширяет PVS клиента.</summary>
[Serializable, NetSerializable]
public readonly record struct OrbitraThermalContact(NetEntity Target);

/// <summary>Список тепловых целей или продление его срока для сессии владельца очков.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraThermalContactsEvent(NetEntity device, NetEntity wearer, MapId map,
    OrbitraThermalContact[] contacts, bool active, bool keepAlive = false) : EntityEventArgs
{
    public readonly NetEntity Device = device;
    public readonly NetEntity Wearer = wearer;
    public readonly MapId Map = map;
    public readonly OrbitraThermalContact[] Contacts = contacts;
    public readonly bool Active = active;
    public readonly bool KeepAlive = keepAlive;
}
