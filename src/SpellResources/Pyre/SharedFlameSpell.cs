using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Pyre;

public partial class SharedFlameSpell : UltimateSpellResource
{
	const float Duration = 10f;
	const float DrainPerSecond = 5f;
	const float LeechRate = 0.20f;
	const float DamageMultiplierOnExpire = 8f;

	/// <summary>
	/// Requirement: spend a total of 75 health on Pyre spells.
	/// Each Pyre spell cast contributes its HealthCost to Progress.
	/// </summary>
	public override float Requirement => 75f;

	public override string ActiveEffectId => "SharedFlameEffect";

	public SharedFlameSpell()
	{
		Name = "Shared Flame";
		Description =
			$"Share your flame with the party, making all allies lose {DrainPerSecond} light/s, but {100 * LeechRate:F0}% of the damage you deal is restored to all party members as light. When this spell ends, all enemies are damaged for {100 * DamageMultiplierOnExpire:F0}% of the total light restored this way.";
		ActivationDescription = $"Burn a total of {Requirement:F0} of your own light on Pyre spells.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Pyre;
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
	/// Accumulate health spent on Pyre spells toward the 75-health requirement.
	/// </summary>
	public override void OnRegularSpellCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Pyre) return;
		if (ctx.Spell.HealthCost <= 0f) return;
		Progress = Mathf.Min(Progress + ctx.Spell.HealthCost, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}