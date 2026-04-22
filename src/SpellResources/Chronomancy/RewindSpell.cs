using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

public partial class RewindSpell : SpellResource
{

	[Export] public float EffectDuration = 3f;

	public RewindSpell()
	{
		Name = "Rewind";
		Description =
			$"Store the target's current health in time, causing its health to rewind back after {EffectDuration} seconds.";
		ManaCost = 6f;
		CastTime = 0.0f;
		Cooldown = 8f;
		Tags = SpellTags.Healing | SpellTags.Duration;
		EffectType = EffectType.Helpful;
		School = SpellSchool.Chronomancy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/rewind.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new Effects.RewindEffect(EffectDuration)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name
		});
	}
}