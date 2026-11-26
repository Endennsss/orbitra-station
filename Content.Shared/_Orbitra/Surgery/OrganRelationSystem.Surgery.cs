namespace Content.Shared.Body;

public sealed partial class OrganRelationSystem
{
    /// <summary>Проверяет прямое крепление без обхода вложенных органов.</summary>
    public bool HasDirectChild(Entity<ParentOrganComponent> parent, EntityUid child) =>
        parent.Comp.Children.Contains(child);
}
