using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Skull's telegraphed AoE — Banshee Wail.
/// After a 3.5-second wind-up the skull unleashes a devastating wall of
/// void-laced sonic energy that crashes over the entire party for heavy damage —
/// unless deflected in time.
///
/// The parry check is resolved by <see cref="FlyingSkull"/> before this
/// spell is ever cast; if deflected, the cast is skipped entirely.
/// </summary>
[GlobalClass]
public partial class BossBansheeWailSpell : SpellResource
{
	public float DamageAmount = 65f;

	public BossBansheeWailSpell()
	{
		Name = "Banshee Wail";
		Description =
			"The Flying Skull unleashes a devastating wail of void energy that crashes over the entire party — unless deflected.";
		Tags = SpellTags.Damage | SpellTags.Void;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/flying-skull/banshee-wail.png");
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				targets.Add(c);
		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		DeflectSpell.PlayDeflectFailedSound(ctx.Caster);
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}