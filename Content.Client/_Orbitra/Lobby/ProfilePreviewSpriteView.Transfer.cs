namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
    /// <summary>Transfers an existing dummy to the transition buffer without respawning its equipment.</summary>
    internal void TransferOrbitraPreviewTo(ProfilePreviewSpriteView target)
    {
        target.ClearPreview();
        target.PreviewDummy = PreviewDummy;
        target.OverrideDirection = OverrideDirection;
        target.SetEntity(PreviewDummy);
        PreviewDummy = EntityUid.Invalid;
        SetEntity((EntityUid?) null);
    }
}
