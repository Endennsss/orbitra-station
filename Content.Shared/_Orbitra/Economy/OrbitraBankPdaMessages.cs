using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Economy;

[Serializable, NetSerializable]
public sealed class OrbitraBankPdaState(bool cardInserted, int account, int balance, OrbitraBankEntry[] entries)
    : BoundUserInterfaceState
{
    public bool CardInserted { get; } = cardInserted;
    public int Account { get; } = account;
    public int Balance { get; } = balance;
    public OrbitraBankEntry[] Entries { get; } = entries;
}

// Данные счёта передаются только адресату, вне общего состояния КПК.
[Serializable, NetSerializable]
public sealed class OrbitraBankPdaSnapshotMessage(OrbitraBankPdaState state) : BoundUserInterfaceMessage
{
    public OrbitraBankPdaState State { get; } = state;
}

[Serializable, NetSerializable]
public sealed class OrbitraBankPdaRefreshMessage : CartridgeMessageEvent;

[Serializable, NetSerializable]
public sealed class OrbitraBankPdaTransferMessage(int recipient, int amount) : CartridgeMessageEvent
{
    public int Recipient { get; } = recipient;
    public int Amount { get; } = amount;
}
