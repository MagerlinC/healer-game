using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

public partial class CrimsonAmulet : EquippableItem
{
	static readonly float LeechRate = 0.1f;

	public override string ItemId => "crimson_amulet";
	public CrimsonAmulet()
	{

		Name = "Crimson Amulet";
		Description = $"{100 * LeechRate:F0}% of damage leeched as life.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(7));
		SpellModifiers.Add(new LeechModifier());
	}

	class LeechModifier : ISpellModifier
	{

		public ModifierPriority Priority { get; } = ModifierPriority.BASE;
		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
		}
		public void OnAfterCast(SpellContext context)
		{
			if (context.Spell.Tags.HasFlag(SpellTags.Damage) && !context.Target.IsFriendly)
			{
				context.Caster.Heal(context.FinalValue * LeechRate);
			}
		}
	}
}