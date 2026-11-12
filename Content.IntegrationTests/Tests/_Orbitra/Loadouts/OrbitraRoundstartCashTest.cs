// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Loadouts;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Content.Shared.Roles;
using Content.Shared.Stacks;

namespace Content.IntegrationTests.Tests._Orbitra.Loadouts;

[TestFixture, NonParallelizable]
public sealed class OrbitraRoundstartCashTest : GameTest
{
    private const string CashPrototype = "OrbitraRoundstartCash";
    private const string CashLoadout = "OrbitraLoadoutRoundstartCash";
    private const string TrinketsGroup = "Trinkets";

    [Test]
    public async Task CashStackRollsWithinRange()
    {
        var spawned = new List<(bool HasComponent, int Count)>();
        await Server.WaitPost(() =>
        {
            for (var i = 0; i < 16; i++)
            {
                var uid = SSpawn(CashPrototype);
                spawned.Add((SEntMan.HasComponent<OrbitraRoundstartCashComponent>(uid),
                    SComp<StackComponent>(uid).Count));
            }
        });

        Assert.That(spawned, Has.Count.EqualTo(16));
        Assert.That(spawned.Select(entry => entry.HasComponent), Is.All.True);
        Assert.That(spawned.Select(entry => entry.Count), Is.All.InRange(100, 4000));
    }

    [Test]
    public async Task TrinketsOfferPlaytimeGatedCash()
    {
        await Server.WaitAssertion(() =>
        {
            var group = SProtoMan.Index<LoadoutGroupPrototype>(TrinketsGroup);
            Assert.That(group.Loadouts, Does.Contain(CashLoadout));

            var loadout = SProtoMan.Index<LoadoutPrototype>(CashLoadout);
            Assert.That(loadout.Storage["back"], Does.Contain(CashPrototype));

            var effect = loadout.Effects.OfType<JobRequirementLoadoutEffect>().Single();
            var requirement = effect.Requirement as OverallPlaytimeRequirement;
            Assert.That(requirement, Is.Not.Null);
            Assert.That(requirement!.Time, Is.EqualTo(TimeSpan.FromHours(100)));
        });
    }
}
