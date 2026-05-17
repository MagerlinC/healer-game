using System;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// The Heart of Light — Legendary amulet.
///
/// Passive: while any Holy-school effect is active on a friendly character,
/// that character takes <see cref="DamageReductionAmount"/> less damage.
///
/// Implemented via an invisible <see cref="HolyProtectionWatcherEffect"/> watcher
/// placed on every living party member on the first spell cast. The watcher
/// subscribes to EffectApplied / EffectRemoved signals and toggles a visible
/// <see cref="HolyProtectionActiveBuff"/> (which carries the icon and the
/// actual stat modifier) whenever Holy effects come and go — so the indicator
/// on the party frame only lights up when the protection is genuinely active.
/// </summary>
public class TheHeartOfLight : EquippableItem
{
	static readonly float DamageReductionAmount = 0.3f;
	public override string ItemId => "the_heart_of_light";

	public TheHeartOfLight()
	{
		Name = "The Heart of Light";
		Description =
			$"While any holy effect is active on the target, they take {Math.Round(DamageReductionAmount * 100)}% less damage.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(2));
		SpellModifiers.Add(new DamageReductionModifier(DamageReductionAmount));
	}

	/// <summary>
	/// Ensures a <see cref="HolyProtectionWatcherEffect"/> watcher is active on the
	/// caster and every living party member before each spell is cast.
	/// The watcher is infinite-duration so this is a no-op for characters that
	/// already have one, and it runs before <c>Apply()</c> so the watcher is
	/// connected in time to catch any Holy effects the spell is about to apply.
	/// </summary>
	class DamageReductionModifier : ISpellModifier
	{
		readonly float _damageReductionAmount;

		public DamageReductionModifier(float damageReductionAmount)
		{
			_damageReductionAmount = damageReductionAmount;
		}

		public ModifierPriority Priority { get; } = ModifierPriority.BASE;

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

		void EnsureWatcher(Character character)
		{
			if (!character.IsFriendly) return;
			if (character.GetEffectById(HolyProtectionWatcherEffect.WatcherEffectId) == null)
				character.ApplyEffect(new HolyProtectionWatcherEffect(_damageReductionAmount));
		}
	}
}