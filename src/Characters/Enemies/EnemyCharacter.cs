using System.Collections.Generic;
using Godot;
using healerfantasy;

/// <summary>
/// Abstract base class for all enemy characters (bosses and adds).
///
/// Provides targeting helpers that would otherwise be duplicated across every
/// enemy file. Each enemy's look and animation clips live in its rig
/// (<c>RigLibrary</c>) and its <c>Clips</c> table.
///
/// Targeting helpers
/// ─────────────────
/// <see cref="FindTank"/>               — alive Dawnwarden, or null
/// <see cref="FindHealer"/>             — alive Healer (player), or null
/// <see cref="PickRandomPartyMember"/>  — uniformly random alive party member, or null
/// <see cref="CollectAlivePartyMembers"/> — all alive party members, shuffled
/// </summary>
public abstract partial class EnemyCharacter : Character
{

	public EnemyCharacter()
	{
		IsFriendly = false;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
	}

	// ── Targeting helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Returns the alive Dawnwarden (the tank), or null.
	/// </summary>
	protected Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.DawnwardenName && c.IsAlive)
				return c;
		return null;
	}

	/// <summary>
	/// Returns the alive Lightkeeper (the player), or null.
	/// </summary>
	protected Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.LightkeeperName && c.IsAlive)
				return c;
		return null;
	}

	/// <summary>
	/// Returns a uniformly random alive party member, or null if the party
	/// has been wiped.
	/// </summary>
	protected Character PickRandomPartyMember()
	{
		var alive = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				alive.Add(c);
		if (alive.Count == 0) return null;
		return alive[(int)(GD.Randi() % (uint)alive.Count)];
	}

	/// <summary>
	/// Returns the exact party member this boss is currently choosing for a
	/// melee-targeted attack, and broadcasts that choice so the UI can follow it.
	/// </summary>
	protected Character SelectCurrentMeleeTarget()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		RaiseBossMeleeTargetChanged(target);
		return target;
	}


}
