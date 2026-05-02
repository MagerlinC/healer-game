using Godot;

namespace healerfantasy.Effects;

/// <summary>
/// Applied to a party member while a <see cref="VinesEnemy"/> is latched onto them.
/// Purely cosmetic — shows an indicator on the party frame so the player knows
/// who is grasped.  Removed automatically when the vine is killed or despawned.
///
/// The <see cref="CharacterEffect.EffectId"/> is unique per vine instance so
/// multiple vines can each grapple a different (or even the same) target without
/// the effects colliding and deduplicating each other.
/// </summary>
public partial class GraspedByVineEffect : CharacterEffect
{
	public GraspedByVineEffect(string vinesInstanceName)
		: base(GameConstants.InfiniteDuration, 0f)
	{
		EffectId = $"GraspedByVine_{vinesInstanceName}";
		AbilityName = "Grasped by a Vine";
		Description = "Vines have latched on, dealing 12 damage per second until they are killed.";
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/vines/grasped-by-vine.png");
		IsHarmful = true;
	}
}