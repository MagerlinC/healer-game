using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class SwarmOfTheForest : SpellResource
{
	[Export] public float ValuePerTick = 10f;
	[Export] public float BuffDuration = 8f;

	public SwarmOfTheForest()
	{
		Name = "Swarm of the Forest";
		Description =
			$"Send out a swarm of insects, dealing {ValuePerTick:F0} nature damage per second for {BuffDuration}s. When this effect expires, the swarm spreads to all allies, healing them for {ValuePerTick:F0} per second for {BuffDuration}s.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 12f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Damage | SpellTags.Healing;
		RequiredSchoolPoints = 2;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/swarm-of-locusts.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new SwarmOfTheForestDamageEffect(BuffDuration, ValuePerTick)
		{
			Icon = ctx.Spell.Icon,
			School = School,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}