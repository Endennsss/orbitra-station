using Content.Server._Orbitra.Economy;

namespace Content.Server.PDA;

public sealed partial class PdaSystem
{
    [Dependency] private OrbitraBankPdaSystem _bankPda = default!;
}
