using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Holy;

public partial class ArchangelOfLightSpell : UltimateSpellResource
{

	static float BuffDuration = 10f;
	static float HealingIncrease = 30f;
	public ArchangelOfLightSpell()
	{
		Name = "Archangel of Light";
		Description =
			$"Embody the ultimate form of light for {BuffDuration:F0}s, making all spells free and increasing holy spell healing by {100 * HealingIncrease:F0}%. Overhealing a target causes the excess healing to heal all allies and damage all enemies.";
		ManaCost = 10f;
		CastTime = 2.0f;
		Cooldown = 0f;
		School = SpellSchool.Holy;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "holy/archangel-of-light.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new ArchangelOfLightEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Holy spells are free and do {100 * HealingIncrease:F0}% increased healing. Overhealing a target causes the excess healing to heal all allies and damage all enemies."
		});
	}

	// TODO: Allow casting after having spent a total of 50 mana on holy spells 
	public override bool CanCast(SpellContext ctx)
	{
	}
}