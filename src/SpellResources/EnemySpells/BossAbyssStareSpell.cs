using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.LastLight.Hud;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Abyss Stares — the eyes flare over a long wind-up, then the whole party is
/// struck at once. Deflectable; otherwise one of the deadliest hits in the game.
/// </summary>
[GlobalClass]
public partial class BossAbyssStareSpell : SpellResource
{
	public float DamageAmount = 115f;

	public BossAbyssStareSpell()
	{
		Name = "The Abyss Stares";
		Description = "The Abyss opens its eyes on the party and everything it sees comes undone — unless deflected.";
		Tags = SpellTags.Damage;
		School = SpellSchool.Umbra;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		TargetingType = TargetingType.Enemy;
		Icon = LastLightIcons.ForAbyss("stare");
	}

	public override float GetBaseValue() => DamageAmount;

	/// <summary>Targets the entire living party.</summary>
	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
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
