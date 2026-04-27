using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// The Lifebinder's Covenant — Legendary amulet that can drop from any boss.
/// An ancient pact sealed between healers and the spirits of those they protect.
/// Every time the wearer's magic washes over the entire party, the spirits
/// return a thread of mana for each soul touched.
///
/// Legendary effect: Group healing spells restore 8 mana per friendly target healed.
/// E.g. Holy Nova hitting all 4 party members restores 32 mana.
///
/// Implemented by counting friendly targets in OnAfterCast for any spell
/// carrying both the GroupSpell and Healing tags.
/// </summary>
public class TheLifebindersCovenant : EquippableItem
{
	static readonly float _manaPerTarget = 8f;
	public override string ItemId => "the_lifebinders_covenant";

	public TheLifebindersCovenant()
	{
		Name = "The Lifebinder's Covenant";
		Description = $"Group healing spells restore {_manaPerTarget} mana per friendly target healed.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(4));
		SpellModifiers.Add(new CovenantModifier(_manaPerTarget));
	}

	class CovenantModifier : ISpellModifier
	{
		readonly float _manaPerTarget;

		public CovenantModifier(float manaPerTarget)
		{
			_manaPerTarget = manaPerTarget;
		}

		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context) { }

		public void OnCalculate(SpellContext context) { }

		public void OnAfterCast(SpellContext context)
		{
			// Only trigger on group healing spells.
			if (!context.Tags.HasFlag(SpellTags.Healing)) return;
			if (!context.Tags.HasFlag(SpellTags.GroupSpell)) return;

			// Count how many friendly characters were healed.
			int friendlyTargets = context.Targets.Count(t => t.IsFriendly);
			if (friendlyTargets > 0)
				context.Caster.RestoreMana(_manaPerTarget * friendlyTargets);
		}
	}
}
