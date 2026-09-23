namespace Content.Client.Administration.UI.Bwoink;

public sealed partial class BwoinkControl
{
    protected override void Dispose(bool disposing)
    {
        _adminManager.AdminStatusUpdated -= UpdateButtons;
        base.Dispose(disposing);
    }
}
