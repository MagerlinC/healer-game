using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

public class ChainOfReflection : EquippableItem
{
	static readonly float _damageBonus = 0.25f;
	static readonly float _damageReflected = 0.25f;
	public override string ItemId => "chain_of_reflection";

	public ChainOfReflection()
	{
		Name = "Chain of Reflection";
		Description =
			$"Deal {_damageBonus * 100:F0}% increased damage, but {_damageReflected}% of damage dealt is reflected to you as void damage.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(6));
		CharacterModifiers.Add(new ReflectionModifier());
	}

	class ReflectionModifier : ICharacterModifier, ISpellModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedDamage += _damageBonus;
		}


		public ModifierPriority Priority { get; }
		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
		}
		public void OnAfterCast(SpellContext context)
		{
			var damageDealt = context.FinalValue;
			context.Caster.TakeDamage(damageDealt * _damageReflected);
			context.Caster.RaiseFloatingCombatText(damageDealt, false, (int)context.Spell.School, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = context.Caster.CharacterName,
				TargetName = context.Caster.CharacterName,
				AbilityName = "Chain of Reflection",
				Amount = damageDealt,
				Description = "Reflected damage from wearing the Chain of Reflection",
				Type = CombatEventType.Damage,
				IsCrit = false
			});
		}
	}
}