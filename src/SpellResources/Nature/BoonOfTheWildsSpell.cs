using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Infuses the target with nature's toughness, temporarily hardening their skin
/// to reduce all incoming damage by 25% for the duration.
///
/// Barkskin is a pure defensive cooldown — cast it on a party member (or yourself)
/// just before a heavy-hitting boss ability to dramatically reduce the incoming
/// damage and take pressure off your healing spells.
/// </summary>
[GlobalClass]
public partial class BoonOfTheWildsSpell : SpellResource
{
	[Export] public float HealthIncrease = 0.20f;
	[Export] public float BuffDuration = 8f;

	public BoonOfTheWildsSpell()
	{
		Name = "Boon of the Wilds";
		Description =
			$"Infuse the target with the energy of nature, temporarily increasing max health by {HealthIncrease * 100:F0}% for {BuffDuration}s.";
		ManaCost = 7f;
		CastTime = 0.0f;
		Cooldown = 10f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing | SpellTags.Nature;
		RequiredSchoolPoints = 1;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "healer/healer6.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new BoonOfTheWildsEffect(BuffDuration, HealthIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}