// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Loadouts;
using Content.Shared.Stacks;
using Robust.Shared.Random;

namespace Content.Server._Orbitra.Loadouts;

public sealed partial class OrbitraRoundstartCashSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraRoundstartCashComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<OrbitraRoundstartCashComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Max < ent.Comp.Min)
            return;

        _stack.SetCount(ent.Owner, _random.Next(ent.Comp.Min, ent.Comp.Max + 1));
    }
}
