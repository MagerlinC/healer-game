using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Skull's telegraphed area ability — Necrotic Waves.
///
/// After a 1-second cast wind-up the skull unleashes a series of sweeping
/// void-energy waves that travel from one screen edge to the other.  Each wave
/// covers the full span of the screen except for a small gap; players must
/// stand in the gap to avoid taking void damage.
///
/// The actual wave spawning and timing is managed entirely by
/// <see cref="FlyingSkull"/> — this resource exists only to carry the ability's
/// name and icon for the cast-bar UI.
/// </summary>
[GlobalClass]
public partial class BossNecroticWavesSpell : SpellResource
{
	public BossNecroticWavesSpell()
	{
		Name        = "Necrotic Waves";
		Description = "The Flying Skull channels necrotic energy and unleashes a series of void waves across the battlefield. Stand in the gap of each wave to avoid damage.";
		Tags        = SpellTags.Damage | SpellTags.Void;
		ManaCost    = 0f;
		CastTime    = 0f;
		Parryable   = false;
		Icon        = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/flying-skull/banshee-wail.png");
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => 0f;

	// No Apply — FlyingSkull drives all wave spawning directly.
	public override void Apply(SpellContext ctx) { }
}
