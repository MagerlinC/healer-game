using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Accelerates time around the caster, granting a powerful cast-speed buff
/// for a short burst. Dramatically reduces cast times for the duration,
/// enabling rapid back-to-back healing or damage casts in critical moments.
///
/// Unlike Time Warp (which buffs the whole party for a moderate bonus),
/// Haste is a self-only burst of extreme speed — best used when a burst
/// of rapid casts is needed immediately.
/// </summary>
[GlobalClass]
public partial class HasteSpell : SpellResource
{
	[Export] public float SpeedBonus = 0.40f;
	[Export] public float BuffDuration = 8f;

	public HasteSpell()
	{
		Name = "Haste";
		Description =
			$"Accelerate your own flow of time, increasing your haste and movement speed by {SpeedBonus:F0}% for {BuffDuration}s.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 15f;
		School = SpellSchool.Chronomancy;
		Tags = SpellTags.Duration;
		RequiredSchoolPoints = 0;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "healer/healer2.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster.ApplyEffect(new HasteEffect(BuffDuration, SpeedBonus)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}