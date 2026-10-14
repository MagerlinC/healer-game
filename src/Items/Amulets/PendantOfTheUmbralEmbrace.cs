using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Pendant of the Umbral Embrace — Legendary amulet that can drop from any boss.
/// The umbral does not only destroy — it hungers for balance. This pendant channels
/// the dark energy released by umbral spells back into the wearer's healing arts.
/// Striking with the dark charges the pendant; the accumulated power is then
/// released into the very next healing spell.
///
/// Legendary effect: After casting an Umbra damage spell, the next healing spell
/// you cast heals for 30% more. The bonus is consumed on use.
///
/// Implemented by persisting a boolean flag on the modifier instance between
/// casts. OnAfterCast sets the flag when a Umbra damage spell lands;
/// OnCalculate multiplies FinalValue and clears the flag on the next heal.
///
/// UI: when charged, fires <see cref="ItemEffectBus.Activate"/> so the healer's
/// <see cref="ItemEffectBar"/> shows a gold indicator badge; fires
/// <see cref="ItemEffectBus.Deactivate"/> when the bonus is consumed.
/// </summary>
public class PendantOfTheUmbralEmbrace : EquippableItem
{
	static readonly float _healingBonus = 0.30f;
	public override string ItemId => "pendant_of_the_voids_embrace";

	public PendantOfTheUmbralEmbrace()
	{
		Name = "Pendant of the Umbral Embrace";
		Description = $"After casting an Umbra damage spell, the next restoring spell restores {Math.Round(_healingBonus * 100)}% more light.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(5));
		SpellModifiers.Add(new UmbraSynergyModifier(
			_healingBonus,
			Icon,
			Name,
			$"Next restoring spell restores {Math.Round(_healingBonus * 100)}% more light."
		));
	}

	/// <summary>
	/// Tracks whether a Umbra damage spell was cast since the last heal.
	/// State lives on this modifier instance, which is held by the player for
	/// the duration of the run — surviving between individual spell casts.
	/// </summary>
	class UmbraSynergyModifier : ISpellModifier
	{
		readonly float _bonus;
		readonly Texture2D? _icon;
		readonly string _displayName;
		readonly string _indicatorDescription;

		// Stable ID used to add/remove the indicator without name collisions.
		const string EffectId = "pendant_of_the_voids_embrace_charged";

		bool _pendingBonus = false;

		public UmbraSynergyModifier(float bonus, Texture2D? icon, string displayName, string indicatorDescription)
		{
			_bonus = bonus;
			_icon = icon;
			_displayName = displayName;
			_indicatorDescription = indicatorDescription;
		}

		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context)
		{
		}

		public void OnCalculate(SpellContext context)
		{
			// If a Umbra damage spell was cast recently, empower the next heal.
			if (_pendingBonus && context.Tags.HasFlag(SpellTags.Healing))
			{
				context.FinalValue *= 1f + _bonus;
				_pendingBonus = false; // consumed — must cast another Void spell to re-charge
				ItemEffectBus.Deactivate(EffectId);
			}
		}

		public void OnAfterCast(SpellContext context)
		{
			// Charge the pendant whenever a Umbra damage spell lands.
			if (context.Spell.School.HasFlag(SpellSchool.Umbra) && context.Tags.HasFlag(SpellTags.Damage))
			{
				_pendingBonus = true;
				ItemEffectBus.Activate(EffectId, _icon, _displayName, _indicatorDescription);
			}
		}
	}
}