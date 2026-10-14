using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Kindling;

public partial class SharedFlameSpell : UltimateSpellResource
{
	const float Duration = 10f;
	const float DrainPerSecond = 5f;
	const float LeechRate = 0.20f;
	const float DamageMultiplierOnExpire = 8f;

	/// <summary>
	/// Requirement: spend a total of 50 health on Kindling spells.
	/// Each Kindling spell cast contributes its HealthCost to Progress.
	/// </summary>
	public override float Requirement => 50f;

	public override string ActiveEffectId => "SharedFlameEffect";

	public SharedFlameSpell()
	{
		Name = "Shared Flame";
		Description =
			$"Share your flame with the party, making all allies lose {DrainPerSecond} HP/s but leech {100 * LeechRate:F0}% of damage dealt by you as healing to all party members. When this spell ends, all enemies are damaged for {100 * DamageMultiplierOnExpire:F0}% of total life leeched.";
		ActivationDescription = "Spend a total of 50 health on Kindling spells.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Kindling;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Self;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/vampiric-embrace.png");
	}

	/// <summary>
	/// Applies the Shared Flame buff to the caster. While active, all party members
	/// are drained for <see cref="DrainPerSecond"/> HP/s, and <see cref="LeechRate"/>
	/// of all damage dealt by the caster is redistributed as healing to the whole party.
	/// On expiry, enemies are hit for <see cref="DamageMultiplierOnExpire"/> × total leeched.
	/// </summary>
	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new SharedFlameEffect(DrainPerSecond, LeechRate, DamageMultiplierOnExpire, Duration)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}

	/// <summary>
	/// Accumulate health spent on Kindling spells toward the 50-health requirement.
	/// </summary>
	public override void OnRegularSpellCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Kindling) return;
		if (ctx.Spell.HealthCost <= 0f) return;
		Progress = Mathf.Min(Progress + ctx.Spell.HealthCost, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}