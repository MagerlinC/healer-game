#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Root script for the Camp scene — a mid-run rest stop where the player can
/// adjust their spell and talent loadout before heading to the next dungeon.
///
/// Extends <see cref="LoadoutController"/> to inherit the full spell/talent
/// overlay system.  Camp-specific additions:
///   • Camp background (camp.png)
///   • Three interactibles: Spell Tome, Talent Board, Map
///   • Map click → navigate to MapScreen to select the next dungeon
///
/// Note: <see cref="RunState.CompleteCamp"/> is called by MapScreenController
/// when the player actually clicks a dungeon node, not here — so clicking Map
/// opens the map without committing the camp-departure state yet.
/// </summary>
public partial class CampController : LoadoutController
{
	protected override void SetupScene()
	{
		// ── Camp background ───────────────────────────────────────────────────
		var bg = new Sprite2D();
		bg.Texture  = GD.Load<Texture2D>(AssetConstants.CampBackgroundPath);
		bg.Centered = true;
		bg.Position = new Vector2(1920f / 2f, 1080f / 2f);
		bg.Scale    = new Vector2(0.5f, 0.5f);
		AddChild(bg);

		var bgHalfW = bg.Texture.GetWidth() * bg.Scale.X / 2f;
		var bgLeft  = bg.Position.X - bgHalfW;
		var bgRight = bg.Position.X + bgHalfW;

		// ── Interactibles ─────────────────────────────────────────────────────
		// Map is on the left so it's prominent — the player came here to rest,
		// spell/talent editing is in the middle/right.
		var mapItem = MakeInteractible(AssetConstants.MapInteractiblePath,
			new Vector2(440f, FloorHeight - 20f), new Vector2(0.100f, 0.100f), 28f);
		var spellTome = MakeInteractible(AssetConstants.SpellTomeInteractiblePath,
			new Vector2(680f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f);
		var talentBoard = MakeInteractible(AssetConstants.TalentBoardInteractiblePath,
			new Vector2(890f, FloorHeight), new Vector2(0.090f, 0.090f), 50f);

		AddChild(mapItem);
		AddChild(spellTome);
		AddChild(talentBoard);
		_interactibles.Add(mapItem);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);

		// ── Player ────────────────────────────────────────────────────────────
		_player = new OverworldPlayer();
		_player.Position = new Vector2(660f, FloorHeight - 15f);
		_player.Scale    = new Vector2(1.5f, 1.5f);
		_player.XMin     = bgLeft;
		_player.XMax     = bgRight;
		AddChild(_player);

		// ── HUD ───────────────────────────────────────────────────────────────
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);
		hud.AddChild(BuildHintLabel());
		hud.AddChild(BuildBackToMenuButton());
		_characterProgressLabel = BuildCharacterProgressLabel();
		hud.AddChild(_characterProgressLabel);

		// Dungeon progress label (e.g. "Dungeon 1 of 3 complete")
		var progressLabel = new Label();
		progressLabel.Text = BuildProgressText();
		progressLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		progressLabel.OffsetLeft = 20f;
		progressLabel.OffsetTop  = 40f;
		progressLabel.AddThemeFontSizeOverride("font_size", 15);
		progressLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 0.45f));
		progressLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hud.AddChild(progressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		mapItem.InputEvent += (_, ev, _) => { if (IsLeftClick(ev)) OnOpenMap(); };
		mapItem.MouseEntered += () => _hintLabel!.Text = "World Map  •  Continue your journey";
		mapItem.MouseExited  += () => _hintLabel!.Text = DefaultHint;

		spellTome.InputEvent += (_, ev, _) => { if (IsLeftClick(ev)) OpenPanel(_spellPanel!); };
		spellTome.MouseEntered += () => _hintLabel!.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited  += () => _hintLabel!.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) => { if (IsLeftClick(ev)) OpenPanel(_talentPanel!); };
		talentBoard.MouseEntered += () => _hintLabel!.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited  += () => _hintLabel!.Text = DefaultHint;
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static string BuildProgressText()
	{
		var d     = RunState.Instance.CompletedDungeons;
		var total = DungeonDefinition.All.Length;
		return $"Rest  ·  {d} of {total} dungeons cleared";
	}

	void OnOpenMap()
	{
		GetTree().ChangeSceneToFile("res://levels/MapScreen.tscn");
	}
}
