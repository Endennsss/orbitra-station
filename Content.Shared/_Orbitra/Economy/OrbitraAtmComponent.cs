using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Economy;

/// <summary>Помечает устройство, через которое игрок управляет личным счётом.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraAtmComponent : Component
{
    public const string CardSlot = "bank_id";
}
