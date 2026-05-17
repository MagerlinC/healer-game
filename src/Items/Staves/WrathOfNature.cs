using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Wrath of Nature — Legendary staff.
///
/// Legendary effect: whenever a heal-over-time effect expires, it converts
/// into a damage-over-time on every living enemy, dealing the same per-tick
/// damage as the HoT healed per tick.
///
/// Implemented via a <see cref="WrathOfNatureWatcherEffect"/> that is applied
/// to every living party member on the first spell cast. The watcher
/// subscribes to each member's EffectApplied signal and silently replaces any
/// plain HoT with a <see cref="WrathOfNatureHoTWrapper"/> the instant it lands,
/// so the conversion fires on natural expiry regardless of which spell or
/// talent applied the HoT.
/// </summary>
public class WrathOfNature : EquippableItem
{
	public override string ItemId => "wrath_of_nature";

	public WrathOfNature()
	{
		Name = "Wrath of Nature";
		Description = "Whenever a heal-over-time effect expires, it converts into a " +
		              "damage-over-time on the boss, dealing the same damage per tick " +
		              "as it healed.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(9));
		SpellModifiers.Add(new ConvertHoTToDoTModifier());
	}

	// ── modifier ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Ensures a <see cref="WrathOfNatureWatcherEffect"/> is active on every
	/// living party member (and the caster) before each spell is cast.
	/// The watcher is infinite-duration so it only needs to be applied once
	/// per character per fight; subsequent OnBeforeCast calls are no-ops.
	/// </summary>
	class ConvertHoTToDoTModifier : ISpellModifier
	{
		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context)
		{
			EnsureWatcher(context.Caster);
			foreach (var member in context.Caster.CollectAlivePartyMembers())
				EnsureWatcher(member);
		}

		public void OnCalculate(SpellContext context)
		{
		}
		public void OnAfterCast(SpellContext context)
		{
		}

		static void EnsureWatcher(Character character)
		{
			if (character.GetEffectById(WrathOfNatureWatcherEffect.WatcherEffectId) == null)
				character.ApplyEffect(new WrathOfNatureWatcherEffect());
		}
	}
}