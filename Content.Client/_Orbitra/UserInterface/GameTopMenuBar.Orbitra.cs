using Content.Client._Orbitra.UserInterface;

namespace Content.Client.UserInterface.Systems.MenuBar.Widgets;

public sealed partial class GameTopMenuBar
{
    /// <summary>Общее оформление обоих штатных вариантов HUD с сохранением контролов и привязок.</summary>
    private void InitializeOrbitra()
    {
        OrbitraHudMenus.StyleButton(EscapeButton, "menu");
        OrbitraHudMenus.StyleButton(GuidebookButton, "circle_question_mark");
        OrbitraHudMenus.StyleButton(CharacterButton, "user_round");
        OrbitraHudMenus.StyleButton(EmotesButton, "drama");
        OrbitraHudMenus.StyleButton(CraftingButton, "hammer");
        OrbitraHudMenus.StyleButton(ActionButton, "hand");
        OrbitraHudMenus.StyleButton(AdminButton, "gavel");
        OrbitraHudMenus.StyleButton(SandboxButton, "shovel");
        OrbitraHudMenus.StyleButton(AHelpButton, "warning");
    }
}
