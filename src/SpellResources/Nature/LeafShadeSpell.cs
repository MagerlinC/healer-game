using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class LeafShadeSpell : SpellResource
{
	[Export] public float DamageReduction = 0.25f;
	[Export] public float BuffDuration = 8f;

	public LeafShadeSpell()
	{
		Name = "Leaf shade";
		Description =
			$"Harden the target's skin with nature magic, reducing all damage they take by {(int)(DamageReduction * 100)}% for {BuffDuration}s.";
		ManaCost = 7f;
		CastTime = 0.0f;
		Cooldown = 10f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 2;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/seeds-of-spring.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new LeafShadeEffect(BuffDuration, DamageReduction)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}