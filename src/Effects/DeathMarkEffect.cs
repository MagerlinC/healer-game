using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Bringer of Death's Death Mark debuff.
/// Brands the target with a necrotic curse, dealing 10 damage per second for
/// 12 seconds.  Can be cleansed by Dispel.
/// </summary>
public partial class DeathMarkEffect : DamageOverTimeEffect
{
    public DeathMarkEffect() : base(damagePerTick: 10f, duration: 12f, tickInterval: 1f)
    {
        EffectId = "DeathMark";
        School   = SpellSchool.Void;
        IsHarmful = true; // Dispellable
    }
}
