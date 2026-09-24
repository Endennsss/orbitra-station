using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Strip;

public sealed partial class StrippingMenu
{
    private bool _orbitraSized;

    private void InitializeOrbitraStripping(BoxContainer contents)
    {
        contents.Orphan();
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(contents);
        ContentsContainer.AddChild(scroll);
        MinSize = new Vector2(280, 240);
        Resizable = true;
        OrbitraEntryWindow.Attach(this);
    }

    public void ApplyOrbitraLayout(Vector2 preferredSize)
    {
        MeasureOrbitraSlots(HandsContainer);
        MeasureOrbitraSlots(InventoryContainer);
        // Кнопки пересоздаются штатным BUI только при изменении содержимого.
        foreach (var button in ButtonContainer.Children)
            OrbitraEditorStyles.Apply(button);
        if (!_orbitraSized)
        {
            SetSize = new Vector2(Math.Max(320, preferredSize.X + 12), Math.Min(640, preferredSize.Y + 40));
            _orbitraSized = true;
        }
        OrbitraEditorStyles.FitWindow(this);
    }

    private static void MeasureOrbitraSlots(LayoutContainer container)
    {
        var size = Vector2.Zero;
        // LayoutContainer сам не учитывает смещение слотов при вычислении высоты прокрутки.
        foreach (var slot in container.Children)
        {
            slot.Measure(new Vector2(float.PositiveInfinity));
            var position = new Vector2(slot.GetValue<float>(LayoutContainer.MarginLeftProperty),
                slot.GetValue<float>(LayoutContainer.MarginTopProperty));
            size = Vector2.Max(size, position + slot.DesiredSize);
        }
        container.MinSize = size;
    }
}
