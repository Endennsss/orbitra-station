using Content.Shared.Body;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarCrystalSystem
{
    /// <summary>Snapshots visible organ appearance without copying anatomy, equipment or capabilities.</summary>
    private void CopyProjectionAppearance(EntityUid source, EntityUid projection)
    {
        if (!_visualBody.TryGatherMarkingsData(source, null, out var profiles, out _, out var markings))
            return;

        _visualBody.ApplyProfiles(projection, profiles);
        _visualBody.CopyAppearanceFrom(source, projection);
        // Применение создаёт собственные словари и списки маркировок вместо общей ссылки с телом.
        _visualBody.ApplyMarkings(projection, markings);
    }
}
