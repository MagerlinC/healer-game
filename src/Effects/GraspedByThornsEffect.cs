using Godot;

namespace healerfantasy.Effects;

/// <summary>
/// Applied to a party member while a <see cref="ThornsEnemy"/> is latched onto them.
/// Purely cosmetic — shows an indicator on the party frame so the player knows
/// who is grasped.  Removed automatically when the thorn is killed or despawned.
///
/// The <see cref="CharacterEffect.EffectId"/> is unique per thorn instance so
/// multiple thorns can each grapple a different (or even the same) target without
/// the effects colliding and deduplicating each other.
/// </summary>
public partial class GraspedByThornsEffect : CharacterEffect
{
	public GraspedByThornsEffect(string thornsInstanceName)
		: base(GameConstants.InfiniteDuration, 0f)
	{
		EffectId = $"GraspedByThorns_{thornsInstanceName}";
		AbilityName = "Grasped by Thorns";
		Description = "Grasping thorns have coiled around them, dealing 12 damage per second until they are cut down.";
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/vines/grasped-by-vine.png");
		IsHarmful = true;
	}
}