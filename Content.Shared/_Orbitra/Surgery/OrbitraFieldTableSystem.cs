using Content.Shared.Foldable;
using Content.Shared.UserInterface;

namespace Content.Shared._Orbitra.Surgery;

public sealed partial class OrbitraFieldTableSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraSurgeryTableComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<OrbitraSurgeryTableComponent, FoldedEvent>(OnFolded);
    }

    private void OnOpenAttempt(Entity<OrbitraSurgeryTableComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (TryComp<FoldableComponent>(ent, out var foldable) && foldable.IsFolded)
            args.Cancel();
    }

    private void OnFolded(Entity<OrbitraSurgeryTableComponent> ent, ref FoldedEvent args)
    {
        if (args.IsFolded && _ui.IsUiOpen(ent.Owner, OrbitraSurgeryUiKey.Key))
            _ui.CloseUi(ent.Owner, OrbitraSurgeryUiKey.Key);
    }
}
