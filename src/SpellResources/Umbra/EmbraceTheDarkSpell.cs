using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Umbra;

[GlobalClass]
public partial class EmbraceTheDarkSpell : UltimateSpellResource
{
	[Export] public float HealthDrainPerStack = 1f;
	[Export] public float HastePerStack = 1f;
	[Export] public float UmbraDamageIncreasePerStack = 2f;

	/// <summary>
	/// Requirement: cast 7 umbral spells.
	/// Each umbral school spell cast increments Progress by 1.
	/// </summary>
	public override float Requirement => 7f;

	public override string ActiveEffectId => "EmbraceTheDark";

	public EmbraceTheDarkSpell()
	{
		Name = "Embrace the Dark";
		Description =
			$"Let the dark in, gaining one stack of Embrace the Dark per second. Each stack causes you to lose {HealthDrainPerStack} light/s but gain {HastePerStack}% haste and {UmbraDamageIncreasePerStack}% increased umbral damage per stack. No maximum stacks - this embrace ends in death.";
		ActivationDescription = $"Cast {Requirement:F0} Umbra spells.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Umbra;
		RequiredSchoolPoints = 3;
		TargetingType = TargetingType.Self;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/voids-embrace.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster.ApplyEffect(new EmbraceTheDarkEffect(HealthDrainPerStack, HastePerStack, UmbraDamageIncreasePerStack)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}

	/// <summary>
	/// Increment progress each time a umbral spell is cast.
	/// </summary>
	public override void OnRegularSpellCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Umbra) return;
		Progress = Mathf.Min(Progress + 1f, Requirement);
	}

	public override bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}