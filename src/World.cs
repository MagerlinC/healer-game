using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.UI;

/// <summary>
/// Root script for the World scene.
///
/// Reads <see cref="RunState.CurrentDungeon"/> and
/// <see cref="RunState.CurrentBossIndexInDungeon"/> to determine which boss
/// and arena background to load for this encounter.
///
/// A single World scene serves all boss fights across all dungeons — the boss
/// and background are instantiated at runtime.
///
/// Multi-boss scenes (e.g. Astral Twins) are supported: when the loaded scene's
/// root is a plain Node2D the code collects all Character children in the boss
/// group and registers each one individually for FCT and health bars.
///
/// Slot order matches PartyUI.MemberDefs:
///   0 = Templar | 1 = Healer (Player) | 2 = Assassin | 3 = Wizard
/// </summary>
public partial class World : Node2D
{
	public override void _Ready()
	{
		CombatLog.Clear();

		var dungeon   = RunState.Instance.CurrentDungeon;
		var bossIndex = RunState.Instance.CurrentBossIndexInDungeon;

		// ── Arena background ──────────────────────────────────────────────────
		var bgLayer = new CanvasLayer { Layer = -10 };
		AddChild(bgLayer);
		var bgRect = new TextureRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Texture     = GD.Load<Texture2D>(dungeon.ArenaBackgroundPaths[bossIndex]);
		bgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		bgLayer.AddChild(bgRect);

		// Tooltip singleton must be added first so it is available to all UI nodes.
		AddChild(new GameTooltip());

		// ── Deflect overlay ───────────────────────────────────────────────────
		AddChild(new DeflectOverlay());

		// ── Floating combat text ──────────────────────────────────────────────
		var fctManager = new FloatingCombatTextManager();
		AddChild(fctManager);

		var ui      = GetNode<GameUI>("PartyUI");
		var player  = GetNode<Player>("Healer");
		var templar  = GetNode<Character>("Templar");
		var assassin = GetNode<Character>("Assassin");
		var wizard   = GetNode<Character>("Wizard");

		// ── Boss — instantiated from current dungeon definition ───────────────
		var bossScene = GD.Load<PackedScene>(dungeon.BossScenePaths[bossIndex]);
		var bossRoot  = bossScene.Instantiate();

		// Position the boss (or its container) in the arena.
		if (bossRoot is Node2D bossNode2D)
			bossNode2D.Position = new Vector2(-57f, -20f);

		AddChild(bossRoot);

		// Collect all Character nodes that are tagged as bosses.
		// Single-boss scenes: the root IS the Character.
		// Multi-boss scenes (e.g. AstralTwins): root is a plain Node2D with
		// Character children, each of which is already in the boss group via
		// their own _Ready().
		var bossCharacters = new List<Character>();
		if (bossRoot is Character singleBoss)
		{
			bossCharacters.Add(singleBoss);
		}
		else
		{
			foreach (var child in bossRoot.GetChildren())
				if (child is Character c)
					bossCharacters.Add(c);
		}

		foreach (var b in bossCharacters)
			fctManager.Register(b);

		// Register boss character refs for hover-targeting and health bar management.
		ui.SetBossCharacters(
			bossCharacters[0],
			bossCharacters.Count > 1 ? bossCharacters[1] : null);

		// If there is more than one boss (e.g. Astral Twins), show a secondary
		// health bar for the second boss beneath the primary one.
		if (bossCharacters.Count > 1)
			ui.ShowSecondaryBossBar(bossCharacters[1]);

		fctManager.Register(player);
		fctManager.Register(templar);
		fctManager.Register(assassin);
		fctManager.Register(wizard);

		ui.BindCharacter(0, templar);
		ui.BindCharacter(1, player);
		ui.BindCharacter(2, assassin);
		ui.BindCharacter(3, wizard);

		player.GameUI = ui;

		ui.RebuildActionBar(player.EquippedSpells);
		ui.BuildGenericActionBar(player);

		// ── Spellbook selector ────────────────────────────────────────────────
		var spellbook = new SpellbookSelector();
		AddChild(spellbook);
		spellbook.Init(player, ui);

		// ── Talent selector ───────────────────────────────────────────────────
		var talentSelector = new TalentSelector();
		AddChild(talentSelector);
		talentSelector.Init(player);

		// ── Death screen ──────────────────────────────────────────────────────
		var deathScreen = new DeathScreen();
		AddChild(deathScreen);

		// ── Victory / dungeon-cleared screen ──────────────────────────────────
		var victoryScreen = new VictoryScreen();
		AddChild(victoryScreen);

		// ── Pause menu (Escape key) ────────────────────────────────────────────
		var pauseMenu = new healerfantasy.UI.PauseMenu();
		AddChild(pauseMenu);

		// ── Arena bounds ──────────────────────────────────────────────────────
		// Four invisible StaticBody2D walls at the edges of the camera view.
		// MoveAndSlide() on the Player will collide with them naturally — no
		// coordinate clamping needed in Player code.
		// Half-extents are derived from the viewport size and camera zoom so
		// the walls are always exactly at the visible edge regardless of resolution.
		AddArenaBounds();

		// For the Frozen Peak the background has a prominent circular arena.
		// A physics ring keeps the player inside the visible circle — tune
		// GameConstants.FrozenPeakArenaRadius if the ring looks too large or small.
		if (dungeon.Tier == GameConstants.FrozenPeakTier)
			AddCircularArenaBound(Vector2.Zero, GameConstants.FrozenPeakArenaFractionX, GameConstants.FrozenPeakArenaFractionY, GameConstants.FrozenPeakArenaCenterOffsetY);
	}

	/// <summary>
	/// Adds an elliptical physics boundary centred on <paramref name="center"/> plus
	/// an optional vertical shift (<paramref name="centerOffsetFractionY"/>).
	/// <paramref name="fractionX"/> and <paramref name="fractionY"/> are fractions of
	/// the viewport's world-space half-width and half-height respectively (the same
	/// values <see cref="AddArenaBounds"/> uses), so the boundary scales correctly
	/// across resolutions and camera zooms. <paramref name="centerOffsetFractionY"/>
	/// is a fraction of the total viewport height (positive = downward).
	/// The ellipse is approximated by <paramref name="segments"/> short
	/// <see cref="SegmentShape2D"/> walls; <c>MoveAndSlide()</c> handles collision.
	/// </summary>
	void AddCircularArenaBound(Vector2 center, float fractionX, float fractionY,
	                           float centerOffsetFractionY = 0f, int segments = 48)
	{
		var camera  = GetNode<Camera2D>("Camera2D");
		var view    = GetViewport().GetVisibleRect().Size;
		var radiusX = view.X / (2f * camera.Zoom.X) * fractionX;
		var radiusY = view.Y / (2f * camera.Zoom.Y) * fractionY;

		// Shift the centre down by a fraction of the total viewport height.
		var actualCenter = center + new Vector2(0f, view.Y / camera.Zoom.Y * centerOffsetFractionY);

		for (var i = 0; i < segments; i++)
		{
			var angleA = Mathf.Tau * i       / segments;
			var angleB = Mathf.Tau * (i + 1) / segments;
			var a = actualCenter + new Vector2(Mathf.Cos(angleA) * radiusX, Mathf.Sin(angleA) * radiusY);
			var b = actualCenter + new Vector2(Mathf.Cos(angleB) * radiusX, Mathf.Sin(angleB) * radiusY);

			var wall  = new StaticBody2D();
			var shape = new CollisionShape2D { Shape = new SegmentShape2D { A = a, B = b } };
			wall.AddChild(shape);
			AddChild(wall);
		}
	}

	void AddArenaBounds()
	{
		var camera = GetNode<Camera2D>("Camera2D");
		var view   = GetViewport().GetVisibleRect().Size;
		var hw     = view.X / (2f * camera.Zoom.X); // half-width in world units
		var hh     = view.Y / (2f * camera.Zoom.Y); // half-height in world units

		// (from, to) pairs for left / right / top / bottom edges
		(Vector2 A, Vector2 B)[] edges =
		{
			(new Vector2(-hw, -hh), new Vector2(-hw,  hh)), // left
			(new Vector2( hw, -hh), new Vector2( hw,  hh)), // right
			(new Vector2(-hw, -hh), new Vector2( hw, -hh)), // top
			(new Vector2(-hw,  hh), new Vector2( hw,  hh)), // bottom
		};

		foreach (var (a, b) in edges)
		{
			var wall  = new StaticBody2D();
			var shape = new CollisionShape2D { Shape = new SegmentShape2D { A = a, B = b } };
			wall.AddChild(shape);
			AddChild(wall);
		}
	}
}
