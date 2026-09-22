namespace Content.Client._Orbitra.UserInterface;

/// <summary>Shared logical UI dimensions; window-specific preferred sizes remain local.</summary>
public static class OrbitraUiMetrics
{
    public const int Small = 8;
    public const int Medium = 12;
    public const int Large = 16;
    public const int Section = 24;
    public const int ElementHeight = 36;
    public const int ActionHeight = 44;
    public const int HeaderHeight = 44;
    public const int CloseSize = 32;
    public const int WindowPadding = 16;
    public const int ScreenMargin = 16;
    public const int FormBreakpoint = 560;
    public const int LabelWidth = 176;
    public const float FormWidth = 720;
}

/// <summary>Semantic button roles. Legacy style names are aliases in the common sheetlet.</summary>
public static class OrbitraButtonStyles
{
    public const string Primary = "OrbitraButtonPrimary";
    public const string Secondary = "OrbitraButtonSecondary";
    public const string Ghost = "OrbitraButtonGhost";
    public const string Danger = "OrbitraButtonDanger";
}
