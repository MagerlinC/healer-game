using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

[GlobalClass]
public partial class VoidsEmbraceSpell : SpellResource
{
	[Export] public float HealthDrainPerStack = 1f;
	[Export] public float HastePerStack = 1f;
	[Export] public float VoidDamageIncreasePerStack = 1f;

	public VoidsEmbraceSpell()
	{
		Name = "Void's Embrace";
		Description =
			$"Embrace the power of the void, gaining one stack of Void's Embrace per second. Each stack causes you to lose {HealthDrainPerStack} HP/s but gain {HastePerStack}% haste and {VoidDamageIncreasePerStack}% increased void damage per stack. No maximum stacks - this embrace ends in death.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Void;
		Tags = SpellTags.Void;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/voids-embrace.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster.ApplyEffect(new VoidsEmbraceEffect(HealthDrainPerStack, HastePerStack, VoidDamageIncreasePerStack)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}