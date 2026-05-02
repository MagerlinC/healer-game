#nullable enable
using System.Linq;
using Godot;
using healerfantasy;

/// <summary>
/// The World Map screen shown between locations.
///
/// Renders a sequence of seven nodes:
///   Dungeon 0 → Camp 0 → Dungeon 1 → Camp 1 → Dungeon 2 → Camp 2 → Dungeon 3
///
/// Dungeons 0–2 are randomly chosen from tiers 1–3 at run start.
/// Dungeon 3 is always The Frozen Peak (tier 4) — the final boss encounter.
///
/// Clicking any node opens a detail panel on the right showing the location's
/// name, description, and (for the currently Available dungeon) an Enter button
/// that commits the transition.
///
/// Camps are shown for context but cannot be entered from the map — they are
/// reached automatically from the Dungeon Cleared screen.
/// </summary>
public partial class MapScreenController : Node2D
{
	// Map node positions in viewport space (1920×1080).
	// The path winds across the map and ascends toward the frozen peak in the upper-right.
	// Adjust these to match the actual world-map background art as needed.
	static readonly Vector2[] NodeCentres =
	{
		new(235f, 310f), // Dungeon 0 — upper-left, mountain peaks
		new(430f, 543f), // Camp 0    — left-centre
		new(700f, 700f), // Dungeon 1 — centre
		new(1215f, 780f), // Camp 1    — right-centre
		new(1490f, 964f), // Dungeon 2 — lower-right
		new(1640f, 790f), // Camp 2    — far-right, beginning to ascend
		new(1800f, 540f) // Dungeon 3 — The Frozen Peak, upper-far-right
	};

	static readonly bool[] IsDungeon = { true, false, true, false, true, false, true };
	static readonly int[] DungeonIdx = { 0, -1, 1, -1, 2, -1, 3 };
	static readonly int[] CampIdx = { -1, 0, -1, 1, -1, 2, -1 };

	static readonly Vector2 DungeonNodeSize = new(160f, 90f);
	static readonly Vector2 CampNodeSize = new(120f, 72f);

	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color ColCompleted = new(0.25f, 0.65f, 0.28f, 1f);
	static readonly Color ColAvailable = new(0.95f, 0.80f, 0.20f, 1f);
	static readonly Color ColInProgress = new(0.85f, 0.55f, 0.15f, 1f);
	static readonly Color ColLocked = new(0.30f, 0.28f, 0.26f, 0.80f);
	static readonly Color ColLineDone = new(0.35f, 0.65f, 0.35f, 0.90f);
	static readonly Color ColLinePending = new(0.45f, 0.40f, 0.28f, 0.60f);
	static readonly Color ColPanelBg = new(0.08f, 0.07f, 0.07f, 0.95f);
	static readonly Color ColPanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color ColTitle = new(0.95f, 0.84f, 0.50f);
	static readonly Color ColHint = new(0.50f, 0.46f, 0.40f);

	// ── runtime ───────────────────────────────────────────────────────────────
	Control? _detailPanel; // right-side detail panel root
	int _selectedSlot = -1; // which map slot is currently shown in the panel
	StyleBoxFlat?[] _nodeBorders = null!; // live border refs for selection highlight

	AudioStreamPlayer _sfxPlayer;

	public override void _Ready()
	{

		_sfxPlayer = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>(AssetConstants.ButtonClickPath),
			VolumeDb = -6f
		};
		AddChild(_sfxPlayer);

		_nodeBorders = new StyleBoxFlat?[NodeCentres.Length];

		// ── Background ────────────────────────────────────────────────────────
		var bgLayer = new CanvasLayer { Layer = -10 };
		AddChild(bgLayer);
		var bgRect = new TextureRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Texture = GD.Load<Texture2D>(AssetConstants.MapBackgroundPath);
		bgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		bgLayer.AddChild(bgRect);

		// ── Map canvas layer ──────────────────────────────────────────────────
		var mapLayer = new CanvasLayer { Layer = 0 };
		AddChild(mapLayer);

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		mapLayer.AddChild(root);

		// Connecting lines (drawn before nodes so they appear underneath)
		var lineDrawer = new PathLineDrawer(NodeCentres, BuildLineStates());
		lineDrawer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		lineDrawer.MouseFilter = Control.MouseFilterEnum.Ignore;
		root.AddChild(lineDrawer);

		// Map nodes
		for (var i = 0; i < NodeCentres.Length; i++)
			root.AddChild(BuildMapNode(i));

		// Detail panel (hidden until a node is clicked)
		_detailPanel = BuildDetailPanel();
		_detailPanel.Visible = false;
		root.AddChild(_detailPanel);

		// ── HUD layer ─────────────────────────────────────────────────────────
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);

		var hudRoot = new Control();
		hudRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		hudRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
		hud.AddChild(hudRoot);

		var title = new Label();
		title.Text = "World Map";
		title.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		title.OffsetLeft = 24f;
		title.OffsetTop = 16f;
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", ColTitle);
		title.MouseFilter = Control.MouseFilterEnum.Ignore;
		hudRoot.AddChild(title);

		var backBtn = MakeTextButton("← Back");
		backBtn.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		backBtn.OffsetLeft = 24f;
		backBtn.OffsetTop = 58f;
		backBtn.Pressed += OnBackPressed;
		hudRoot.AddChild(backBtn);

		var menuBtn = MakeTextButton("← Main Menu");
		menuBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		menuBtn.GrowHorizontal = Control.GrowDirection.Begin;
		menuBtn.OffsetRight = -12f;
		menuBtn.OffsetTop = 16f;
		menuBtn.Pressed += OnMainMenuPressed;
		hudRoot.AddChild(menuBtn);

		var hint = new Label();
		hint.Text = "Click a location to see details";
		hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		hint.GrowVertical = Control.GrowDirection.Begin;
		hint.OffsetLeft = 24f;
		hint.OffsetBottom = -16f;
		hint.AddThemeFontSizeOverride("font_size", 14);
		hint.AddThemeColorOverride("font_color", ColHint);
		hint.MouseFilter = Control.MouseFilterEnum.Ignore;
		hudRoot.AddChild(hint);
	}

	// ── map node builder ──────────────────────────────────────────────────────

	Control BuildMapNode(int slotIndex)
	{
		var isDungeonNode = IsDungeon[slotIndex];
		var state = GetNodeState(slotIndex);
		var centre = NodeCentres[slotIndex];
		var size = isDungeonNode ? DungeonNodeSize : CampNodeSize;

		var borderColor = state switch
		{
			RunState.MapNodeState.Completed => ColCompleted,
			RunState.MapNodeState.Available => ColAvailable,
			RunState.MapNodeState.InProgress => ColInProgress,
			_ => ColLocked
		};

		var bgColor = state == RunState.MapNodeState.Locked
			? new Color(0.10f, 0.09f, 0.08f, 0.85f)
			: ColPanelBg;

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = bgColor;
		panelStyle.SetCornerRadiusAll(isDungeonNode ? 8 : 50);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = borderColor;
		_nodeBorders[slotIndex] = panelStyle;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.CustomMinimumSize = size;
		panel.Position = centre - size / 2f;
		panel.Size = size;
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var vbox = new VBoxContainer();
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddThemeConstantOverride("separation", 2);
		panel.AddChild(vbox);

		// Icon
		var icon = new Label();
		icon.Text = isDungeonNode ? "⚔" : "🔥";
		icon.HorizontalAlignment = HorizontalAlignment.Center;
		icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		icon.AddThemeFontSizeOverride("font_size", isDungeonNode ? 20 : 16);
		icon.Modulate = new Color(1f, 1f, 1f, state == RunState.MapNodeState.Locked ? 0.40f : 1f);
		vbox.AddChild(icon);

		// Name
		var nodeName = isDungeonNode
			? RunState.Instance.RunDungeons[DungeonIdx[slotIndex]].Name
			: "Rest Camp";
		if (state == RunState.MapNodeState.Completed) nodeName = "✓  " + nodeName;

		var nameLabel = new Label();
		nameLabel.Text = nodeName;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", isDungeonNode ? 11 : 10);
		nameLabel.AddThemeColorOverride("font_color",
			state == RunState.MapNodeState.Locked ? new Color(0.45f, 0.42f, 0.38f) : borderColor);
		vbox.AddChild(nameLabel);

		// Boss count sub-label for non-locked dungeons
		if (isDungeonNode && state != RunState.MapNodeState.Locked)
		{
			var bossCount = RunState.Instance.RunDungeons[DungeonIdx[slotIndex]].BossCount;
			var sub = new Label();
			sub.Text = $"{bossCount} boss{(bossCount != 1 ? "es" : "")}";
			sub.HorizontalAlignment = HorizontalAlignment.Center;
			sub.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			sub.AddThemeFontSizeOverride("font_size", 9);
			sub.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.42f));
			vbox.AddChild(sub);
		}

		// All nodes open the detail panel on click
		panel.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				SelectSlot(slotIndex);
				panel.AcceptEvent();
			}
		};

		// Hover highlight
		panel.MouseEntered += () =>
		{
			if (_selectedSlot != slotIndex)
				panelStyle.BgColor = new Color(bgColor.R + 0.06f, bgColor.G + 0.05f, bgColor.B + 0.04f, bgColor.A);
		};
		panel.MouseExited += () =>
		{
			if (_selectedSlot != slotIndex) panelStyle.BgColor = bgColor;
		};

		return panel;
	}

	// ── detail panel ─────────────────────────────────────────────────────────

	// Labels / button references we mutate when the selection changes
	Label _detailTitle = null!;
	Label _detailSub = null!;
	Label _detailBody = null!;
	Button _enterBtn = null!;
	Label _spellbookWarningLabel = null!;
	StyleBoxFlat _detailPanelStyle = null!;

	Control BuildDetailPanel()
	{
		// Anchored to the right side of the screen
		var container = new PanelContainer();
		container.SetAnchorsPreset(Control.LayoutPreset.RightWide);
		container.CustomMinimumSize = new Vector2(320f, 0f);
		container.GrowHorizontal = Control.GrowDirection.Begin;
		// Leave room so it doesn't clip the top/bottom HUD
		container.OffsetTop = 90f;
		container.OffsetBottom = -60f;
		container.OffsetRight = -12f;

		_detailPanelStyle = new StyleBoxFlat();
		_detailPanelStyle.BgColor = new Color(0.07f, 0.06f, 0.05f, 0.97f);
		_detailPanelStyle.SetCornerRadiusAll(8);
		_detailPanelStyle.SetBorderWidthAll(2);
		_detailPanelStyle.BorderColor = ColPanelBorder;
		_detailPanelStyle.ContentMarginLeft = _detailPanelStyle.ContentMarginRight = 20f;
		_detailPanelStyle.ContentMarginTop = _detailPanelStyle.ContentMarginBottom = 18f;
		container.AddThemeStyleboxOverride("panel", _detailPanelStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		container.AddChild(vbox);

		// Close button row
		var closeRow = new HBoxContainer();
		var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		closeRow.AddChild(spacer);
		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.Flat = true;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.60f, 0.55f, 0.48f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		closeBtn.Pressed += CloseDetailPanel;
		closeRow.AddChild(closeBtn);
		vbox.AddChild(closeRow);

		// Title
		_detailTitle = new Label();
		_detailTitle.AutowrapMode = TextServer.AutowrapMode.Word;
		_detailTitle.AddThemeFontSizeOverride("font_size", 20);
		_detailTitle.AddThemeColorOverride("font_color", ColTitle);
		vbox.AddChild(_detailTitle);

		// Sub-label (state badge)
		_detailSub = new Label();
		_detailSub.AddThemeFontSizeOverride("font_size", 13);
		vbox.AddChild(_detailSub);

		// Separator
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.50f, 0.40f, 0.22f, 0.55f));
		vbox.AddChild(sep);

		// Body text (boss list, description, etc.)
		_detailBody = new Label();
		_detailBody.AutowrapMode = TextServer.AutowrapMode.Word;
		_detailBody.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_detailBody.AddThemeFontSizeOverride("font_size", 14);
		_detailBody.AddThemeColorOverride("font_color", new Color(0.78f, 0.74f, 0.68f));
		vbox.AddChild(_detailBody);

		// Spacer to push button to the bottom
		var fill = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		vbox.AddChild(fill);

		// Spellbook warning (shown when player hasn't opened the Spellbook yet)
		_spellbookWarningLabel = new Label();
		_spellbookWarningLabel.Text =
			"Open your Spellbook in camp before entering a dungeon — click the tome to pick your spells!";
		_spellbookWarningLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_spellbookWarningLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_spellbookWarningLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_spellbookWarningLabel.AddThemeFontSizeOverride("font_size", 13);
		_spellbookWarningLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.78f, 0.28f));
		_spellbookWarningLabel.Visible = false;
		vbox.AddChild(_spellbookWarningLabel);

		// Enter button
		var enterStyle = MakeBtnStyle(new Color(0.12f, 0.17f, 0.12f), new Color(0.30f, 0.65f, 0.28f));
		var enterHover = MakeBtnStyle(new Color(0.18f, 0.24f, 0.16f), new Color(0.40f, 0.80f, 0.35f));

		_enterBtn = new Button();
		_enterBtn.Text = "Enter";
		_enterBtn.CustomMinimumSize = new Vector2(0f, 52f);
		_enterBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_enterBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_enterBtn.AddThemeFontSizeOverride("font_size", 18);
		_enterBtn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		_enterBtn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.92f, 0.88f));
		_enterBtn.AddThemeStyleboxOverride("normal", enterStyle);
		_enterBtn.AddThemeStyleboxOverride("hover", enterHover);
		_enterBtn.AddThemeStyleboxOverride("pressed", enterStyle);
		_enterBtn.AddThemeStyleboxOverride("focus", enterStyle);
		_enterBtn.Pressed += OnEnterPressed;
		vbox.AddChild(_enterBtn);

		return container;
	}

	void SelectSlot(int slotIndex)
	{
		_selectedSlot = slotIndex;
		var state = GetNodeState(slotIndex);
		var isDungeonNode = IsDungeon[slotIndex];

		// Update detail panel content
		string name, sub, body;
		Color subColor;

		if (isDungeonNode)
		{
			var def = RunState.Instance.RunDungeons[DungeonIdx[slotIndex]];
			name = def.Name;

			switch (state)
			{
				case RunState.MapNodeState.Available:
					sub = "▶  Available";
					subColor = ColAvailable;
					body = $"Bosses:\n" + def.BossNames.Join("\n");
					break;
				case RunState.MapNodeState.Completed:
					sub = "✓  Cleared";
					subColor = ColCompleted;
					body = "You have already conquered this dungeon.";
					break;
				default:
					sub = "🔒  Locked";
					subColor = ColLocked;
					body = "Complete earlier dungeons to unlock this location.";
					break;
			}
		}
		else
		{
			name = "Rest Camp";
			switch (state)
			{
				case RunState.MapNodeState.InProgress:
					sub = "🔥  Current Rest Stop";
					subColor = ColInProgress;
					body = "You are resting here. Use the map to head to the next dungeon when ready.";
					break;
				case RunState.MapNodeState.Completed:
					sub = "✓  Departed";
					subColor = ColCompleted;
					body = "You rested here and moved on.";
					break;
				default:
					sub = "🔒  Not yet reached";
					subColor = ColLocked;
					body = "Clear the preceding dungeon to reach this camp.";
					break;
			}
		}

		_detailTitle.Text = name;
		_detailSub.Text = sub;
		_detailSub.AddThemeColorOverride("font_color", subColor);
		_detailBody.Text = body;

		// Show Enter button only for available dungeons; gate on spellbook having been opened.
		var canEnter = isDungeonNode && state == RunState.MapNodeState.Available;
		var spellbookReady = PlayerProgressStore.HasOpenedSpellbook;

		_enterBtn.Visible = canEnter;
		_spellbookWarningLabel.Visible = canEnter && !spellbookReady;

		if (canEnter)
		{
			_enterBtn.Text = $"Enter {name}";
			_enterBtn.Disabled = !spellbookReady;
			_detailPanelStyle.BorderColor = spellbookReady ? ColAvailable : new Color(0.55f, 0.50f, 0.28f);
		}
		else
		{
			_enterBtn.Disabled = false;
			_detailPanelStyle.BorderColor = subColor * new Color(0.7f, 0.7f, 0.7f, 1f);
		}

		_detailPanel!.Visible = true;
	}

	void CloseDetailPanel()
	{
		_detailPanel!.Visible = false;
		_selectedSlot = -1;
	}

	// ── enter / navigation ────────────────────────────────────────────────────

	void OnEnterPressed()
	{
		if (_selectedSlot < 0 || !IsDungeon[_selectedSlot]) return;
		if (GetNodeState(_selectedSlot) != RunState.MapNodeState.Available) return;
		_sfxPlayer.Play();

		// Mark camp as completed if we arrived from one (player was at camp)
		if (RunState.Instance.CompletedCamps < RunState.Instance.CompletedDungeons)
			RunState.Instance.CompleteCamp();

		// Start run history on the very first dungeon
		if (RunState.Instance.CompletedDungeons == 0 && RunState.Instance.CompletedCamps == 0)
			RunHistoryStore.StartRun();

		GlobalAutoLoad.Reset();
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	void OnBackPressed()
	{
		var target = RunState.Instance.CompletedDungeons > 0
			? "res://levels/Camp.tscn"
			: "res://levels/Overworld.tscn";
		GetTree().ChangeSceneToFile(target);
	}

	void OnMainMenuPressed()
	{
		var runInProgress = RunState.Instance.CompletedDungeons > 0
		                    || RunState.Instance.CurrentBossIndexInDungeon > 0;
		if (runInProgress) RunHistoryStore.FinalizeRun(false);
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	RunState.MapNodeState GetNodeState(int slotIndex)
	{
		return IsDungeon[slotIndex]
			? RunState.Instance.GetDungeonMapState(DungeonIdx[slotIndex])
			: RunState.Instance.GetCampMapState(CampIdx[slotIndex]);
	}

	bool[] BuildLineStates()
	{
		var states = new bool[NodeCentres.Length - 1];
		for (var i = 0; i < states.Length; i++)
		{
			var from = GetNodeState(i);
			var to = GetNodeState(i + 1);
			states[i] = from is RunState.MapNodeState.Completed or RunState.MapNodeState.InProgress
			            && to is RunState.MapNodeState.Completed or RunState.MapNodeState.InProgress
				            or RunState.MapNodeState.Available;
		}

		return states;
	}

	static Button MakeTextButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.Flat = true;
		btn.CustomMinimumSize = new Vector2(140f, 40f);
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 14);
		btn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));
		return btn;
	}

	static StyleBoxFlat MakeBtnStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 16f;
		s.ContentMarginTop = s.ContentMarginBottom = 10f;
		return s;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// PATH LINE DRAWER
	// ══════════════════════════════════════════════════════════════════════════

	sealed partial class PathLineDrawer : Control
	{
		readonly Vector2[] _centres;
		readonly bool[] _done;

		public PathLineDrawer(Vector2[] centres, bool[] done)
		{
			_centres = centres;
			_done = done;
		}

		public override void _Draw()
		{
			for (var i = 0; i < _centres.Length - 1; i++)
			{
				var color = _done[i] ? ColLineDone : ColLinePending;
				DrawLine(_centres[i], _centres[i + 1], color, 4f, true);
				var mid = (_centres[i] + _centres[i + 1]) / 2f;
				DrawCircle(mid, 5f, color * new Color(1f, 1f, 1f, 0.6f));
			}
		}
	}
}