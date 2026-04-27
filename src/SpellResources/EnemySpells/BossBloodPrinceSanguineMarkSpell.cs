using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Blood Prince's Sanguine Mark — brands a party member with a vampiric curse.
///
/// Applies <see cref="SanguineMarkEffect"/> which:
///   • Deals 20 damage per second for 12 seconds.
///   • If dispelled early, heals the Blood Prince proportionally to how much
///     duration remained — the earlier the dispel, the larger the heal.
///
/// In Phase 2, the Blood Prince casts this twice in a row to mark two targets.
/// The boss passes itself as the <see cref="SanguineMarkEffect.Boss"/> reference.
/// </summary>
[GlobalClass]
public partial class BossBloodPrinceSanguineMarkSpell : SpellResource
{
	/// <summary>Maximum boss heal when the mark is dispelled at the moment of application.</summary>
	public float BossHealOnDispel = 300f;

	/// <summary>Reference to the Blood Prince — must be set by the boss before casting.</summary>
	public Character Boss { get; set; }

	public BossBloodPrinceSanguineMarkSpell()
	{
		Name = "Sanguine Mark";
		Description =
			"Brands the target with a vampiric curse, dealing 20 damage per second for 12 seconds. " +
			"If dispelled, the Blood Prince absorbs the remaining curse energy as healing — " +
			"the sooner it is cleansed, the greater the heal.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new SanguineMarkEffect
			{
				BossHealOnDispel = BossHealOnDispel,
				Boss = Boss,
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				Icon = Icon
			});
		}
	}
}
