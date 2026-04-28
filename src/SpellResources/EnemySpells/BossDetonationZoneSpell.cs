using System.Collections.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Demon Slime's Detonation Zone — a boss-exclusive ability.
///
/// The Slime marks the healer's current position with a glowing red zone.
/// After <see cref="DetonationZone.FuseDuration"/> seconds the zone explodes,
/// dealing heavy damage to every party member still standing inside it.
///
/// Counterplay: move out of the marked area before it detonates.
///
/// Targeting is always the player (Healer) regardless of the explicit target
/// passed by the caster — the <see cref="ResolveTargets"/> override handles
/// this so the <see cref="DetonationZone"/> node is placed at the right spot.
/// </summary>
[Godot.GlobalClass]
public partial class BossDetonationZoneSpell : SpellResource
{
	public float DamageAmount = 70f;

	public BossDetonationZoneSpell()
	{
		Name = "Detonation Zone";
		Description = "Marks the healer's position with an unstable zone of volatile energy. "
		              + "After 2.5 seconds the zone detonates, dealing heavy damage to anyone still inside.";
		// Duration tag prevents the pipeline from emitting instant floating combat
		// text or a combat-log record — the DetonationZone node handles those itself
		// when it actually detonates.
		Tags = SpellTags.Damage | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = false;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>
	/// Always resolves to the player (Healer), regardless of who the caster
	/// nominally aimed at. This ensures the zone is centred on the player's
	/// position so the counterplay is purely about moving out of it.
	/// </summary>
	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive && c.CharacterName == GameConstants.HealerName)
				return new List<Character> { c };

		// Fallback — should not happen in normal play.
		return new List<Character>();
	}

	/// <summary>
	/// Spawns a <see cref="DetonationZone"/> node at the player's current
	/// world position. The zone node owns its countdown, visual, and detonation
	/// logic independently of any character effect.
	/// </summary>
	public override void Apply(SpellContext ctx)
	{
		if (ctx.Targets.Count == 0) return;

		var player = ctx.Targets[0];
		var zone = new DetonationZone { DamageAmount = ctx.FinalValue };
		zone.GlobalPosition = player.GlobalPosition;

		// Add as a sibling of the boss so it lives in the same scene layer.
		ctx.Caster.GetParent().AddChild(zone);
	}
}