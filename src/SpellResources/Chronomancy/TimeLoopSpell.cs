using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Places a temporal loop on a single ally. After the duration elapses, the
/// loop resolves and instantly heals them for a burst of health — effectively
/// a time-delayed safety net.
///
/// Useful for pre-empting incoming damage: cast it on a tank before a big hit
/// and the heal fires automatically at the right moment.
/// </summary>
[GlobalClass]
public partial class TimeLoopSpell : SpellResource
{
	[Export] public float HealAmount = 35f;
	[Export] public float Delay = 5f;

	public TimeLoopSpell()
	{
		Name = "Time Loop";
		Description =
			$"Traps an ally in a temporal loop. After {Delay}s, the loop resolves and heals them for {HealAmount} HP.";
		ManaCost = 8f;
		CastTime = 0.0f;
		School = SpellSchool.Chronomancy;
		Tags = SpellTags.Healing | SpellTags.Duration;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/chronomancy/time-loop.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new Effects.TimeLoopEffect(Delay, ctx.FinalValue)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name
		});
	}
}