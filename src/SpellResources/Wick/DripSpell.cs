using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Wick;

/// <summary>
/// Places a temporal loop on a single ally. After the duration elapses, the
/// loop resolves and instantly heals them for a burst of health — effectively
/// a time-delayed safety net.
///
/// Useful for pre-empting incoming damage: cast it on a tank before a big hit
/// and the heal fires automatically at the right moment.
/// </summary>
[GlobalClass]
public partial class DripSpell : SpellResource
{
	[Export] public float ManaAmount = 35f;
	[Export] public float Delay = 4f;

	public DripSpell()
	{
		Name = "Drip";
		Description =
			$"Traps an ally in a slow drip of wax. After {Delay}s, the wax falls, restoring {ManaAmount} kindling.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 8f;
		School = SpellSchool.Wick;
		Tags = SpellTags.Healing | SpellTags.Duration;
		RequiredSchoolPoints = 1;
		TargetingType = TargetingType.Self;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/mana-loop.png");
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		// Always target self
		return [caster];
	}

	public override float GetBaseValue()
	{
		return ManaAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new Effects.DripEffect(Delay, ctx.FinalValue)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}