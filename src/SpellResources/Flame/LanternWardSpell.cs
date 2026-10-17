using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Wraps a target in a layer of Flame light, creating a damage-absorbing shield
/// for a short duration. The shield soaks incoming hits before they can damage
/// health, then fades naturally on expiry.
///
/// An excellent pre-emptive cooldown to cast before a predictable boss attack.
/// </summary>
[GlobalClass]
public partial class LanternWardSpell : SpellResource
{
	[Export] public float ShieldAmount = 40f;
	[Export] public float ShieldDuration = 8f;

	public LanternWardSpell()
	{
		Name = "Lantern Ward";
		Description = $"Surround the target in lantern light, absorbing up to {ShieldAmount} damage for {ShieldDuration}s.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 8f;
		School = SpellSchool.Flame;
		RequiredSchoolPoints = 1;
		Tags = SpellTags.Healing;
		TargetingType = TargetingType.Ally;
	}

	public override float GetBaseValue()
	{
		return ShieldAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new ShieldEffect("LanternWard", ctx.FinalValue, ShieldDuration)
		{
			EffectId = "LanternWard",
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}