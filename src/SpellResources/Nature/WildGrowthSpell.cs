using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Applies a short heal-over-time to every living party member simultaneously.
/// Weaker per-target than Renewing Bloom, but covers the whole group at once.
/// </summary>
[GlobalClass]
public partial class WildGrowthSpell : SpellResource
{
	[Export] public float HealPerTick = 4f;
	[Export] public float EffectDuration = 8f;
	[Export] public float TickInterval = 1f;

	public WildGrowthSpell()
	{
		Name = "Wild Growth";
		Description =
			$"Spreads natural energy across the party, healing each member for {HealPerTick} every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 14f;
		CastTime = 0.0f;
		Cooldown = 8f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing | SpellTags.Nature | SpellTags.Duration | SpellTags.GroupSpell;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/nature/wild-growth.png");
	}

	public override float GetBaseValue()
	{
		return HealPerTick;
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character { IsAlive: true } c)
				targets.Add(c);
		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.ApplyEffect(new Effects.HealOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
			{
				Icon = Icon,
				SourceCharacterName = ctx.Caster.CharacterName,
				AbilityName = Name
			});
	}
}