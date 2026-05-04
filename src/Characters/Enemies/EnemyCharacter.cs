using System.Collections.Generic;
using Godot;
using healerfantasy;

/// <summary>
/// Abstract base class for all enemy characters (bosses and adds).
///
/// Provides targeting helpers and an animation loader that would otherwise be
/// duplicated verbatim across every enemy file.
///
/// Targeting helpers
/// ─────────────────
/// <see cref="FindTank"/>               — alive Templar, or null
/// <see cref="FindHealer"/>             — alive Healer (player), or null
/// <see cref="PickRandomPartyMember"/>  — uniformly random alive party member, or null
/// <see cref="CollectAlivePartyMembers"/> — all alive party members, shuffled
///
/// Animation loader
/// ────────────────
/// <see cref="AddAnimFromFiles"/> — adds a named animation to a <see cref="SpriteFrames"/>
/// by loading sequentially numbered PNGs from a given base path and prefix.
/// Most bosses follow the pattern  res://assets/enemies/{boss-name}/{prefix}{n}.png
/// and pass their local AssetBase constant + file prefix.  Enemies with a
/// different folder layout (e.g. per-animation sub-directories) can keep a
/// private override that builds the path differently.
/// </summary>
public abstract partial class EnemyCharacter : Character
{
	// ── Targeting helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Returns the alive party member named "Templar" (the tank), or null.
	/// </summary>
	protected Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.TemplarName && c.IsAlive)
				return c;
		return null;
	}

	/// <summary>
	/// Returns the alive party member named "Healer" (the player), or null.
	/// </summary>
	protected Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.HealerName && c.IsAlive)
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
	/// Returns all alive party members in a randomised order.
	/// Useful for abilities that need to visit every member (e.g. jump slams).
	/// </summary>
	protected List<Character> CollectAlivePartyMembers()
	{
		var members = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				members.Add(c);

		// Fisher-Yates shuffle
		for (var i = members.Count - 1; i > 0; i--)
		{
			var j = (int)(GD.Randi() % (uint)(i + 1));
			(members[i], members[j]) = (members[j], members[i]);
		}
		return members;
	}

	// ── Animation helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Adds a named animation to <paramref name="frames"/> by loading PNGs named
	/// <c>{assetBase}{filePrefix}1.png</c> … <c>{assetBase}{filePrefix}{count}.png</c>.
	///
	/// <para>
	/// Example — boss whose files live at res://assets/enemies/blood-knight/:
	/// <code>
	/// AddAnimFromFiles(frames, "idle", AssetBase + "idle", 3, 4f, true);
	/// // loads blood-knight/idle1.png, idle2.png, idle3.png
	/// </code>
	/// </para>
	///
	/// Enemies that use a different path layout (sub-directories per animation,
	/// capitalised filenames, etc.) can shadow this with a private overload.
	/// </summary>
	/// <param name="frames">The <see cref="SpriteFrames"/> to add the animation to.</param>
	/// <param name="animName">Name of the animation (e.g. "idle", "attack").</param>
	/// <param name="filePrefix">
	/// Full path prefix including the asset base directory, e.g.
	/// <c>"res://assets/enemies/blood-knight/idle"</c>.  The frame number and
	/// <c>.png</c> extension are appended automatically.
	/// </param>
	/// <param name="count">Number of frames (1 … count).</param>
	/// <param name="fps">Playback speed in frames per second.</param>
	/// <param name="looping">Whether the animation loops.</param>
	protected static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool looping)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, looping);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var tex = GD.Load<Texture2D>($"{filePrefix}{i}.png");
			frames.AddFrame(animName, tex);
		}
	}
}
