using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Countess's Crimson Curse — a wicked hex that shreds the target's
/// ability to receive healing while draining their blood.
///
/// Applies <see cref="CrimsonCurseEffect"/> which:
///   • Deals 15 damage per second for 10 seconds.
///   • Reduces all healing received by the target by 50%.
///
/// Dispellable — cleansing it removes both the DoT and the healing reduction immediately.
/// </summary>
[GlobalClass]
public partial class BossCountessCrimsonCurseSpell : SpellResource
{
	const float DamagePerTick = 15f;

	public BossCountessCrimsonCurseSpell()
	{
		Name = "Crimson Curse";
		Description =
			"A vampiric hex that saps the target's blood and corrupts healing magic, " +
			"dealing 15 damage per second and reducing healing received by 50% for 10 seconds. Dispellable.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-countess/crimson-curse.png");
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new CrimsonCurseEffect(DamagePerTick)
			{
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				Icon = Icon
			});
		}
	}
}