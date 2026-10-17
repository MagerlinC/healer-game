using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class MothswarmSpell : SpellResource
{
	[Export] public float DamagePerTick = 10f;
	[Export] public float HealPerTick = 4f;
	[Export] public float BuffDuration = 8f;

	public MothswarmSpell()
	{
		Name = "Mothswarm";
		Description =
			$"Send out a swarm of moths, dealing {DamagePerTick:F0} ember damage per second for {BuffDuration}s. When this effect expires, the moths scatter to all allies, restoring {HealPerTick:F0} light per second for {BuffDuration}s.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 12f;
		School = SpellSchool.Ember;
		Tags = SpellTags.Damage | SpellTags.Healing | SpellTags.Duration;
		RequiredSchoolPoints = 2;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/swarm-of-locusts.png");
	}

	public override float GetBaseValue()
	{
		return DamagePerTick;
	}

	public override SpellTags SecondaryKind => SpellTags.Healing;

	public override float GetSecondaryBaseValue()
	{
		return HealPerTick;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new MothswarmDamageEffect(BuffDuration, ctx.FinalValue, ctx.SecondaryFinalValue)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}