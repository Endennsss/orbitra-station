using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Lobby;
using Content.Client.RoundEnd;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Client._Orbitra.UserInterface;
using Content.IntegrationTests.Fixtures;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraRoundMenusTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true, NoLoadTestPrototypes = true };

    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child))
            yield return nested;
    }

    [Test]
    public async Task CyrillicNameSurvivesRepeatedClientAndServerSaves()
    {
        var preferences = Client.Resolve<IClientPreferencesManager>();
        await Client.WaitPost(() => Client.Resolve<IConfigurationManager>().SetCVar(CCVars.RestrictedNames, true));
        await Server.WaitPost(() => Server.Resolve<IConfigurationManager>().SetCVar(CCVars.RestrictedNames, true));
        foreach (var name in new[] { "Дуглас Лесли", "Ёлка Соловьёва", "Алио-Тейл", "John Smith", "Дуглас Лесли" })
        {
            await Client.WaitPost(() => preferences.UpdateCharacter(preferences.Preferences.SelectedCharacter.WithName(name),
                preferences.Preferences.SelectedCharacterIndex));
            await Pair.RunTicksSync(10);
            await Client.WaitAssertion(() => Assert.That(preferences.Preferences.SelectedCharacter.Name, Is.EqualTo(name)));
            await Server.WaitAssertion(() => Assert.That(Server.Resolve<IServerPreferencesManager>()
                .GetPreferences(ServerSession!.UserId).SelectedCharacter.Name, Is.EqualTo(name)));
        }
    }

    [Test]
    public async Task ManifestWrapsColumnsAndGhostRulesKeepTextWithoutTimer()
    {
        RoundEndSummaryWindow summary = null!;
        GhostRolesWindow roles = null!;
        GhostRoleRulesWindow rules = null!;
        await Server.WaitPost(() => Server.Resolve<IConfigurationManager>().SetCVar(CCVars.GhostRoleTime, 0f));
        await Pair.RunTicksSync(5);
        await Client.WaitPost(() =>
        {
            summary = new RoundEndSummaryWindow("Secret", "Round result", TimeSpan.FromMinutes(20), 1,
            [new RoundEndMessageEvent.RoundEndPlayerInfo
            {
                PlayerOOCName = "LongAccountName",
                PlayerICName = "Длинное имя персонажа для проверки переноса",
                Role = "job-name-captain",
                Antag = true,
            }]);
            All(summary).OfType<TabContainer>().Single().CurrentTab = 1;
            roles = new GhostRolesWindow();
            roles.OpenCentered();
            roles.AddEntry("Мышь", "Длинное описание роли с переносом текста внутри карточки.", true, null,
                [new GhostRoleInfo { Identifier = 1, Kind = GhostRoleKind.FirstComeFirstServe },
                 new GhostRoleInfo { Identifier = 2, Kind = GhostRoleKind.FirstComeFirstServe }], CEntMan.System<SpriteSystem>());
            rules = new GhostRoleRulesWindow("Rules remain visible", _ => {});
            rules.OpenCentered();
        });
        try
        {
            foreach (var width in new[] { 900f, 600f, 420f })
            {
                await Client.WaitPost(() => summary.SetSize = new Vector2(width, 500));
                await Pair.RunTicksSync(3);
                await Client.WaitAssertion(() =>
                {
                    Assert.That(summary.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(roles.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(rules.HasStyleClass("OrbitraEntryWindow"), Is.True);
                    Assert.That(All(roles).OfType<CollapsibleHeading>().Single().HasStyleClass("OrbitraLobbyButton"), Is.True);
                    Assert.That(All(roles).OfType<PanelContainer>().Count(p => p.HasStyleClass("OrbitraRoleCard")), Is.EqualTo(1));
                    var rows = All(summary).OfType<OrbitraManifestRow>().ToArray();
                    Assert.That(rows.Length, Is.EqualTo(2));
                    Assert.That(rows[0].ChildCount, Is.EqualTo(4));
                    Assert.That(rows[1].ChildCount, Is.EqualTo(4));
                    for (var i = 0; i < 4; i++)
                    {
                        Assert.That(rows[1].GetChild(i).Width, Is.EqualTo(rows[0].GetChild(i).Width).Within(1),
                            $"Column {i}; header {rows[0].Size}, body {rows[1].Size}; header parent {rows[0].Parent!.Size}, body parent {rows[1].Parent!.Size}; header child {rows[0].GetChild(i).DesiredSize}, body child {rows[1].GetChild(i).DesiredSize}");
                        Assert.That(rows[1].GetChild(i).Position.X + rows[1].GetChild(i).Width, Is.LessThanOrEqualTo(rows[1].Width + 1));
                    }
                    Assert.That(All(rules).OfType<RichTextLabel>().Single().Height, Is.GreaterThan(0));
                    Assert.That(All(rules).OfType<Button>().Single(b => b.Name == "RequestButton").Disabled, Is.False);
                });
            }
        }
        finally
        {
            await Client.WaitPost(() => { summary.Dispose(); roles.Dispose(); rules.Dispose(); });
        }
    }
}
