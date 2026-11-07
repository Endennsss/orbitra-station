// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Xenobiology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Xenobiology;

[UsedImplicitly]
public sealed class OrbitraXenobiologyProcessorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraXenobiologyProcessorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraXenobiologyProcessorWindow>();
        _window.ProcessRequested += () => SendMessage(new OrbitraXenobiologyProcessMessage());
        _window.EjectRequested += () => SendMessage(new OrbitraXenobiologyEjectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is OrbitraXenobiologyProcessorBoundUserInterfaceState processor)
            _window?.UpdateState(processor);
    }
}
