// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Client._Orbitra.Ghost.Skins;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Ghost.Widgets;

public sealed partial class GhostGui
{
    private OrbitraGhostSkinWindow? _orbitraSkinWindow;

    private void InitializeOrbitraSkinButton(WrapContainer flow)
    {
        var button = new OrbitraButton { Name = "OrbitraGhostSkinsButton", Text = Loc.GetString("orbitra-ghost-skins-button") };
        button.OnPressed += _ =>
        {
            _orbitraSkinWindow ??= new OrbitraGhostSkinWindow();
            if (_orbitraSkinWindow.IsOpen)
                OrbitraEntryWindow.RequestClose(_orbitraSkinWindow);
            else
                _orbitraSkinWindow.OpenCentered();
        };
        flow.AddChild(button);
    }

    private void CloseOrbitraSkinWindow() => _orbitraSkinWindow?.Close();

    private void DisposeOrbitraSkinWindow()
    {
        _orbitraSkinWindow?.Dispose();
        _orbitraSkinWindow = null;
    }
}
