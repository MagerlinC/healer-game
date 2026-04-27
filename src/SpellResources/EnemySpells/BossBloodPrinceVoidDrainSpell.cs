using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Blood Prince's Void Drain — a Phase 2 AoE curse that drains the entire party.
///
/// Applies a non-dispellable void DoT to every living party member, dealing
/// 20 damage per second for 10 seconds. The curse is bound in blood magic too
/// ancient to be cleansed — it cannot be removed by Dispel.
///
/// Used exclusively in Phase 2 once Blood Covenant is active, dramatically
/// increasing the healing burden on the player.
/// </summary>
[GlobalClass]
public partial class BossBloodPrinceVoidDrainSpell : SpellResource
{
	const float DamagePerTick = 20f;
	const float Duration = 10f;

	/// <summary>Reference to the Blood Prince — used as the DoT's caster for log entries.</summary>
	public Character Boss { get; set; }

	public BossBloodPrinceVoidDrainSpell()
	{
		Name = "Void Drain";
		Description =
			"Ancient blood magic drains the life from the entire party simultaneously, " +
			"dealing 20 damage per second for 10 seconds. This curse cannot be dispelled.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	/// <summary>Targets every living party member.</summary>
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
		// Re-use VoidDrainEffect with no heal-fraction (siphon is handled by the
		// Blood Covenant passive in TheBloodPrince._Process, not per-tick).
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new VoidDrainEffect(DamagePerTick, Duration, 0f)
			{
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				IsDispellable = false, // blood magic — cannot be cleansed
				Icon = Icon
			});
		}
	}
}
