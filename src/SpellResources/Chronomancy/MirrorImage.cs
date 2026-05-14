using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

public partial class MirrorImage : UltimateSpellResource
{
	const float Duration = 10f;

	/// <summary>
	/// Requirement: accumulate 12 seconds of chronomancy buff coverage across all targets.
	/// Each active chronomancy buff on any party member contributes 1 second per real second.
	/// Multiple simultaneous buffs or targets all count independently.
	/// </summary>
	public override float Requirement => 12f;

	public override string ActiveEffectId => "MirrorImageEffect";

	public MirrorImage()
	{
		Name = "Mirror Image";
		Description =
			$"Reach into a parallel timeline and pull a mirror image of yourself into this one. Each time you cast a spell while Mirror Image is active, your mirror image mimics that spell, dealing 50% of the damage or healing to a random target within range. Lasts {Duration:F0}s.";
		ActivationDescription = "Accumulate a total of 12 seconds of chronomancy buff duration across all targets.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Chronomancy;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/mirror-image.png");
	}

	/// <summary>
	/// Applies the Mirror Image buff to the caster. While active, every spell cast
	/// is echoed at 50% power to a random valid target. The mirror image copies only
	/// the final damage or healing value — it does NOT re-apply the full spell (no
	/// HoT/DoT applications, no secondary effects), avoiding effect ID clashes.
	/// </summary>
	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new MirrorImageEffect(Duration)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}

	/// <summary>
	/// Each frame: count active chronomancy buffs on every living party member.
	/// Each active chronomancy buff contributes delta seconds to Progress.
	/// </summary>
	public override void OnProcessTick(Character caster, float delta)
	{
		if (IsRequirementMet) return;

		foreach (var character in caster.CollectAlivePartyMembers())
		{
			foreach (var effect in character.GetAllEffects())
			{
				// Count chronomancy buffs: beneficial, finite-duration, chronomancy school.
				if (effect.School == SpellSchool.Chronomancy
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