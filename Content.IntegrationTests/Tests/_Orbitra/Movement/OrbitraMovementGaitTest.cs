// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._Orbitra.Movement;

[TestFixture("MobHuman")]
[TestFixture("MobObserver")]
[TestFixture("AdminObserver")]
public sealed class OrbitraMovementGaitTest(string prototype) : MovementTest
{
    protected override string PlayerPrototype => prototype;
    protected override int Tiles => 10;

    [Test]
    public async Task ShiftSpeedsUpAndControlSlowsDown()
    {
        await SetPhysicalKey(Keyboard.Key.Shift, true);
        await RunTicks(12);
        Assert.That(await GetStaminaDamage(), Is.Zero);
        await SetPhysicalKey(Keyboard.Key.Shift, false);

        await SetPhysicalKey(Keyboard.Key.D, true);
        var normal = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Shift, true);
        var boosted = await GetRequestedSpeed();
        await RunTicks(12);
        var spent = await GetStaminaDamage();

        await SetPhysicalKey(Keyboard.Key.Control, true);
        var both = await GetRequestedSpeed();
        var afterSlowStep = await GetStaminaDamage();
        await RunTicks(12);
        Assert.That(await GetStaminaDamage(), Is.EqualTo(afterSlowStep));

        await SetPhysicalKey(Keyboard.Key.Shift, false);
        var slowed = await GetRequestedSpeed();

        await SetPhysicalKey(Keyboard.Key.Control, false);
        var released = await GetRequestedSpeed();

        if (prototype == "MobHuman")
        {
            var oldSprintSpeed = 0f;
            await Server.WaitPost(() => oldSprintSpeed = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer).CurrentSprintSpeed);
            Assert.That(boosted, Is.EqualTo(oldSprintSpeed * 1.3f).Within(0.01f));
            Assert.That(spent, Is.Positive);

            await Server.WaitPost(() =>
            {
                var stamina = SEntMan.GetComponent<StaminaComponent>(SPlayer);
                SEntMan.System<SharedStaminaSystem>().TakeStaminaDamage(SPlayer, stamina.CritThreshold * 0.55f - stamina.StaminaDamage, stamina, visual: false);
            });
            await SetPhysicalKey(Keyboard.Key.Shift, true);
            Assert.That(await GetRequestedSpeed(), Is.EqualTo(normal).Within(0.01f));
            await SetPhysicalKey(Keyboard.Key.Shift, false);
        }
        else
        {
            Assert.That(spent, Is.Zero);
        }

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

    private async Task<float> GetStaminaDamage()
    {
        var damage = 0f;
        await Server.WaitPost(() => damage = SEntMan.GetComponentOrNull<StaminaComponent>(SPlayer)?.StaminaDamage ?? 0f);
        return damage;
    }
}
