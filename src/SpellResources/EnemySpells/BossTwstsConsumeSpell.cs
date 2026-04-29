using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// That Which Swallowed the Stars' "Consume" ability.
///
/// Applies a <see cref="BossTwstsConsumeEffect"/> to every living party member
/// simultaneously.  The DoT deals escalating void damage over 30 seconds and
/// heals the boss if dispelled — the earlier the dispel, the larger the heal.
///
/// The boss (caster) reference is threaded into each effect instance so that
/// the dispel-triggered heal can be applied from inside the effect's
/// <see cref="BossTwstsConsumeEffect.OnExpired"/> callback.
/// </summary>
[GlobalClass]
public partial class BossTwstsConsumeSpell : SpellResource
{
	int _targetCount;
	public BossTwstsConsumeSpell(int targetCount)
	{
		Name = "Consume";
		Description =
			"The star-devourer's void seeps into the party, dealing increasing damage " +
			"over 30 seconds. Dispelling it returns the energy to the boss as healing — " +
			"the sooner it is cleansed, the greater the heal.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets +
		                          "enemy/that-which-swallowed-the-stars/consume.png");
		EffectType = EffectType.Harmful;
		_targetCount = targetCount;
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var possibleTargets = caster.GetTree().GetNodesInGroup("party")
			.Cast<Character>()
			.Where(t => t.IsAlive)
			.ToList();

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		possibleTargets = possibleTargets.OrderBy(_ => rng.Randf()).ToList();

		return possibleTargets.Take(_targetCount).ToList();
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new BossTwstsConsumeEffect
			{
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				Icon = Icon,
				Boss = ctx.Caster
			});
		}
	}
}