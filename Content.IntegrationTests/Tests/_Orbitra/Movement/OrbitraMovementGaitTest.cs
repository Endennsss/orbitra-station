// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Movement.Components;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Orbitra.Movement;

[TestFixture("MobHuman")]
[TestFixture("MobObserver")]
[TestFixture("AdminObserver")]
public sealed class OrbitraMovementGaitTest(string prototype) : InteractionTest
{
    protected override string PlayerPrototype => prototype;

    [Test]
    public async Task ShiftSpeedsUpAndControlSlowsDown()
    {
        await SetPhysicalKey(Keyboard.Key.D, true);
        var normal = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Shift, true);
        var boosted = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Control, true);
        var both = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Shift, false);
        var slowed = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Control, false);
        var released = await GetRequestedSpeed();
        await SetPhysicalKey(Keyboard.Key.D, false);

        Assert.That(slowed, Is.GreaterThan(0f).And.LessThan(normal));
        Assert.That(boosted, Is.GreaterThan(normal));
        Assert.That(both, Is.EqualTo(slowed).Within(0.01f));
        Assert.That(released, Is.EqualTo(normal).Within(0.01f));
    }

    private async Task SetPhysicalKey(Keyboard.Key key, bool down)
    {
        await Client.WaitPost(() =>
        {
            var args = new KeyEventArgs(key, false, false, key == Keyboard.Key.Control && down, key == Keyboard.Key.Shift && down, false, 0);
            if (down)
                InputManager.KeyDown(args);
            else
                InputManager.KeyUp(args);
        });
        await RunTicks(8);
    }

    private async Task<float> GetRequestedSpeed()
    {
        var speed = 0f;
        await Server.WaitPost(() => speed = SEntMan.GetComponent<InputMoverComponent>(SPlayer).WishDir.Length());
        return speed;
    }
}
