using Godot;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class HealSpellResource : SpellResource
{
	[Export] public float HealAmount = 25f;

	public override void Act(Character _caster, Character target)
	{
		target.Heal(HealAmount);
	}

	public HealSpellResource()
	{
		Name = "Heal";
		Description = $"Restores {HealAmount} health to the target.";
		ManaCost = 10f;
		CastTime = 1.5f;
	}
}