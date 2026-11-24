using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Economy;

[Serializable, NetSerializable]
public enum OrbitraAtmUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum OrbitraBankEntryKind : byte
{
    StartingBalance,
    Salary,
    Deposit,
    Withdrawal,
    Transfer,
    Purchase,
}

[Serializable, NetSerializable]
public readonly record struct OrbitraBankEntry(OrbitraBankEntryKind Kind, int Amount, int RelatedAccount = 0);

/// <summary>Личный снимок счёта отправляется только запрашивающему игроку.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraAtmSnapshotMessage(bool cardInserted, int account, int balance, OrbitraBankEntry[] entries) : BoundUserInterfaceMessage
{
    public bool CardInserted { get; } = cardInserted;
    public int Account { get; } = account;
    public int Balance { get; } = balance;
    public OrbitraBankEntry[] Entries { get; } = entries;
}

[Serializable, NetSerializable]
public sealed class OrbitraAtmRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class OrbitraAtmInsertCardMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class OrbitraAtmEjectCardMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class OrbitraAtmDepositMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class OrbitraAtmWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public int Amount { get; } = amount;
}

[Serializable, NetSerializable]
public sealed class OrbitraAtmTransferMessage(int recipient, int amount) : BoundUserInterfaceMessage
{
    public int Recipient { get; } = recipient;
    public int Amount { get; } = amount;
}
