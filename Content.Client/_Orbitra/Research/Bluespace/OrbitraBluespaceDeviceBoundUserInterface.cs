// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Shared._Orbitra.Research.Bluespace;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Orbitra.Research.Bluespace;

[UsedImplicitly]
public sealed class OrbitraBluespaceDeviceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraBluespaceDeviceWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraBluespaceDeviceWindow>();
        _window.EnabledRequested += enabled => SendMessage(new OrbitraBluespaceSetEnabledMessage(enabled));
        _window.PowerModeRequested += mode => SendMessage(new OrbitraBluespaceSetPowerModeMessage(mode));
        _window.LootCategoryRequested += category => SendMessage(new OrbitraBluespaceSetLootCategoryMessage(category));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is OrbitraBluespaceDeviceBoundUserInterfaceState device)
            _window?.UpdateState(device);
    }
}
