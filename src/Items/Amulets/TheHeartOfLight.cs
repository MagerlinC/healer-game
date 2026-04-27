using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// The Heart of Light — Legendary amulet.
///
/// Passive: while any Holy-school effect is active on a friendly character,
/// that character takes <see cref="DamageReductionAmount"/> less damage.
///
/// Implemented by applying a <see cref="HolyProtectionEffect"/> to each
/// friendly target whenever the player casts a Holy spell. The effect
/// is a permanent passive tracker that checks for active Holy effects
/// at the time damage is computed, using <see cref="IConditionalCharacterModifier"/>.
/// </summary>
public class TheHeartOfLight : EquippableItem
{
	static readonly float DamageReductionAmount = 0.3f;
	public override string ItemId => "the_heart_of_light";

	public TheHeartOfLight()
	{
		Name = "The Heart of Light";
		Description = $"While any holy effect is active on the target, they take {(int)(DamageReductionAmount * 100)}% less damage.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(2));
		SpellModifiers.Add(new DamageReductionModifier(DamageReductionAmount));
	}

	/// <summary>
	/// After every Holy spell cast, ensures each friendly target has a
	/// <see cref="HolyProtectionEffect"/> tracker installed. The tracker
	/// conditionally reduces damage taken while Holy effects are active on
	/// the character. It is idempotent — re-applying it to an already-protected
	/// character is a no-op (same <see cref="CharacterEffect.EffectId"/>).
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
		}

		public void OnCalculate(SpellContext context)
		{
		}

		public void OnAfterCast(SpellContext context)
		{
			// Only care about Holy spells — they are what create Holy effects.
			if (context.Spell.School != SpellSchool.Holy) return;

			// Ensure every friendly target has the protection tracker.
			foreach (var target in context.Targets)
			{
				if (target.IsFriendly)
					target.ApplyEffect(new HolyProtectionEffect(_damageReductionAmount));
			}

			// Also protect the caster (the healer) — Holy group heals may not
			// list them explicitly as a target.
			if (context.Caster.IsFriendly)
				context.Caster.ApplyEffect(new HolyProtectionEffect(_damageReductionAmount));
		}
	}
}