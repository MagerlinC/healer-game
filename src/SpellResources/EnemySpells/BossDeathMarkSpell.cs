using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Bringer of Death's Death Mark attack.
/// Brands a random party member with a necrotic curse that deals 10 damage per
/// second for 12 seconds.  Can be removed by Dispel.
/// </summary>
[GlobalClass]
public partial class BossDeathMarkSpell : SpellResource
{
	float _damageAmountPerTick = 25f;
	public BossDeathMarkSpell()
	{
		Name = "Death Mark";
		Description = $"Brands the target with a necrotic curse, dealing {_damageAmountPerTick} damage per second until cleansed.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new DeathMarkEffect(_damageAmountPerTick)
			{
				AbilityName = Name,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				Icon = Icon
			});
		}
	}
}