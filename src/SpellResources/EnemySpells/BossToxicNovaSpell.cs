using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Demon Slime's telegraphed AoE — Toxic Nova.
/// After a 3-second wind-up the slime ruptures, spraying toxic bile across the
/// entire party for 50 damage — unless the player deflects in time.
///
/// The parry check is resolved by <see cref="DemonSlime"/> before this
/// spell is ever cast; if deflected, the cast is skipped entirely.
/// </summary>
[GlobalClass]
public partial class BossToxicNovaSpell : SpellResource
{
	public float DamageAmount = 50f;

	public BossToxicNovaSpell()
	{
		Name = "Toxic Nova";
		Description = "The Demon Slime erupts in a wave of toxic bile, drenching the entire party — unless deflected.";
		Tags = SpellTags.Damage | SpellTags.Nature;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/demon-slime/toxic-nova.png");
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>Targets every alive party member.</summary>
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