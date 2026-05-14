using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Places a temporal loop on a single ally. After the duration elapses, the
/// loop resolves and instantly heals them for a burst of health — effectively
/// a time-delayed safety net.
///
/// Useful for pre-empting incoming damage: cast it on a tank before a big hit
/// and the heal fires automatically at the right moment.
/// </summary>
[GlobalClass]
public partial class ManaLoopSpell : SpellResource
{
	[Export] public float ManaAmount = 50f;
	[Export] public float Delay = 4f;

	// TODO: Make the 2 loop spells more interesting

	public ManaLoopSpell()
	{
		Name = "Mana Loop";
		Description =
			$"Traps an ally in a temporal loop. After {Delay}s, the loop resolves and restores {ManaAmount} mana.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 4f;
		School = SpellSchool.Chronomancy;
		Tags = SpellTags.Healing | SpellTags.Duration;
		RequiredSchoolPoints = 1;
		EffectType = EffectType.Helpful;
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
		ctx.Target?.ApplyEffect(new Effects.TimeLoopEffect(Delay, ctx.FinalValue)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}