using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Demon Slime's Corrosive Ooze debuff.
/// Coats the target in acidic slime, dealing 12 damage per second for 10 seconds.
/// Can be cleansed by Dispel.
/// </summary>
public partial class CorrosiveOozeEffect : DamageOverTimeEffect
{
    public CorrosiveOozeEffect() : base(damagePerTick: 12f, duration: 10f, tickInterval: 1f)
    {
        EffectId  = "CorrosiveOoze";
        School    = SpellSchool.Nature;
        IsHarmful = true; // Dispellable
    }
}
