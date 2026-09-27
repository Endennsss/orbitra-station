using Content.Shared.Mobs.Components;
using Content.Shared.Materials;
using Content.Server.Construction.Components;
using Content.Server.Cargo.Components;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarCrystalSystem
{
    // Временное снаряжение привязано к телу проекции, а не к членству в культе.
    [Dependency] private DestructibleSystem _destructible = default!;

    private void InitializeEquipment()
    {
        SubscribeLocalEvent<OrbitraRatvarProjectionItemComponent, EntityTerminatingEvent>(OnEquipmentTerminating);
    }

    private void OnEquipmentTerminating(Entity<OrbitraRatvarProjectionItemComponent> ent, ref EntityTerminatingEvent args)
    {
        EjectEquipmentContents(ent);
        if (TryComp<ActiveOrbitraRatvarProjectionComponent>(ent.Comp.Projection, out var projection))
            projection.Equipment.Remove(ent);
    }

    private void EquipProjection(Entity<ActiveOrbitraRatvarProjectionComponent> projection, OrbitraRatvarCrystalComponent crystal)
    {
        var coordinates = Transform(projection).Coordinates;
        foreach (var (slot, prototype) in crystal.Equipment)
        {
            var item = Spawn(prototype, coordinates);
            TrackEquipment(item, projection);
            if (!_inventory.TryEquip(projection, item, slot, silent: true))
                DissolveEquipment((item, Comp<OrbitraRatvarProjectionItemComponent>(item)));
        }

        foreach (var prototype in crystal.HandItems)
        {
            var item = Spawn(prototype, coordinates);
            TrackEquipment(item, projection);
            _hands.TryPickupAnyHand(projection, item);
        }
    }

    private void TrackEquipment(EntityUid item, Entity<ActiveOrbitraRatvarProjectionComponent> projection)
    {
        if (!projection.Comp.Equipment.Add(item)) return;
        var equipment = EnsureComp<OrbitraRatvarProjectionItemComponent>(item);
        equipment.Projection = projection;
        if (equipment.BreakDamage is { } damage)
        {
            // YAML объединяет поведения у одинаковых порогов; заменяем компонент через штатный API.
            RemComp<DestructibleComponent>(item);
            _destructible.AddThreshold((item, null), new DamageThreshold
            {
                Trigger = new DamageTrigger { Damage = damage },
                Behaviors = [new DoActsBehavior { Acts = ThresholdActs.Destruction }],
            }, null);
        }
        // Проекции нельзя разобрать или переработать в постоянные материалы.
        RemComp<ConstructionComponent>(item);
        RemComp<PhysicalCompositionComponent>(item);
        RemComp<StaticPriceComponent>(item);
        if (!TryComp<ContainerManagerComponent>(item, out var containers)) return;
        foreach (var container in _container.GetAllContainers(item, containers))
            foreach (var child in container.ContainedEntities)
                TrackEquipment(child, projection);
    }

    private void UpdateEquipment()
    {
        // Только выданное проекциям снаряжение; перенос сумки тоже меняет владельца содержимого.
        var query = EntityQueryEnumerator<OrbitraRatvarProjectionItemComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var equipment, out var xform))
        {
            if (equipment.Dissolving || TerminatingOrDeleted(uid)) continue;
            if (!TryComp<ActiveOrbitraRatvarProjectionComponent>(equipment.Projection, out var projection) || projection.Ending)
            {
                DissolveEquipment((uid, equipment));
                continue;
            }

            var parent = xform.ParentUid;
            while (TryComp(parent, out TransformComponent? parentTransform))
            {
                if (HasComp<MobStateComponent>(parent))
                {
                    if (parent != equipment.Projection) DissolveEquipment((uid, equipment));
                    break;
                }
                parent = parentTransform.ParentUid;
            }
        }
    }

    private void DissolveEquipment(Entity<OrbitraRatvarProjectionItemComponent> ent)
    {
        if (ent.Comp.Dissolving || TerminatingOrDeleted(ent)) return;
        ent.Comp.Dissolving = true;
        EjectEquipmentContents(ent);
        QueueDel(ent);
    }

    private void EjectEquipmentContents(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containers)) return;
        // Координаты грида, не удаляемого предмета или тела: иначе реальные вещи удалятся вместе с родителем.
        var destination = _transform.GetMoverCoordinates(uid);
        if (TerminatingOrDeleted(destination.EntityId)) return;
        foreach (var container in _container.GetAllContainers(uid, containers))
            _container.EmptyContainer(container, force: true, destination: destination);
    }
}
