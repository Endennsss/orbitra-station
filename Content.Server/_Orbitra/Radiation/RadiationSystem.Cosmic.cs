// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Content.Server._Orbitra.Radiation;
using Content.Server.Radiation.Components;

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem
{
    [Dependency] private OrbitraCosmicRadiationSystem _orbitraCosmic = default!;

    private void ApplyOrbitraCosmicRadiation(Entity<RadiationReceiverComponent> receiver, float duration)
    {
        // Фон виден штатным дозиметрам; его урон уже применён отдельно с новой защитой.
        receiver.Comp.CurrentRadiation += _orbitraCosmic.ApplyExposure(receiver.Owner, duration);
    }
}
