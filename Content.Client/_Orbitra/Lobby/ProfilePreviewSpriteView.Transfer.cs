namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
    /// <summary>Переносит готовый манекен в буфер перехода вместе со снаряжением.</summary>
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
