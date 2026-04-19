using Godot;
using healerfantasy;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class HealOverTimeSpellResource : SpellResource
{
	[Export] public float HealPerTick    = 5f;
	[Export] public float EffectDuration = 10f;
	[Export] public float TickInterval   = 1f;

	public override void Act(Character _caster, Character target)
	{
		target.ApplyEffect(new HealOverTimeEffect(HealPerTick, EffectDuration, TickInterval));
	}

	public HealOverTimeSpellResource()
	{
		Name        = "Renew";
		Description = $"Heals the target for {HealPerTick} every {TickInterval}s for {EffectDuration}s.";
		ManaCost    = 10f;
		CastTime    = 1.5f;
		Icon        = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer3.png");
	}
}
