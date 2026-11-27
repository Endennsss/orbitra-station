using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Roadmap;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared._Orbitra.Roadmap;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra.Roadmap;

public sealed class OrbitraAtlasAccessTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    [Test]
    public async Task DraftPublicationRevocationAndLeavingLobby()
    {
        var atlas = Server.System<OrbitraAtlasSystem>();
        var admins = Server.ResolveDependency<IAdminManager>();
        var session = ServerSession!;
        var id = Guid.NewGuid();
        var draft = new AtlasContent("Шлюзы", "Описание", AtlasStatus.Active, [new("Анимация", AtlasStatus.Done)]);
        OrbitraAtlasEui view = null;
        AdminData admin = null;
        AdminFlags originalFlags = default;
        AtlasChange Request(AtlasAction action, int version, AtlasContent content = null) => new()
        {
            Id = id, RequestId = Guid.NewGuid(), Revision = version, Action = action, Content = content,
        };
        OrbitraRoadmapState State() => (OrbitraRoadmapState) view.GetNewState();
        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.ServerId, id.ToString());
            admin = admins.GetAdminData(session)!;
            originalFlags = admin.Flags;
            admin.Flags = AdminFlags.Host;
            view = atlas.TryOpen(session)!;
        });
        try
        {
            await Pair.RunTicksSync(5);
            await Server.WaitAssertion(() =>
            {
                Assert.That(State().CanEdit, Is.True);
                view.HandleMessage(Request(AtlasAction.Save, 0, draft));
                Assert.That(State().Entries.Single().Pending, Is.True);
                admin.Flags = AdminFlags.Admin;
                Assert.That(State().CanEdit, Is.False);
                Assert.That(State().Entries, Is.Empty);
                view.HandleMessage(Request(AtlasAction.Publish, 1, draft));
                Assert.That(State().Success, Is.False);
                admin.Flags = AdminFlags.RoadmapEdit;
                view.HandleMessage(Request(AtlasAction.Publish, 1, draft));
                Assert.That(State().Success, Is.True);
                view.HandleMessage(Request(AtlasAction.Save, 2, draft with { Title = "Ещё не объявлено" }));
                view.HandleMessage(Request(AtlasAction.Publish, 2, draft));
                Assert.That(State().Notice, Is.EqualTo("orbitra-atlas-conflict"));
                Assert.That(State().Entries.Single().Content.Title, Is.EqualTo("Ещё не объявлено"));
            });
            await Server.WaitPost(() => admins.DeAdmin(session));
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(State().Entries.Single().Content.Title, Is.EqualTo("Шлюзы"));
                Assert.That(State().CanEdit, Is.False);
                admins.ReAdmin(session);
                view.HandleMessage(Request(AtlasAction.Unpublish, 3));
                Assert.That(State().Entries.Single().Published, Is.False);
                Server.System<ServerGameTicker>().PlayerJoinGame(session, silent: true);
                Assert.That(view.IsShutDown, Is.True);
                Assert.That(atlas.TryOpen(session), Is.Null);
            });
        }
        finally
        {
            await Server.WaitPost(() => admin.Flags = originalFlags);
        }
    }
}
