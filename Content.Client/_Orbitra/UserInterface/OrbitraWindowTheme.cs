// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Common window appearance, independent of feature-specific layouts.</summary>
internal static class OrbitraWindowTheme
{
    internal const string EditorLayoutClass = "OrbitraEditorLayout";
    internal const string NativeClass = "OrbitraNativePresentation";

    internal static void Attach(BaseWindow window)
    {
        if (window is not (FancyWindow or DefaultWindow) || OrbitraControlTheme.IsNative(window))
            return;

        Apply(window);
        OrbitraEntryWindow.Attach(window, applyEditorLayout: window.HasStyleClass(EditorLayoutClass));
    }

    internal static void Apply(BaseWindow window)
    {
        if (OrbitraControlTheme.IsNative(window))
            return;
        if (window is FancyWindow fancy)
            fancy.ApplyOrbitraChrome();
        else if (window is DefaultWindow standard)
        {
            OrbitraDefaultWindowChrome.Apply(standard);
            OrbitraEngineMenus.ApplyLayout(standard);
        }
        OrbitraControlTheme.Apply(window);
    }
}
