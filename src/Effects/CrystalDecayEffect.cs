using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Crystal Knight's Crystal Decay debuff.
/// Deals 10 damage per second until the player casts Dispel on the afflicted
/// party member. Marked as harmful so <see cref="Character.RemoveHarmfulEffects"/>
/// can cleanse it.
///
/// Duration is set to a very large value because the effect persists until
/// dispelled — it does not expire on its own.
/// </summary>
public partial class CrystalDecayEffect : DamageOverTimeEffect
{
	public CrystalDecayEffect() : base(damagePerTick: 10f, duration: 9999f, tickInterval: 1f)
	{
		EffectId   = "CrystalDecay"; // Unique ID ensures reapplication refreshes instead of stacking
		School     = SpellSchool.Void;
		IsHarmful  = true;           // Can be cleansed by Dispel
	}
}
