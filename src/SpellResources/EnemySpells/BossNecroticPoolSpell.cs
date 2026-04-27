using System.Collections.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Skull's area-based ability — Necrotic Pool.
///
/// The Skull marks the healer's current position with a swirling pool of
/// void energy. Unlike a one-shot explosion, the pool lingers for
/// <see cref="NecroticPool.LifetimeDuration"/> seconds and pulses
/// <see cref="DamagePerPulse"/> damage every second to anyone standing inside.
///
/// Counterplay: move out of the pool before the next pulse lands.
///
/// Targeting always resolves to the player (Healer) so the pool is centred
/// on their position — the same counterplay philosophy as
/// <see cref="BossDetonationZoneSpell"/>.
/// </summary>
[Godot.GlobalClass]
public partial class BossNecroticPoolSpell : SpellResource
{
	public float DamagePerPulse = 20f;

	public BossNecroticPoolSpell()
	{
		Name = "Necrotic Pool";
		Description =
			$"Summons a swirling pool of void energy beneath the healer that pulses for {DamagePerPulse} void damage per second to anyone standing inside. Lasts 4 seconds.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = false;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamagePerPulse;
	}

	/// <summary>
	/// Always resolves to the healer so the pool centres on their position.
	/// </summary>
	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive && c.CharacterName == GameConstants.HealerName)
				return new List<Character> { c };

		return new List<Character>();
	}

	/// <summary>
	/// Spawns a <see cref="NecroticPool"/> node at the healer's current position.
	/// The pool node handles its own countdown, rendering, and pulsed damage.
	/// </summary>
	public override void Apply(SpellContext ctx)
	{
		if (ctx.Targets.Count == 0) return;

		var player = ctx.Targets[0];
		var pool = new NecroticPool { DamagePerPulse = ctx.FinalValue };
		pool.GlobalPosition = player.GlobalPosition;

		// Add as sibling of the boss so it lives in the arena scene layer.
		ctx.Caster.GetParent().AddChild(pool);
	}
}