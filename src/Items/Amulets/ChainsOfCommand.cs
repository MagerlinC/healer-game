using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

public partial class ChainsOfCommand : EquippableItem
{
	static readonly float PartyDamageBonus = 0.25f;

	public override string ItemId => "chains_of_command";
	public ChainsOfCommand()
	{

		Name = "Chains of Command";
		Description = $"Party members deal {100 * PartyDamageBonus:F0}% more damage";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(8));
		SpellModifiers.Add(new PartyBoostingModifier());
	}

	// IPartySpellModifier causes SpellPipeline to inject this modifier into
	// NPC party member casts, so the bonus applies to the Assassin, Templar,
	// and Wizard even though they don't equip items themselves.
	class PartyBoostingModifier : IPartySpellModifier
	{
		public ModifierPriority Priority { get; } = ModifierPriority.BASE;
		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
			// Only boost damage spells — don't inflate heals or utility casts.
			if (context.Tags.HasFlag(SpellTags.Damage))
				context.FinalValue *= 1f + PartyDamageBonus;
		}
		public void OnAfterCast(SpellContext context)
		{
		}
	}
}