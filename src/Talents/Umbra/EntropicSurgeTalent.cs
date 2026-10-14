using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Umbra;

/// <summary>
/// Umbra spells have a 15% chance to trigger an Entropic Surge, doubling their
/// final value on that cast. Adds an exciting high-variance element to Umbra
/// builds without any guaranteed throughput increase.
/// </summary>
public class EntropicSurgeTalent : ISpellModifier
{
	const float SurgeChance = 0.15f;
	const float SurgeMultiplier = 2.0f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Umbra)) return;
		if (GD.Randf() < SurgeChance)
			ctx.FinalValue *= SurgeMultiplier;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}