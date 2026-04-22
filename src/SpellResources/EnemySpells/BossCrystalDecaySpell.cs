using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Crystal Knight's Crystal Decay attack.
/// Applies a <see cref="CrystalDecayEffect"/> to the target, dealing 10 damage
/// per second until the player casts Dispel on the afflicted party member.
/// </summary>
[GlobalClass]
public partial class BossCrystalDecaySpell : SpellResource
{
	public BossCrystalDecaySpell()
	{
		Name = "Crystal Decay";
		Description = "Afflicts the target with crystalline corruption, causing them to lose 10 health per second until cleansed.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/crystal-knight/crystal-decay.png");
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new CrystalDecayEffect
			{
				AbilityName = Name,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				Icon = Icon
			});
		}
	}
}