using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

public partial class RewindSpell : SpellResource
{

	[Export] public float EffectDuration = 4f;
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