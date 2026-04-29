using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Burst of Winter.
///
/// 1.5-second cast followed by a nova explosion centred on the Queen.
/// On resolution:
///   1. A <see cref="BurstOfWinterNovaEffect"/> visual bursts outward from
///      the Queen's position (purely cosmetic — expands and fades in ~0.4 s).
///   2. Every living party member takes <see cref="Damage"/> frost damage.
///   3. Every living party member is knocked outward from the Queen, launched
///      toward the edge of the arena. If an <see cref="PartyMember.ArenaBoundary"/>
///      is active they land at (or just inside) the ring boundary.
/// </summary>
[GlobalClass]
public partial class BossQueenBurstOfWinterSpell : SpellResource
{
	// ── tunables ─────────────────────────────────────────────────────────────
	/// <summary>Flat damage dealt to each party member on detonation.</summary>
	public float Damage = 40f;

	/// <summary>
	/// Fallback knockback distance (world pixels) used only when no arena
	/// boundary is active (e.g. during dev testing outside the Frozen Peak).
	/// </summary>
	const float FallbackKnockbackDistance = 200f;

	// ── config ────────────────────────────────────────────────────────────────
	/// <summary>Reference to the Queen — must be set before <c>SpellPipeline.Cast</c>.</summary>
	public Character Boss { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────
	public BossQueenBurstOfWinterSpell()
	{
		Name = "Burst of Winter";
		Description = "The Queen releases a devastating nova of frozen energy, " +
		              "dealing damage to all nearby enemies and blasting them to the " +
		              "edges of the frozen arena.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 1.5f;
		EffectType = EffectType.Harmful;
	}

	// ── Apply ─────────────────────────────────────────────────────────────────
	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[BurstOfWinter] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[BurstOfWinter] Boss has no parent — cannot spawn nova.");
			return;
		}

		// ── Visual ──────────────────────────────────────────────────────────
		var nova = new BurstOfWinterNovaEffect();
		nova.GlobalPosition = Boss.GlobalPosition;
		parent.AddChild(nova);

		// ── Damage + knockback ───────────────────────────────────────────────
		foreach (var node in Boss.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;

			// Damage
			target.TakeDamage(Damage);
			target.RaiseFloatingCombatText(Damage, false, (int)SpellSchool.Generic, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = GameConstants.FrozenPeakBossName,
				TargetName = target.CharacterName,
				AbilityName = "Burst of Winter",
				Amount = Damage,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Blasted by the Queen's nova of frozen energy."
			});

			// Knockback — push outward from the boss, then clamp to arena.
			var fromBoss = target.GlobalPosition - Boss.GlobalPosition;
			var dir = fromBoss.LengthSquared() > 0.01f
				? fromBoss.Normalized()
				: Vector2.FromAngle(GD.Randf() * Mathf.Tau); // fallback if standing on boss

			// Compute the destination: the arena boundary edge in the knockback
			// direction, inset a few pixels so the character lands cleanly inside
			// the one-sided physics walls and can always walk back in afterward.
			// Falls back to a short fixed distance if no boundary is active.
			var destination = PartyMember.GetArenaBoundaryPoint(dir)
			                  ?? target.GlobalPosition + dir * FallbackKnockbackDistance;

			if (target is PartyMember pm)
			{
				// NPC: teleport to destination + stun so they don't immediately walk back.
				pm.KnockbackTo(destination);
			}
			else
			{
				// Player: place at destination; they control their own recovery.
				target.GlobalPosition = destination;
			}
		}

		GD.Print($"[BurstOfWinter] Nova detonated at {Boss.GlobalPosition}.");
	}
}