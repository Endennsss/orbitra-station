using Content.Shared._Orbitra.Economy;
using Content.Shared.StationRecords;

namespace Content.Server._Orbitra.Economy;

/// <summary>Хранит баланс и последние операции текущей смены на сущности разума.</summary>
[RegisterComponent]
public sealed partial class OrbitraBankAccountComponent : Component
{
    public int Number;
    public int Balance;
    public int Salary;
    public TimeSpan NextPay;
    public EntityUid? Card;
    public StationRecordKey? StationRecord;
    public readonly Queue<OrbitraBankEntry> Entries = new();
}
