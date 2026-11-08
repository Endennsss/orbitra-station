// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Installs the default Orbitra theme before ordinary windows are first shown.</summary>
public sealed class OrbitraWindowThemeController : UIController
{
    private bool _attached;

    public override void Initialize()
    {
        base.Initialize();
        Ensure();
    }

    internal void Ensure()
    {
        if (_attached)
            return;
        _attached = true;
        // Контроллер живёт столько же, сколько менеджер UI; покадровый обход не нужен.
        UIManager.WindowRoot.OnChildAdded += OnWindowAdded;
        foreach (var child in UIManager.WindowRoot.Children.ToArray())
            OnWindowAdded(child);
    }

    private static void OnWindowAdded(Control control)
    {
        if (control is BaseWindow window)
            OrbitraWindowTheme.Attach(window);
    }
}
