// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Xenobiology;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Xenobiology;

/// <summary>A stable colony list entry reused when measurements change.</summary>
internal sealed class OrbitraXenobiologySpecimenButton : OrbitraContainerButton
{
    private readonly TextureRect _icon = new() { SetSize = new Vector2(40), Stretch = TextureRect.StretchMode.KeepAspectCentered };
    private readonly Label _name = new() { ClipText = true };
    private readonly Label _status = new() { ClipText = true, StyleClasses = { StyleClass.FontSmall } };
    private readonly ProgressBar _growth = new() { MinValue = 0, MaxValue = 1, MinHeight = 5 };

    public OrbitraXenobiologySpecimenButton()
    {
        HorizontalExpand = true;
        ToggleMode = true;
        AddStyleClass(ContainerButton.StyleClassButton);
        AddStyleClass(OrbitraButtonStyles.Secondary);
        OrbitraXenobiologyPresentation.StyleMeter(_growth);
        OrbitraMotion.AttachButton(this);
        var row = new BoxContainer { Margin = new Thickness(8, 6), SeparationOverride = 8 };
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 3 };
        row.AddChild(_icon);
        text.AddChild(_name);
        text.AddChild(_status);
        text.AddChild(_growth);
        row.AddChild(text);
        AddChild(row);
    }

    public void Update(OrbitraXenobiologySpecimen specimen, OrbitraXenobiologyBreedPrototype breed,
        ILocalizationManager loc, SpriteSystem sprite)
    {
        _name.Text = OrbitraXenobiologyPresentation.Name(specimen, loc.GetString(breed.Name), loc);
        _name.ToolTip = _name.Text;
        _status.Text = loc.GetString("orbitra-xeno-list-status", ("status", OrbitraXenobiologyPresentation.Status(specimen, loc)),
            ("generation", specimen.Generation));
        _status.FontColorOverride = OrbitraXenobiologyPresentation.StatusColor(specimen);
        _growth.Visible = !specimen.Adult && !specimen.Dead;
        _growth.Value = OrbitraXenobiologyPresentation.Unit(specimen.Growth);
        OrbitraXenobiologyPresentation.SetIcon(_icon, sprite, breed, specimen.Adult, specimen.Dead);
    }
}
