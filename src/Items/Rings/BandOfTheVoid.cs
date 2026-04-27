using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

/// <summary>
/// Band of the Void — Legendary ring.
///
/// Passive: damage-over-time effects applied by the player last
/// <see cref="_durationExtension"/> seconds longer.
///
/// Implemented by setting <see cref="SpellContext.EffectDurationBonus"/> during
/// the calculate phase for any spell tagged with both
/// <see cref="SpellTags.Damage"/> and <see cref="SpellTags.Duration"/>.
/// Spell Apply methods read <c>ctx.EffectDurationBonus</c> and add it to
/// their base effect duration.
/// </summary>
public class BandOfTheVoid : EquippableItem
{
	readonly float _durationExtension = 0.5f;
	public override string ItemId => "band_of_the_void";

	public BandOfTheVoid()
	{
		Name = "Band of the Void";
		Description = $"Damage over time effects last {_durationExtension}s longer";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(5));
		SpellModifiers.Add(new DurationExtenderModifier(_durationExtension));
	}

	class DurationExtenderModifier : ISpellModifier
	{
		readonly float _extension;

		public DurationExtenderModifier(float extension)
		{
			_extension = extension;
		}

		public ModifierPriority Priority { get; } = ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context) { }

		public void OnCalculate(SpellContext context)
		{
			// Extend only damage-over-time spells (Damage + Duration tags).
			// Pure damage spells (e.g. Soul Shatter) have Damage but not Duration.
			if (context.Tags.HasFlag(SpellTags.Damage) && context.Tags.HasFlag(SpellTags.Duration))
				context.EffectDurationBonus += _extension;
		}

		public void OnAfterCast(SpellContext context) { }
	}
}
