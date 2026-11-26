using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._Orbitra.Surgery;
using Content.Server.GameTicking;
using Content.Server.Lathe;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Buckle;
using Content.Shared.Foldable;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryDevTest
{
    [Test]
    public async Task FieldTableTreatsWoundsAndScannerPrintsProjectile()
    {
        var surgery = Server.System<OrbitraSurgerySystem>();
        var fold = Server.System<FoldableSystem>();
        var buckle = Server.System<SharedBuckleSystem>();
        var hands = Server.System<SharedHandsSystem>();
        var ui = Server.System<SharedUserInterfaceSystem>();
        var tableKey = OrbitraSurgeryUiKey.Key;
        var scannerKey = ForensicScannerUiKey.Key;
        EntityUid surgeon = default, patient = default, table = default, scanner = default, fragment = default;
        NetEntity netScanner = default;
        EntityCoordinates coords = default;
        SurgeryZone wound = default;
        string projectileName = string.Empty;
        var range = await Pair.CreateTestMap();
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var station = Server.System<ServerStationSystem>().GetStations().Single();
            Server.System<ServerGameTicker>().MakeJoinGame(ServerSession!, station, "Paramedic", silent: true);
            surgeon = ServerSession!.AttachedEntity!.Value;
            coords = SComp<TransformComponent>(StationTable()).Coordinates.Offset(new Vector2(0, -1));
            patient = SSpawnAtPosition("MobHuman", range.GridCoords);
            SComp<OrbitraSurgicalPatientComponent>(patient).TraumaProfile = "OrbitraTestSevereTrauma";
            var bullet = SSpawnAtPosition("BulletMagnum", range.GridCoords.Offset(new Vector2(-0.8f, 0)));
            projectileName = SComp<MetaDataComponent>(bullet).EntityName;
            Server.System<SharedGunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, gunUid: null, speed: 6f);
        });
        await Pair.RunTicksSync((int) Math.Ceiling(0.4 * SGameTiming.TickRate));
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            transform.SetCoordinates(patient, coords);
            transform.SetCoordinates(surgeon, coords);
            var supplies = coords.Offset(new Vector2(3, 0));
            var fab = SSpawnAtPosition("MedicalTechFab", supplies);
            Assert.That(Server.System<LatheSystem>().GetAvailableRecipes(fab, SComp<LatheComponent>(fab)),
                Does.Contain(new ProtoId<LatheRecipePrototype>("OrbitraFieldOperatingTable")));
            var locker = SSpawnAtPosition("LockerParamedicFilled", supplies);
            var containers = Server.System<SharedContainerSystem>();
            var storage = containers.GetContainer(locker, "entity_storage");
            table = storage.ContainedEntities.Single(SEntMan.HasComponent<OrbitraSurgeryTableComponent>);
            Assert.That(containers.Remove(table, storage, destination: coords), Is.True);
            Assert.That(buckle.TryBuckle(patient, surgeon, table), Is.False);
            Assert.That(fold.TryToggleFold(table, SComp<FoldableComponent>(table), surgeon), Is.True);
            Assert.That(buckle.TryBuckle(patient, surgeon, table), Is.True);
            Assert.That(fold.TryToggleFold(table, SComp<FoldableComponent>(table), surgeon), Is.False);
            Assert.That(ui.TryOpenUi(table, tableKey, surgeon), Is.True);
            var trauma = SComp<OrbitraTraumaComponent>(patient);
            wound = trauma.FragmentSources.Single().Key;
            Assert.That(trauma.FragmentSources[wound].Id, Is.EqualTo("BulletMagnum"));
            trauma.Injuries[SurgeryZone.LeftLeg] = trauma.Injuries.GetValueOrDefault(SurgeryZone.LeftLeg) | SurgeryInjury.Fracture;
            trauma.Injuries[SurgeryZone.Torso] = trauma.Injuries.GetValueOrDefault(SurgeryZone.Torso) | SurgeryInjury.InternalBleeding;
            Equip("Retractor");
            Assert.That(surgery.TryStartStep(table, surgeon, patient, SurgeryZone.LeftLeg, SurgeryProcedure.Fracture, 0, true), Is.False);
            Assert.That(surgery.TryStartStep(table, surgeon, patient, wound, SurgeryProcedure.Shrapnel, 0, false), Is.True);
            Assert.That(SComp<OrbitraSurgeryOperationComponent>(patient).Duration.TotalSeconds, Is.EqualTo(4.5).Within(0.01));
        });

        await Pair.RunTicksSync(5 * SGameTiming.TickRate);
        await Server.WaitAssertion(() =>
        {
            Server.System<TagSystem>().AddTag(surgeon, new ProtoId<TagPrototype>("InstantDoAfters"));
            for (var step = 1; step < 3; step++)
            {
                Equip(OrbitraSurgerySteps.Get(SurgeryProcedure.Shrapnel)[step]);
                Assert.That(surgery.TryStartStep(table, surgeon, patient, wound, SurgeryProcedure.Shrapnel, step, false), Is.True);
            }
            for (var step = 0; step < 3; step++)
            {
                Equip(OrbitraSurgerySteps.Get(SurgeryProcedure.InternalBleeding)[step]);
                Assert.That(surgery.TryStartStep(table, surgeon, patient, SurgeryZone.Torso, SurgeryProcedure.InternalBleeding, step, false), Is.True);
            }
            Assert.That(SComp<OrbitraTraumaComponent>(patient).FragmentSources, Is.Empty);
            var objects = SEntMan.AllEntityQueryEnumerator<OrbitraFragmentEvidenceComponent>();
            Assert.That(objects.MoveNext(out fragment, out var evidence), Is.True);
            Assert.That(evidence!.Projectile?.Id, Is.EqualTo("BulletMagnum"));
            Assert.That(objects.MoveNext(out _, out _), Is.False);
            buckle.Unbuckle(patient, surgeon);
            Assert.That(fold.TryToggleFold(table, SComp<FoldableComponent>(table), surgeon), Is.True);
            scanner = Equip("ForensicScanner");
            netScanner = SEntMan.GetNetEntity(scanner);
            Server.System<SharedInteractionSystem>().InteractUsing(surgeon, scanner, fragment, coords);
            Assert.That(SComp<ForensicScannerComponent>(scanner).OrbitraProjectile, Is.EqualTo("BulletMagnum"));
            Assert.That(ui.IsUiOpen(scanner, scannerKey, surgeon), Is.True);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitPost(() => Client.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
            CEntMan.GetEntity(netScanner), scannerKey, new ForensicScannerPrintMessage()));
        await Pair.RunTicksSync(8);
        await Server.WaitAssertion(() =>
        {
            var reports = SEntMan.AllEntityQueryEnumerator<PaperComponent, MetaDataComponent>();
            var printed = false;
            while (reports.MoveNext(out _, out var report, out var meta))
            {
                if (meta.EntityPrototype?.ID == "ForensicReportPaper" && report.Content.Contains(projectileName))
                    printed = true;
            }
            Assert.That(printed, Is.True);
            var oldFragment = SSpawnAtPosition("OrbitraExtractedFragment", coords);
            Server.System<SharedInteractionSystem>().InteractUsing(surgeon, scanner, oldFragment, coords);
            Assert.That(SComp<ForensicScannerComponent>(scanner).OrbitraProjectile, Is.EqualTo(string.Empty));
        });

        EntityUid Equip(string prototype)
        {
            if (hands.GetActiveItem(surgeon) is { } held)
                Assert.That(hands.TryDrop(surgeon, held), Is.True);
            var item = SSpawnAtPosition(prototype, coords);
            Assert.That(hands.TryPickup(surgeon, item), Is.True);
            return item;
        }
    }
}
