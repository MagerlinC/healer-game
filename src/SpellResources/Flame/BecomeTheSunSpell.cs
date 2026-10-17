using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Flame;

public partial class BecomeTheSunSpell : UltimateSpellResource
{
	static float BuffDuration = 10f;
	static float HealingIncrease = 0.3f;

	/// <summary>
	/// Requirement: spend a total of 50 mana on Flame spells.
	/// Each Flame spell cast contributes its ManaCost to Progress.
	/// </summary>
	public override float Requirement => 50f;

	public override string ActiveEffectId => "BecomeTheSunEffect";

	public BecomeTheSunSpell()
	{
		Name = "Become the Sun";
		Description =
			$"Burn as bright as the sun for {BuffDuration:F0}s, making all spells free and increasing light restored by Flame spells by {100 * HealingIncrease:F0}%. Light that overflows a full target spreads to all allies and burns all enemies.";
		ActivationDescription = $"Spend a total of {Requirement:F0} kindling on Flame spells.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 0f;
		School = SpellSchool.Flame;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Ally;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "holy/archangel-of-light.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new BecomeTheSunEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Flame spells are free and restore {100 * HealingIncrease:F0}% more light. Light that overflows a full target spreads to all allies and burns all enemies."
		});
	}

	/// <summary>
	/// Accumulate mana spent on Flame spells toward the 50-mana requirement.
	/// </summary>
	public override void OnRegularSpellCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Flame) return;
		Progress = Mathf.Min(Progress + ctx.Spell.ManaCost, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}