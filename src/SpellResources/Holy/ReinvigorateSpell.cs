using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

public partial class ReinvigorateSpell : SpellResource
{
	float HealAmount = 10f;
	public ReinvigorateSpell()
	{
		Name = "Reinvigorate";
		Description =
			$"Instantly restores {HealAmount} health to the target and refreshes the duration of all buffs which were applied by the caster on the target.";
		ManaCost = 5f;
		CastTime = 0.0f;
		Tags = SpellTags.Healing;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer4.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target.Heal(HealAmount);
		ctx.Target.RefreshAllPlayerEffects();
	}
}