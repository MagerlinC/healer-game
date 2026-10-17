using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Scald.
///
/// 1.5-second cast followed by a nova explosion centred on the Queen.
/// On resolution:
///   1. A <see cref="ScaldNovaEffect"/> visual bursts outward from
///      the Queen's position (purely cosmetic — expands and fades in ~0.4 s).
///   2. Every living party member takes <see cref="Damage"/> damage.
///   3. Every living party member is knocked outward from the Queen, launched
///      toward the edge of the arena. If an <see cref="PartyMember.ArenaBoundary"/>
///      is active they land at (or just inside) the ring boundary.
/// </summary>
[GlobalClass]
public partial class BossQueenScaldSpell : SpellResource
{
	// ── tunables ─────────────────────────────────────────────────────────────
	/// <summary>Flat damage dealt to each party member on detonation.</summary>
	public float Damage = 40f;

	/// <summary>
	/// Fallback knockback distance (world pixels) used only when no arena
	/// boundary is active (e.g. during dev testing outside the Guttering Spire).
	/// </summary>
	const float FallbackKnockbackDistance = 200f;

	// ── config ────────────────────────────────────────────────────────────────
	/// <summary>Reference to the Queen — must be set before <c>SpellPipeline.Cast</c>.</summary>
	public Character Boss { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────
	public BossQueenScaldSpell()
	{
		Name = "Scald";
		Description = "The Queen sheds a scalding wave of molten wax, " +
		              "dealing damage to all nearby enemies and blasting them to the " +
		              "edges of the spire.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 1.5f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/scald.png");
		TargetingType = TargetingType.Enemy;
	}

	// ── Apply ─────────────────────────────────────────────────────────────────
	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[Scald] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[Scald] Boss has no parent — cannot spawn nova.");
			return;
		}

		// ── Visual ──────────────────────────────────────────────────────────
		var nova = new ScaldNovaEffect();
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
				SourceName = GameConstants.GutteringSpireBossName,
				TargetName = target.CharacterName,
				AbilityName = "Scald",
				Amount = Damage,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Scalded by a wave of the Queen's molten wax."
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

		GD.Print($"[Scald] Nova detonated at {Boss.GlobalPosition}.");
	}
}