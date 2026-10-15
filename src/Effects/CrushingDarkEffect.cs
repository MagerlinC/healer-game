using healerfantasy.LastLight.Hud;
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// The Infinite Abyss's melee leaves the dark pressing down on its target: each
/// stack makes them take <see cref="AmplificationPerStack"/> more damage, up to
/// <see cref="MaxStacks"/>. Every new hit adds a stack and refreshes the duration.
/// Dispellable — clearing it is how the tank survives.
/// </summary>
public partial class CrushingDarkEffect : CharacterEffect, ICharacterModifier
{
	public const string Id = "CrushingDark";
	const int MaxStacks = 5;
	const float AmplificationPerStack = 0.08f;

	public CrushingDarkEffect(float duration)
		: base(duration, 0f)
	{
		EffectId = Id;
		AbilityName = "Crushing Dark";
		Description = $"Takes {AmplificationPerStack * 100:F0}% more damage per stack. Dispellable.";
		Icon = LastLightIcons.ForAbyss("crushing-dark");
		IsHarmful = true;
		IsDispellable = true;
		MaximumStacks = MaxStacks;
	}

	public override void OnReapplied(Character target, CharacterEffect newEffect)
	{
		if (CurrentStacks < MaxStacks) CurrentStacks++;
		Refresh();
	}

	public void Modify(CharacterStats stats)
	{
		stats.DamageTakenMultiplier *= 1f + AmplificationPerStack * CurrentStacks;
	}
}
