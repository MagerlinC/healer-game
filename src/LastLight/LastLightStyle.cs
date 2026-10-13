using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// Entry point for the "Last Light" art direction: the healer is the only
/// light, the party exists where that light reaches them, and everything
/// hostile is darkness.
///
/// <see cref="World"/> calls <see cref="Apply"/> once a fight is set up. The
/// party is always converted; bosses and arena backgrounds are converted when
/// they have an entry below — everything else keeps its original art.
/// </summary>
public static class LastLightStyle
{
	/// <summary>Master switch. Set to false to get the original art back.</summary>
	public const bool Enabled = true;

	// Written each frame by the healer's rig, read by every other rig and the background.
	public static Vector2 LightUv = new(0.5f, 0.5f);
	public static Vector2 LightFeetUv = new(0.5f, 0.55f);
	public static float LightStrength = 1f;

	static readonly Dictionary<string, Func<RigDefinition>> PartyRigs = new()
	{
		[GameConstants.TemplarName] = RigLibrary.Templar,
		[GameConstants.AssassinName] = RigLibrary.Assassin,
		[GameConstants.WizardName] = RigLibrary.Wizard,
		[GameConstants.HealerName] = RigLibrary.Healer
	};

	/// <summary>Boss scene path → rig.</summary>
	static readonly Dictionary<string, Func<RigDefinition>> BossRigs = new()
	{
		["res://levels/CrystalKnight.tscn"] = RigLibrary.CrystalKnight,
		["res://levels/BringerOfDeath.tscn"] = RigLibrary.BringerOfDeath,
		["res://levels/DemonSlime.tscn"] = RigLibrary.DemonSlime
	};

	/// <summary>Dungeon name → background preset.</summary>
	static readonly Dictionary<string, LastLightBackground.Preset> Backgrounds = new()
	{
		["The Ancient Keep"] = LastLightBackground.Preset.AncientKeep
	};

	public static void Apply(IEnumerable<Character> party, IEnumerable<Character> bosses, string bossScenePath,
		DungeonDefinition dungeon, TextureRect arenaBackground)
	{
		if (!Enabled) return;

		foreach (var member in party)
			if (PartyRigs.TryGetValue(member.CharacterName, out var make))
				SilhouetteRig.Attach(member, make());

		if (BossRigs.TryGetValue(bossScenePath, out var makeBoss))
			foreach (var boss in bosses)
				SilhouetteRig.Attach(boss, makeBoss());

		if (Backgrounds.TryGetValue(dungeon.Name, out var preset))
		{
			arenaBackground.Visible = false;
			var bg = new LastLightBackground { BackgroundPreset = preset };
			arenaBackground.GetParent().AddChild(bg);
		}
	}
}
