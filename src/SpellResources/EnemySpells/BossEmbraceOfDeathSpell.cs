using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Bringer of Death's telegraphed AoE — Embrace of Death.
/// After a 3.5-second wind-up, all party members are engulfed in necrotic
/// energy, taking 45 damage unless the player deflects in time.
///
/// The parry check is resolved by <see cref="BringerOfDeath"/> before this
/// spell is ever cast; if deflected, the cast is skipped entirely.
/// </summary>
[GlobalClass]
public partial class BossEmbraceOfDeathSpell : SpellResource
{
	public float DamageAmount = 50f;

	public BossEmbraceOfDeathSpell()
	{
		Name = "Embrace of Death";
		Description = "The Bringer envelops the entire party in a crushing wave of necrotic energy — unless deflected.";
		Tags = SpellTags.Damage | SpellTags.Void;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/bringer-of-death/embrace-of-death.png");
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
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}