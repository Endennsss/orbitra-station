using Content.Shared._Orbitra.Surgery;

namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    private void ApplyOrbitraTraumaDelay(DoAfterArgs args)
    {
        if (!args.NeedHand)
            return;

        var delay = new OrbitraHandActionDelayEvent(1f);
        RaiseLocalEvent(args.User, ref delay);
        args.Delay *= delay.Multiplier;
    }
}
