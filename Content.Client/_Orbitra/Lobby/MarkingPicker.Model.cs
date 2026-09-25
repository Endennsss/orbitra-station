namespace Content.Client.Humanoid;

public sealed partial class MarkingPicker
{
    private bool _orbitraModelSubscribed;

    private void SubscribeOrbitraModel()
    {
        if (_orbitraModelSubscribed || !IsInsideTree || _markingsModel == null)
            return;
        _orbitraModelSubscribed = true;
        _markingsModel.OrganDataChanged += UpdateMarkings;
        _markingsModel.EnforcementsChanged += UpdateMarkings;
        _markingsModel.OrganProfileDataChanged += OnOrbitraOrganProfileChanged;
    }

    private void UnsubscribeOrbitraModel()
    {
        if (!_orbitraModelSubscribed || _markingsModel == null)
            return;
        _orbitraModelSubscribed = false;
        _markingsModel.OrganDataChanged -= UpdateMarkings;
        _markingsModel.EnforcementsChanged -= UpdateMarkings;
        _markingsModel.OrganProfileDataChanged -= OnOrbitraOrganProfileChanged;
    }

    private void OnOrbitraOrganProfileChanged(bool refresh)
    {
        // Данные внешности приходят после состава органов: ранее пустые вкладки нужно восстановить.
        if (refresh)
            UpdateMarkings();
    }
}
