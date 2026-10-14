using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Wick;

public partial class TwinFlameSpell : UltimateSpellResource
{
	const float Duration = 10f;

	/// <summary>
	/// Requirement: accumulate 12 seconds of Wick buff coverage across all targets.
	/// Each active Wick buff on any party member contributes 1 second per real second.
	/// Multiple simultaneous buffs or targets all count independently.
	/// </summary>
	public override float Requirement => 12f;

	public override string ActiveEffectId => "TwinFlameEffect";

	public TwinFlameSpell()
	{
		Name = "Twin Flame";
		Description =
			$"Split your flame in two, lighting a twin of yourself. Each time you cast a spell while Twin Flame is active, your twin mimics that spell, dealing 50% of the damage or restoring 50% of the light to a random target within range. Lasts {Duration:F0}s.";
		ActivationDescription = "Accumulate a total of 12 seconds of Wick buff duration across all targets.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Wick;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Self;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/mirror-image.png");
	}

	/// <summary>
	/// Applies the Twin Flame buff to the caster. While active, every spell cast
	/// is echoed at 50% power to a random valid target. The mirror image copies only
	/// the final damage or healing value — it does NOT re-apply the full spell (no
	/// HoT/DoT applications, no secondary effects), avoiding effect ID clashes.
	/// </summary>
	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new TwinFlameEffect(Duration)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}

	/// <summary>
	/// Each frame: count active Wick buffs on every living party member.
	/// Each active Wick buff contributes delta seconds to Progress.
	/// </summary>
	public override void OnProcessTick(Character caster, float delta)
	{
		if (IsRequirementMet) return;

		foreach (var character in caster.CollectAlivePartyMembers())
		{
			foreach (var effect in character.GetAllEffects())
			{
				// Count Wick buffs: beneficial, finite-duration, Wick school.
				if (effect.School == SpellSchool.Wick
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