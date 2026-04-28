using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Countess's Sanguine Nova — a telegraphed blood explosion that erupts
/// outward and strikes the entire party.
///
/// The parry check is resolved upstream by <see cref="TheCountess"/> before
/// this spell is ever cast — if Deflect was used in time, the boss skips
/// casting entirely. This spell fires only when the hit lands.
///
/// <see cref="Parryable"/> is set to true for UI and boss-logic identification.
/// </summary>
[GlobalClass]
public partial class BossCountessSanguineNovaSpell : SpellResource
{
	public float DamageAmount = 60f;

	public BossCountessSanguineNovaSpell()
	{
		Name = "Sanguine Nova";
		Description = "The Countess detonates a sphere of pressurised blood, " +
		              "blasting the entire party — unless deflected in time.";
		Tags = SpellTags.Damage | SpellTags.Void;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-countess/sanguine-nova.png");
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>Hits every alive party member.</summary>
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