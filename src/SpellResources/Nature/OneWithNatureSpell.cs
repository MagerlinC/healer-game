using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Nature;

public partial class OneWithNatureSpell : UltimateSpellResource
{
	static float BuffDuration = 10f;
	static float HealingIncrease = 0.3f;

	/// <summary>
	/// Requirement: accumulate 16 seconds of nature HoT coverage across all targets.
	/// Each active nature HoT on any party member contributes 1 second per real second.
	/// Multiple simultaneous HoTs or targets all count independently.
	/// </summary>
	public override float Requirement => 16f;

	public override string ActiveEffectId => "OneWithNature";

	public OneWithNatureSpell()
	{
		Name = "One With Nature";
		Description =
			$"Become one with nature for {BuffDuration:F0}s, making all spells instant cast and increasing nature spell healing by {100 * HealingIncrease:F0}%. Directly healing a target causes all beneficial nature effects on that target to refresh their duration.";
		ActivationDescription = "Accumulate a total of 16 seconds of nature healing-over-time duration across all targets.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 0f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/one-with-nature.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new OneWithNatureEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Nature healing spells are instant and do {100 * HealingIncrease:F0}% increased healing. Beneficial nature effects on targets you directly heal refresh their duration."
		});
	}

	/// <summary>
	/// Each frame: count active nature HoT effects on every living party member.
	/// Each active HoT contributes delta seconds to Progress (multiple instances stack).
	/// </summary>
	public override void OnProcessTick(Character caster, float delta)
	{
		if (IsRequirementMet) return;

		foreach (var character in caster.CollectAlivePartyMembers())
		{
			foreach (var effect in character.GetAllEffects())
			{
				// Count nature HoTs: beneficial, finite-duration, nature school.
				// Excludes OneWithNature itself (infinite-adjacent) and enemy debuffs.
				if (effect.School == SpellSchool.Nature
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