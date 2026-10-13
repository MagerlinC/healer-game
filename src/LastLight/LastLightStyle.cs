using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// Entry point for the "Last Light" art direction: the healer is the only
/// light, the party exists where that light reaches them, and everything
/// hostile is darkness.
///
/// Characters get rigs automatically (<see cref="InstallAutoRigs"/>); <see cref="World"/>
/// calls <see cref="ApplyArena"/> to swap in the dungeon's backdrop.
/// </summary>
public static class LastLightStyle
{
	/// <summary>Master switch. Set to false to get the original art back.</summary>
	public const bool Enabled = true;

	// Written each frame by the healer's rig, read by every other rig and the background.
	public static Vector2 LightUv = new(0.5f, 0.5f);
	public static Vector2 LightFeetUv = new(0.5f, 0.55f);
	public static float LightStrength = 1f;

	/// <summary>Other lights in the scene (bonfires) that also hold the dark back.</summary>
	public static readonly List<LightPocket> Pockets = new();

	/// <summary>
	/// Character type → rig. Rigs attach automatically whenever one of these enters
	/// the game, so bosses and mid-fight adds (clones, runes, vines) are covered alike.
	/// </summary>
	static readonly Dictionary<Type, Func<Character, RigDefinition>> Rigs = new()
	{
		// the party
		[typeof(Templar)] = _ => RigLibrary.Templar(),
		[typeof(Assassin)] = _ => RigLibrary.Assassin(),
		[typeof(Wizard)] = _ => RigLibrary.Wizard(),
		[typeof(Player)] = _ => RigLibrary.Healer(),

		// The Ancient Keep
		[typeof(CrystalKnight)] = _ => RigLibrary.CrystalKnight(),
		[typeof(BringerOfDeath)] = _ => RigLibrary.BringerOfDeath(),
		[typeof(DemonSlime)] = _ => RigLibrary.DemonSlime(),

		// The Forsaken Citadel
		[typeof(FlyingDemon)] = _ => RigLibrary.FlyingDemon(),
		[typeof(MechaGolem)] = _ => RigLibrary.MechaGolem(),
		[typeof(FlyingSkull)] = _ => RigLibrary.FlyingSkull(),

		// Castle of Blood
		[typeof(BloodKnight)] = _ => RigLibrary.BloodKnight(),
		[typeof(TheCountess)] = _ => RigLibrary.TheCountess(),
		[typeof(CountessClone)] = _ => RigLibrary.TheCountess(),
		[typeof(TheBloodPrince)] = _ => RigLibrary.TheBloodPrince(),
		[typeof(BloodRune)] = _ => RigLibrary.BloodRune(),

		// The Sanctum of Stars
		[typeof(TheNightborne)] = _ => RigLibrary.TheNightborne(),
		[typeof(AstralTwin)] = c => RigLibrary.AstralTwin(dusk: c.Name.ToString().Contains("Dusk")),
		[typeof(ThatWhichSwallowedTheStars)] = _ => RigLibrary.ThatWhichSwallowedTheStars(),

		// The Frozen Peak
		[typeof(QueenOfTheFrozenWastes)] = _ => RigLibrary.QueenOfTheFrozenWastes(),
		[typeof(TheFrozenTerror)] = _ => RigLibrary.TheFrozenTerror(),

		// Rune of Nature
		[typeof(VinesEnemy)] = _ => RigLibrary.Vines()
	};

	/// <summary>Dungeon name → background preset.</summary>
	static readonly Dictionary<string, LastLightBackground.Preset> Backgrounds = new()
	{
		["The Ancient Keep"] = LastLightBackground.Preset.AncientKeep,
		["The Forsaken Citadel"] = LastLightBackground.Preset.ForsakenCitadel,
		["Castle of Blood"] = LastLightBackground.Preset.CastleOfBlood,
		["The Sanctum of Stars"] = LastLightBackground.Preset.SanctumOfStars,
		["The Frozen Peak"] = LastLightBackground.Preset.FrozenPeak
	};

	static bool _rigsInstalled;

	/// <summary>Attach a rig to every known character type as it enters the tree.</summary>
	public static void InstallAutoRigs(SceneTree tree)
	{
		if (!Enabled || _rigsInstalled) return;
		_rigsInstalled = true;
		tree.NodeAdded += node =>
		{
			if (node is not Character c || !Rigs.TryGetValue(c.GetType(), out var make)) return;
			// Deferred, so the character's own _Ready has built its sprite first.
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(c) && c.GetNodeOrNull("SilhouetteRig") == null)
					SilhouetteRig.Attach(c, make(c));
			}).CallDeferred();
		};
	}

	/// <summary>Replaces the arena background for dungeons that have a Last Light preset.</summary>
	public static void ApplyArena(DungeonDefinition dungeon, TextureRect arenaBackground)
	{
		if (!Enabled || !Backgrounds.TryGetValue(dungeon.Name, out var preset)) return;
		arenaBackground.Visible = false;
		arenaBackground.GetParent().AddChild(new LastLightBackground { BackgroundPreset = preset });
	}
}
