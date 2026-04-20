using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

public class ArcaneHungerTalent : ISpellModifier
{

	const float BonusPerExtraMana = 0.01f; // +1% per extra mana

	// Consume max X% of maximum mana
	const float MaxExtraConsumedMana = 0.10f;
	float _bonusMultiplier = 1.0f;
	public ModifierPriority Priority { get; } = ModifierPriority.BASE;
	public void OnBeforeCast(SpellContext context)
	{
		var availableExtraMana = context.Caster.CurrentMana - context.Spell.ManaCost;
		if (availableExtraMana <= 0) return;
		// Only consume up to the max percentage of max mana, even if the player has more than that available.
		var extraManaSpent = System.Math.Min(availableExtraMana, MaxExtraConsumedMana * context.Caster.MaxMana);
		_bonusMultiplier = 1f + extraManaSpent * BonusPerExtraMana;
		context.Caster.SpendMana(extraManaSpent);
	}
	public void OnCalculate(SpellContext context)
	{
		context.FinalValue *= _bonusMultiplier;
	}
	public void OnAfterCast(SpellContext context)
	{
	}
}