namespace Content.Client._Orbitra.UserInterface;

/// <summary>Общие логические размеры интерфейса. Предпочтительные размеры задаёт каждое окно.</summary>
public static class OrbitraUiMetrics
{
    public const int Small = 8;
    public const int Medium = 12;
    public const int Large = 16;
    public const int Section = 24;
    public const int ElementHeight = 36;
    public const int ActionHeight = 44;
    public const int HeaderHeight = 36;
    public const int CloseSize = 32;
    public const int IconSize = 16;
    public const int WindowPadding = 16;
    public const int ScreenMargin = 16;
    public const int FormBreakpoint = 560;
    public const int LabelWidth = 176;
    public const float FormWidth = 720;
}

/// <summary>Смысловые роли кнопок. Старые имена стилей определены в общей таблице стилей как псевдонимы.</summary>
public static class OrbitraButtonStyles
{
    public const string Primary = "OrbitraButtonPrimary";
    public const string Secondary = "OrbitraButtonSecondary";
    public const string Ghost = "OrbitraButtonGhost";
    public const string Danger = "OrbitraButtonDanger";
}
