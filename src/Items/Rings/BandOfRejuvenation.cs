using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

public class BandOfRejuvenation : EquippableItem
{
	readonly float _durationExtension = 3f;
	public override string ItemId => "band_of_rejuvenation";

	public BandOfRejuvenation()
	{
		Name = "Band of Rejuvenation";
		Description = $"Heal over time effects last {_durationExtension}s longer";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(2));
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

		public void OnBeforeCast(SpellContext context)
		{
		}

		public void OnCalculate(SpellContext context)
		{
			if (context.Tags.HasFlag(SpellTags.Healing) && context.Tags.HasFlag(SpellTags.Duration))
				context.EffectDurationBonus += _extension;
		}

		public void OnAfterCast(SpellContext context)
		{
		}
	}
}