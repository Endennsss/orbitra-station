using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Ratvar;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._Orbitra.Ratvar;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed partial class OrbitraRatvarEminenceTest
{
    [Test]
    public async Task ViewCameraFramingResetsWhenCleared()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        NetEntity avatar = default, target = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetNetEntity(SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value);
            target = SEntMan.GetNetEntity(_target);
            System.TrySelect(mind, _target);
        });
        await Pair.RunTicksSync(15);
        await Client.WaitPost(() =>
        {
            var eye = Client.System<Robust.Client.GameObjects.EyeSystem>();
            eye.SetZoom(CEntMan.GetEntity(target), new Vector2(0.75f));
            eye.SetRotation(CEntMan.GetEntity(target), Angle.FromDegrees(90));
            eye.SetOffset(CEntMan.GetEntity(target), new Vector2(0.2f, 0.3f));
            Client.System<OrbitraRatvarEminenceViewSystem>().FrameUpdate(0);
        });
        await Client.WaitAssertion(() =>
        {
            var eye = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(avatar));
            Assert.That(eye.Zoom, Is.EqualTo(new Vector2(0.75f)));
            Assert.That(eye.Rotation, Is.EqualTo(Angle.FromDegrees(90)));
            Assert.That(eye.Offset, Is.EqualTo(new Vector2(0.2f, 0.3f)));
        });
        await Server.WaitPost(() => System.ClearSelection((mind, SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind))));
        await Pair.RunTicksSync(15);
        await Client.WaitAssertion(() =>
        {
            var eye = CEntMan.GetComponent<EyeComponent>(CEntMan.GetEntity(avatar));
            Assert.That(eye.Zoom, Is.EqualTo(Vector2.One));
            Assert.That(eye.Rotation, Is.EqualTo(Angle.Zero));
            Assert.That(eye.Offset, Is.EqualTo(Vector2.Zero));
        });
    }

    [Test]
    public async Task ViewAvatarUsesCultChatWithoutLocalSpeech()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default;
        var chat = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            Server.System<ChatSystem>().TrySendInGameICMessage(avatar, "+р eminence-private-test", InGameICChatType.Speak, false);
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() => Assert.That(chat.History.Single(m => m.Msg.Message.Contains("eminence-private-test")).Msg.Channel,
            Is.EqualTo(ChatChannel.Radio)));
        await Server.WaitPost(() =>
        {
            Server.System<RoleSystem>().MindRemoveRole<OrbitraRatvarRoleComponent>(mind);
            Server.System<ChatSystem>().TrySendInGameICMessage(avatar, "+р eminence-revoked-test", InGameICChatType.Speak, false);
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() => Assert.That(chat.History.Any(m => m.Msg.Message.Contains("eminence-revoked-test")), Is.False));
    }

    [Test]
    public async Task ViewSwitchAndClearInSameTickPreserveOnlyCurrentSubscription()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default, replacement = default;
        bool first = false, second = false, repeated = false;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            first = System.TrySelect(mind, _target);
            replacement = SEntMan.SpawnEntity(Human, _origin);
            Server.System<MindSystem>().TransferTo(_targetMind, replacement);
            second = System.TrySelect(mind, replacement);
            System.ClearSelection((mind, SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind)));
            repeated = System.TrySelect(mind, replacement);
        });
        await Pair.RunTicksSync(15);
        await Server.WaitAssertion(() =>
        {
            Assert.That(first && second && repeated, Is.True);
            Assert.That(ServerSession!.ViewSubscriptions, Is.EquivalentTo(new[] { replacement }));
            Assert.That(SEntMan.HasComponent<OrbitraRatvarEminenceViewComponent>(avatar), Is.True);
            Assert.That(SEntMan.GetComponent<EyeComponent>(avatar).Target, Is.EqualTo(replacement));
            Assert.That(ServerSession.AttachedEntity, Is.EqualTo(avatar));
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).VisitingEntity, Is.Null);
        });
    }

    [Test]
    public async Task ViewMirrorsBlurAndKeepsNativeLightAndFov()
    {
        var mind = await AcquireInvitation(await PrepareInvitation());
        EntityUid avatar = default;
        await Server.WaitPost(() =>
        {
            avatar = SEntMan.GetComponent<MindComponent>(mind).OwnedEntity!.Value;
            Server.System<BlindableSystem>().SetMinDamage(_target, 2);
            System.TrySelect(mind, _target);
            Server.System<SharedEyeSystem>().SetDrawLight(avatar, false);
            Server.System<SharedEyeSystem>().SetDrawFov(avatar, false);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<BlurryVisionComponent>(avatar).Magnitude,
                Is.EqualTo(SEntMan.GetComponent<BlurryVisionComponent>(_target).Magnitude));
            var eye = SEntMan.GetComponent<EyeComponent>(avatar);
            Assert.That(eye.DrawFov && eye.DrawLight, Is.True);
        });
        await Server.WaitPost(() => System.ClearSelection((mind, SEntMan.GetComponent<OrbitraRatvarEminenceComponent>(mind))));
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<BlurryVisionComponent>(avatar), Is.False));
    }
}
