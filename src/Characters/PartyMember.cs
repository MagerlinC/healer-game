using Godot;
using healerfantasy;

/// <summary>
/// A non-player party member (Templar, Assassin, Wizard).
///
/// Target tracking
/// ───────────────
/// When the player explicitly attacks a boss, <see cref="NotifyPlayerAttackedBoss"/>
/// is called with that boss. All party members then prefer to attack the same
/// target, creating the "switch targets" mechanic needed for the Astral Twins
/// fight and making the party feel responsive to player decisions.
/// </summary>
public partial class PartyMember : Character
{
	// ── Shared targeting state ────────────────────────────────────────────────

	/// <summary>
	/// The boss the player most recently attacked directly.
	/// Party members prefer this target via <see cref="FindPreferredBoss"/>.
	/// </summary>
	public static Character LastKnownBossTarget { get; private set; }

	/// <summary>
	/// Called by <see cref="Player"/> whenever it fires a harmful spell at a boss.
	/// Updates the shared target so all party members follow the player's focus.
	/// </summary>
	public static void NotifyPlayerAttackedBoss(Character boss)
	{
		LastKnownBossTarget = boss;
	}

	/// <summary>
	/// Returns the boss the party should focus — the player's last-attacked target
	/// if that target is still alive, otherwise the first alive boss found.
	/// </summary>
	protected Character FindPreferredBoss()
	{
		if (LastKnownBossTarget != null && LastKnownBossTarget.IsAlive)
			return LastKnownBossTarget;

		// Fallback: first alive boss in the group.
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is Character c && c.IsAlive)
				return c;

		return null;
	}
}
