using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Holy;

public partial class ArchangelOfLightSpell : UltimateSpellResource
{
	static float BuffDuration = 10f;
	static float HealingIncrease = 0.3f;

	/// <summary>
	/// Requirement: spend a total of 50 mana on holy spells.
	/// Each holy spell cast contributes its ManaCost to Progress.
	/// </summary>
	public override float Requirement => 50f;

	public override string ActiveEffectId => "ArchangelOfLightEffect";

	public ArchangelOfLightSpell()
	{
		Name = "Archangel of Light";
		Description =
			$"Embody the ultimate form of light for {BuffDuration:F0}s, making all spells free and increasing holy spell healing by {100 * HealingIncrease:F0}%. Overhealing a target causes the excess healing to heal all allies and damage all enemies.";
		ActivationDescription = "Spend a total of 50 mana on holy spells.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 0f;
		School = SpellSchool.Holy;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Ally;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "holy/archangel-of-light.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new ArchangelOfLightEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Holy spells are free and do {100 * HealingIncrease:F0}% increased healing. Overhealing a target causes the excess healing to heal all allies and damage all enemies."
		});
	}

	/// <summary>
	/// Accumulate mana spent on holy spells toward the 50-mana requirement.
	/// </summary>
	public override void OnRegularSpellCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Holy) return;
		Progress = Mathf.Min(Progress + ctx.Spell.ManaCost, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}