using System.Numerics;

namespace Content.Client.MainMenu.UI;

public sealed partial class MainMenuControl
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Размер зависит только от экрана, а не от надписи или состояния кнопки.
        VBox.SetWidth = Math.Max(0, Math.Min(520, availableSize.X - 32));
        OrbitraMenuScroll.MaxHeight = Math.Max(0, availableSize.Y);
        VBox.SeparationOverride = availableSize.Y < 600 ? 8 : 12;
        OrbitraLogo.SetSize = new Vector2(availableSize.Y < 600 ? 48 : 80);
        return base.MeasureOverride(availableSize);
    }
}
