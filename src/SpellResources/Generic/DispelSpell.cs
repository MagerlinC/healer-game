using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Generic;

/// <summary>
/// Removes all harmful effects from a single target.
///
/// Always available — lives in the player's generic action bar and cannot be
/// removed from the loadout. Free to cast (no mana cost), with a moderate
/// cooldown to prevent constant cleansing.
/// </summary>
[GlobalClass]
public partial class DispelSpell : SpellResource
{
	public DispelSpell()
	{
		Name = "Dispel";
		Description = "Cleanses the target of all harmful effects.";
		ManaCost = 0f;
		CastTime = 0f;
		Cooldown = 10f;
		School = SpellSchool.Generic;
		EffectType = EffectType.Helpful;
		Tags = SpellTags.None;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "generic/dispel.png");
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.RemoveHarmfulEffects();
	}
}