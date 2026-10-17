using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Ember;

public partial class OneWithTheAshSpell : UltimateSpellResource
{
	static float BuffDuration = 10f;
	static float HealingIncrease = 0.3f;

	/// <summary>
	/// Requirement: accumulate 100 seconds of Ember HoT coverage across all targets.
	/// Each active ember HoT on any party member contributes 1 second per real second.
	/// Multiple simultaneous HoTs or targets all count independently.
	/// </summary>
	public override float Requirement => 100f;

	public override string ActiveEffectId => "OneWithTheAsh";

	public OneWithTheAshSpell()
	{
		Name = "One With the Ash";
		Description =
			$"Become one with the ash for {BuffDuration:F0}s, making all spells instant cast and increasing light restored by Ember spells by {100 * HealingIncrease:F0}%. Directly restoring light to a target refreshes the duration of all beneficial Ember effects on it.";
		ActivationDescription = $"Accumulate a total of {Requirement:F0} seconds of lingering Ember light across all targets.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 0f;
		School = SpellSchool.Ember;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Ally;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/one-with-nature.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new OneWithTheAshEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Ember restoring spells are instant and restore {100 * HealingIncrease:F0}% more light. Beneficial Ember effects on targets you directly restore refresh their duration."
		});
	}

	/// <summary>
	/// Each frame: count active ember HoT effects on every living party member.
	/// Each active HoT contributes delta seconds to Progress (multiple instances stack).
	/// </summary>
	public override void OnProcessTick(Character caster, float delta)
	{
		if (IsRequirementMet) return;

		foreach (var character in caster.CollectAlivePartyMembers())
		{
			foreach (var effect in character.GetAllEffects())
			{
				// Count ember HoTs: beneficial, finite-duration, ember school.
				// Excludes OneWithTheAsh itself (infinite-adjacent) and enemy debuffs.
				if (effect.School == SpellSchool.Ember
				    && !effect.IsHarmful
				    && effect.Duration < GameConstants.InfiniteDuration)
				{
					Progress += delta;
				}
			}
		}

		Progress = Mathf.Min(Progress, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}