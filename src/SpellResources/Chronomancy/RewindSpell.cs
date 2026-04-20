using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

public partial class RewindSpell : SpellResource
{

	[Export] public float EffectDuration = 4f;

	public RewindSpell()
	{
		Name = "Rewind";
		Description =
			$"Store the target's current health in time, causing its health to rewind back after {EffectDuration} seconds.";
		ManaCost = 6f;
		CastTime = 0.0f;
		Tags = SpellTags.Damage | SpellTags.Duration;
		TargetType = TargetType.Enemy;
		School = SpellSchool.Chronomancy;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/chronomancy/rewind.png");
	}

	public override void Apply(SpellContext ctx)
	{
		// ctx.FinalValue is the modifier-adjusted per-tick heal amount.
		ctx.Target?.ApplyEffect(new Effects.RewindEffect(EffectDuration)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name
		});
	}
}