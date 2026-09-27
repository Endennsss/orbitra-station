using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>A permanent, non-stacking blessing, independent of the wielder's role.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraRatvarEnchantmentComponent : Component
{
    /// <summary>Selected blessing.</summary>
    [AutoNetworkedField] public OrbitraRatvarEnchantment Kind;
    /// <summary>Rolled strength.</summary>
    [AutoNetworkedField] public int Level;
    /// <summary>Additional damage distributed over the original damage types.</summary>
    [AutoNetworkedField] public DamageSpecifier Bonus = new();
    /// <summary>Fraction of base damage healed per soul-tap level.</summary>
    [AutoNetworkedField] public float HealingFraction;
    /// <summary>Server-side fire stacks applied by one successful hit.</summary>
    public float FireStacks;
}

[Serializable, NetSerializable]
public enum OrbitraRatvarEnchantment : byte { Sharpness, Tiny, SoulTap, Burn }
