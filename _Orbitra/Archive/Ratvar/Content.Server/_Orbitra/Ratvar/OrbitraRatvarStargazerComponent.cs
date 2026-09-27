using Content.Shared._Orbitra.Ratvar;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Machine-local reservation and prototype-controlled blessing parameters.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraRatvarStargazerComponent : Component
{
    /// <summary>Uninterrupted ritual duration.</summary>
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(6);
    /// <summary>Shared machine cooldown after success.</summary>
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(180);
    /// <summary>Available effects and maximum levels.</summary>
    [DataField] public Dictionary<OrbitraRatvarEnchantment, int> Enchantments = new();
    /// <summary>Additional melee damage per level.</summary>
    [DataField] public float SharpnessPerLevel = 2;
    /// <summary>Fraction of base damage healed per level.</summary>
    [DataField] public float SoulHealingPerLevel = 0.1f;
    /// <summary>Native fire stacks per level of the fire blessing.</summary>
    [DataField] public float FireStacksPerLevel = 1;
    /// <summary>Success effect.</summary>
    [DataField] public EntProtoId Effect = "OrbitraRatvarEnchantEffect";
    /// <summary>Success sound.</summary>
    [DataField] public SoundSpecifier? Sound;
    /// <summary>Reserved machine operation.</summary>
    public DoAfterId? Pending;
    /// <summary>Earliest next use.</summary>
    [AutoPausedField] public TimeSpan NextEnchant;
}
