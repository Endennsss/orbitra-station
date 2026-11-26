using Content.Shared._Orbitra.Economy;

namespace Content.Client._Orbitra.Economy;

internal static class OrbitraBankUi
{
    public static string FormatEntry(OrbitraBankEntry entry)
    {
        var key = entry.Kind switch
        {
            OrbitraBankEntryKind.StartingBalance => "orbitra-atm-entry-start",
            OrbitraBankEntryKind.Salary => "orbitra-atm-entry-salary",
            OrbitraBankEntryKind.Deposit => "orbitra-atm-entry-deposit",
            OrbitraBankEntryKind.Withdrawal => "orbitra-atm-entry-withdraw",
            OrbitraBankEntryKind.Transfer => "orbitra-atm-entry-transfer",
            OrbitraBankEntryKind.Purchase => "orbitra-atm-entry-purchase",
            _ => "orbitra-atm-entry-unknown",
        };
        var amount = entry.Amount.ToString("+#;-#;0");
        return entry.Kind == OrbitraBankEntryKind.Transfer
            ? Loc.GetString(key, ("amount", amount), ("account", entry.RelatedAccount))
            : Loc.GetString(key, ("amount", amount));
    }
}
