using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Fabricator repair and conversion settings and the shared current operation.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarFabricatorComponent : Component
{
    /// <summary>Shared cult energy charged on successful completion.</summary>
    [DataField]
    public int Energy = 200;

    /// <summary>Required uninterrupted repair time.</summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);

    /// <summary>Extra repair amount added to the remaining integrity.</summary>
    [DataField]
    public int RepairBonus = 15;

    /// <summary>Exact neutral structure prototypes allowed as repair targets.</summary>
    [DataField]
    public HashSet<EntProtoId> NeutralStructures = new();

    /// <summary>Exact floor recipes; unsupported source tiles are never modified.</summary>
    [DataField]
    public Dictionary<ProtoId<ContentTileDefinition>, ProtoId<ContentTileDefinition>> Floors = new();

    /// <summary>Energy charged for a successfully placed floor.</summary>
    [DataField]
    public int FloorEnergy = 20;

    /// <summary>Uninterrupted floor fabrication time.</summary>
    [DataField]
    public TimeSpan FloorDelay = TimeSpan.FromSeconds(2);

    /// <summary>Exact wall prototypes accepted by the Girder-to-brass adapter.</summary>
    [DataField]
    public HashSet<EntProtoId> Walls = new();

    /// <summary>Energy charged for a successful wall conversion.</summary>
    [DataField]
    public int WallEnergy = 200;

    /// <summary>Uninterrupted wall conversion time.</summary>
    [DataField]
    public TimeSpan WallDelay = TimeSpan.FromSeconds(6);

    /// <summary>Exact full-tile reinforced windows accepted by the window adapter.</summary>
    [DataField]
    public HashSet<EntProtoId> Windows = new();

    /// <summary>Energy charged for a successful window conversion.</summary>
    [DataField]
    public int WindowEnergy = 200;

    /// <summary>Uninterrupted window conversion time.</summary>
    [DataField]
    public TimeSpan WindowDelay = TimeSpan.FromSeconds(6);

    /// <summary>Prevents concurrent use of the same tool.</summary>
    public DoAfterId? Pending;
}
