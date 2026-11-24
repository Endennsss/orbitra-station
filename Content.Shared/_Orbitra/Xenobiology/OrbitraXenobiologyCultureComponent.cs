// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Xenobiology;

/// <summary>Спящая культура, которая при насыщении водой вне контейнера превращается в детёныша.</summary>
[RegisterComponent]
public sealed partial class OrbitraXenobiologyCultureComponent : Component
{
    /// <summary>Порода будущего слизня.</summary>
    [DataField(required: true)]
    public ProtoId<OrbitraXenobiologyBreedPrototype> Breed;

    /// <summary>Раствор, принимающий воду для гидратации.</summary>
    [DataField]
    public string Solution = "culture";

    /// <summary>Признак завершённого выращивания культуры.</summary>
    public bool Used;
}
