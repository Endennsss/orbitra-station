// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Shared appearance for ordinary controls; leaves their layout constraints intact.</summary>
internal static class OrbitraControlTheme
{
    internal const string AppliedClass = "OrbitraBaseThemed";
    internal const string ButtonClass = "OrbitraBaseButton";
    internal const string FixedHeightClass = "OrbitraFixedHeight";

    internal static bool IsNative(Control control)
    {
        for (Control? current = control; current != null; current = current.Parent)
            if (current.HasStyleClass(OrbitraWindowTheme.NativeClass))
                return true;
        return false;
    }

    internal static void Apply(Control control)
    {
        if (IsNative(control) || control.HasStyleClass(AppliedClass))
            return;
        control.AddStyleClass(AppliedClass);
        // Существующие sheetlet остаются единственным источником цветов и состояния элементов.
        control.AddStyleClass("OrbitraEditorControl");
        if (control is LineEdit or Slider)
        {
            control.RemoveStyleClass("actionSearchBox");
            OrbitraFocusRing.Attach(control);
        }
        if (control is Button or OptionButton && control is not CheckBox && control is not OrbitraWindowCloseButton)
        {
            control.AddStyleClass(ButtonClass);
            if (float.IsFinite(control.SetHeight))
                control.AddStyleClass(FixedHeightClass);
        }
        if (control is Button button)
            OrbitraMotion.AttachButton(button);
        if (control is OptionButton option)
        {
            OrbitraMotion.AttachButton(option);
            OrbitraOptionPopup.Attach(option);
        }
        if (control is CheckBox checkbox)
            checkbox.TextureRect.AddStyleClass("OrbitraCheckIcon");
        if (control is TabContainer tabs)
            OrbitraMotion.AttachTabs(tabs);
        OrbitraTooltips.Attach(control);
        foreach (var child in control.Children)
            Apply(child);
        control.OnChildAdded += Apply;
    }
}
