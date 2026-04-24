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
	public CrystalDecayEffect() : base(20f, 30f, 1f)
	{
		EffectId = "CrystalDecay";
		School = SpellSchool.Void;
	}
}