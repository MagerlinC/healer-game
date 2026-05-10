using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Nature;

public partial class OneWithNatureSpell : SpellResource
{

	static float BuffDuration = 10f;
	static float HealingIncrease = 20f;
	public OneWithNatureSpell()
	{
		Name = "One With Nature";
		Description =
			$"Become one with nature for {BuffDuration:F0}s, making all nature spells instant cast and increasing their healing by {100 * HealingIncrease:F0}%. Directly healing a target causes all beneficial nature effects on that target to refresh their duration.";
		ManaCost = 10f;
		CastTime = 2.0f;
		Cooldown = 0f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/one-with-nature.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Caster?.ApplyEffect(new OneWithNatureEffect(BuffDuration, HealingIncrease)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description =
				$"Nature healing spells are instant and do {100 * HealingIncrease:F0}% increased healing. Beneficial nature effects on targets you directly heal refresh their duration."
		});
	}
}