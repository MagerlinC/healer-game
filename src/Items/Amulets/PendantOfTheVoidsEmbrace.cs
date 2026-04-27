using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Pendant of the Void's Embrace — Legendary amulet that can drop from any boss.
/// The void does not only destroy — it hungers for balance. This pendant channels
/// the dark energy released by void spells back into the wearer's healing arts.
/// Striking with the void charges the pendant; the accumulated power is then
/// released into the very next healing spell.
///
/// Legendary effect: After casting a Void damage spell, the next healing spell
/// you cast heals for 30% more. The bonus is consumed on use.
///
/// Implemented by persisting a boolean flag on the modifier instance between
/// casts. OnAfterCast sets the flag when a Void damage spell lands;
/// OnCalculate multiplies FinalValue and clears the flag on the next heal.
/// </summary>
public class PendantOfTheVoidsEmbrace : EquippableItem
{
	static readonly float _healingBonus = 0.30f;
	public override string ItemId => "pendant_of_the_voids_embrace";

	public PendantOfTheVoidsEmbrace()
	{
		Name = "Pendant of the Void's Embrace";
		Description = $"After casting a Void damage spell, the next healing spell heals for {_healingBonus * 100}% more.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(5));
		SpellModifiers.Add(new VoidSynergyModifier(_healingBonus));
	}

	/// <summary>
	/// Tracks whether a Void damage spell was cast since the last heal.
	/// State lives on this modifier instance, which is held by the player for
	/// the duration of the run — surviving between individual spell casts.
	/// </summary>
	class VoidSynergyModifier : ISpellModifier
	{
		readonly float _bonus;
		bool _pendingBonus = false;

		public VoidSynergyModifier(float bonus)
		{
			_bonus = bonus;
		}

		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context) { }

		public void OnCalculate(SpellContext context)
		{
			// If a Void damage spell was cast recently, empower the next heal.
			if (_pendingBonus && context.Tags.HasFlag(SpellTags.Healing))
			{
				context.FinalValue *= 1f + _bonus;
				_pendingBonus = false; // consumed — must cast another Void spell to re-charge
			}
		}

		public void OnAfterCast(SpellContext context)
		{
			// Charge the pendant whenever a Void damage spell lands.
			if (context.Tags.HasFlag(SpellTags.Void) && context.Tags.HasFlag(SpellTags.Damage))
				_pendingBonus = true;
		}
	}
}
