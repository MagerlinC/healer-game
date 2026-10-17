using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items;

public enum ItemRarity
{
	Rare,
	Epic,
	Legendary
}

public enum EquipSlot
{
	Staff,
	Ring1,
	Ring2,
	Amulet
}

/// <summary>
/// Abstract base for all equippable items.
///
/// Items carry optional <see cref="ICharacterModifier"/>s (stat bonuses) and
/// <see cref="ISpellModifier"/>s (gameplay-altering legendary effects), following
/// the same pattern as <see cref="SpellSystem.Talent"/>.
///
/// Rare and Epic items provide only stat bonuses via CharacterModifiers.
/// Legendary items may additionally carry SpellModifiers for unique effects.
///
/// Items are found during a run (dropped by bosses), can be equipped at any
/// time via the Armory, and are lost at run's end after being logged in
/// RunHistory.
///
/// To add a new item type (e.g. rings, amulets):
///   1. Add a new value to <see cref="EquipSlot"/>.
///   2. Subclass EquippableItem, set Slot to the new value, add modifiers.
///   3. Register the factory in <see cref="ItemRegistry"/>.
/// </summary>
public abstract class EquippableItem
{
	/// <summary>
	/// Unique string identifier used for serialisation and run-history logging.
	/// Must be stable across code changes (treat as a data key, not a display name).
	/// </summary>
	public abstract string ItemId { get; }

	public string Name { get; protected init; } = string.Empty;
	public string Description { get; protected init; } = string.Empty;

	Texture2D? _icon;

	/// <summary>
	/// The item's icon: the drawn Last Light icon (see <c>LastLightIcons</c>), falling back
	/// to the art loaded at construction time from <see cref="AssetConstants"/>.
	/// </summary>
	public Texture2D? Icon
	{
		get => LastLight.Hud.LastLightIcons.ForItem(this) ?? _icon;
		protected set => _icon = value;
	}

	public ItemRarity Rarity { get; protected init; }

	/// <summary>
	/// The spell school this item is built around, if any (e.g. a Pyre-only staff).
	/// Items tied to a school only drop and appear at the merchant once that school
	/// is unlocked (see <see cref="PlayerProgressStore.IsSchoolUnlocked"/>).
	/// </summary>
	public SpellSchool? School { get; protected init; }

	public EquipSlot Slot { get; protected init; }

	/// <summary>
	/// Applied during <see cref="Character.GetCharacterStats"/> — same pipeline as Talent
	/// character modifiers. Use for passive stat bonuses (MaxMana, CritChance, etc.).
	/// </summary>
	public List<ICharacterModifier> CharacterModifiers { get; } = new();

	/// <summary>
	/// Injected into <see cref="Character.GetSpellModifiers"/> — same pipeline as Talent
	/// spell modifiers. Use only for Legendary items with gameplay-altering effects.
	/// </summary>
	public List<ISpellModifier> SpellModifiers { get; } = new();
}