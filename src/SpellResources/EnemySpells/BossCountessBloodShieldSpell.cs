using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Countess's Blood Shield — a reactive self-shield conjured from coagulated blood.
///
/// Applied once when the Countess drops below 35% health. Absorbs a large amount
/// of damage before the shield shatters, giving her a second wind.
/// </summary>
[GlobalClass]
public partial class BossCountessBloodShieldSpell : SpellResource
{
	public float ShieldAmount = 450f;
	public float ShieldDuration = 30f;

	public BossCountessBloodShieldSpell()
	{
		Name = "Blood Shield";
		Description = "The Countess encases herself in a barrier of coagulated blood, absorbing incoming damage.";
		Tags = SpellTags.Helpful;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Helpful;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new ShieldEffect(ShieldAmount, ShieldDuration)
		{
			AbilityName = Name,
			Description = Description,
			SourceCharacterName = ctx.Caster?.CharacterName,
			Icon = Icon
		});
	}
}
