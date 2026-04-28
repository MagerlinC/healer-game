using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.Items;
using healerfantasy.UI;

/// <summary>
/// Overlay shown when a boss dies.
///
/// Three possible outcomes after each kill:
///
///   1. Non-final boss in current dungeon → "ARENA CLEARED!"
///      Continue button: advance boss index and reload World.tscn.
///
///   2. Final boss of a non-final dungeon → "DUNGEON CLEARED!"
///      Continue button: mark dungeon as completed and load Camp.tscn.
///
///   3. Final boss of the final dungeon → "VICTORY!"
///      Play Again / Main Menu buttons: finalize the run.
///
/// XP is awarded on every kill. Level-up notifications are displayed.
///
/// Sits on CanvasLayer 20 and is hidden by default.
/// ProcessMode is Always so buttons keep receiving input while the tree is paused.
/// </summary>
public partial class VictoryScreen : CanvasLayer
{
	AudioStreamPlayer _audioPlayer = new();

	public override void _Ready()
	{
		Layer = 20;
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;

		_audioPlayer.Stream = GD.Load<AudioStream>(AssetConstants.VictorySoundPath);
		_audioPlayer.VolumeDb = -4f;
		AddChild(_audioPlayer);

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.Died),
			Callable.From((Character character) =>
			{
				if (character.IsFriendly) return;

				// For multi-boss encounters (e.g. The Astral Twins), only show the
				// victory screen when ALL bosses are dead.
				foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
					if (node is Character c && c != character && c.IsAlive)
						return;

				// Snapshot this boss encounter before clearing the rolling log.
				RunHistoryStore.RecordBossEncounter(character.CharacterName);
				CombatLog.Clear();

				// Award XP.
				var dungeon = RunState.Instance.CurrentDungeon;
				var bossIndex = RunState.Instance.CurrentBossIndexInDungeon;
				var xpReward = bossIndex < dungeon.XpRewards.Count
					? dungeon.XpRewards[bossIndex]
					: 100;
				var levelsGained = PlayerProgressStore.AddXp(xpReward);

				// Roll for item drop.
				var droppedItem = ItemRegistry.RollDrop(character.CharacterName);
				if (droppedItem != null)
					ItemStore.AddToInventory(droppedItem);

				if (RunState.Instance.IsDevTestFight)
				{
					// Developer test fight — always return to Overworld after kill.
					ShowDevTestComplete(character.CharacterName, xpReward, levelsGained, droppedItem);
				}
				else if (!RunState.Instance.IsLastBossInDungeon)
				{
					// Mid-dungeon: more bosses to fight.
					ShowArenaCleared(character.CharacterName, xpReward, levelsGained, droppedItem);
				}
				else if (!RunState.Instance.IsLastDungeon)
				{
					// Last boss but not the last dungeon → head to camp.
					ShowDungeonCleared(character.CharacterName, xpReward, levelsGained, droppedItem);
				}
				else
				{
					// Final boss of the entire run → full victory!
					ShowVictoryScreen(xpReward, levelsGained, droppedItem);
				}
			}));

		// ── Dark overlay ──────────────────────────────────────────────────────
		var overlay = new ColorRect();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0f, 0f, 0f, 0.80f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// ── Centred content column ────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 20);
		overlay.AddChild(vbox);

		_titleLabel = new Label();
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 48);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.50f));
		vbox.AddChild(_titleLabel);

		_subLabel = new Label();
		_subLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_subLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_subLabel.AddThemeFontSizeOverride("font_size", 18);
		_subLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		vbox.AddChild(_subLabel);

		_xpLabel = new Label();
		_xpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_xpLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_xpLabel.AddThemeFontSizeOverride("font_size", 16);
		_xpLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.95f));
		vbox.AddChild(_xpLabel);

		// Item drop section — populated on boss death when loot drops, hidden otherwise.
		_itemSection = new VBoxContainer();
		_itemSection.Visible = false;
		vbox.AddChild(_itemSection);

		_btnRow = new HBoxContainer();
		// Anchor to bottom center
		_btnRow.AnchorLeft = 0.5f;
		_btnRow.AnchorRight = 0.5f;
		_btnRow.AnchorTop = 1.0f;
		_btnRow.AnchorBottom = 1.0f;

		// Offset slightly upward from bottom
		_btnRow.OffsetLeft = -100; // half width (adjust or let it size dynamically)
		_btnRow.OffsetRight = 100;
		_btnRow.OffsetBottom = -40; // distance from bottom
		_btnRow.OffsetTop = -80;
		_btnRow.AddThemeConstantOverride("separation", 20);
		overlay.AddChild(_btnRow);
	}

	// ── public API ────────────────────────────────────────────────────────────

	public void ShowArenaCleared(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "ARENA CLEARED!";
		_subLabel.Text = $"{defeatedBossName} has been defeated.\nPrepare for the next battle.";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Continue  ▶",
			new Color(0.10f, 0.16f, 0.10f),
			new Color(0.30f, 0.65f, 0.28f),
			OnArenaContinuePressed));

		Visible = true;
		GetTree().Paused = true;
	}

	public void ShowDungeonCleared(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "DUNGEON CLEARED!";
		_subLabel.Text = $"{defeatedBossName} has been defeated.\nHead to camp and prepare for the next dungeon.";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Rest at Camp  ▶",
			new Color(0.10f, 0.14f, 0.18f),
			new Color(0.28f, 0.52f, 0.75f),
			OnDungeonClearedContinuePressed));

		Visible = true;
		GetTree().Paused = true;
	}

	public void ShowVictoryScreen(int xpGained, int levelsGained, EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "VICTORY!";
		_subLabel.Text = "All dungeons have been conquered. The realm is saved!";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Play Again",
			new Color(0.18f, 0.14f, 0.10f), new Color(0.65f, 0.52f, 0.28f), OnPlayAgainPressed));
		_btnRow.AddChild(MakeButton("Main Menu",
			new Color(0.14f, 0.11f, 0.09f), new Color(0.45f, 0.38f, 0.22f), OnMainMenuPressed));

		Visible = true;
		GetTree().Paused = true;
	}

	// ── dev test complete ─────────────────────────────────────────────────────

	/// <summary>
	/// Shown when a boss is killed during a developer test fight (launched via
	/// the Ctrl+Alt+O popup). Skips normal run progression and returns straight
	/// to the Overworld with a full RunState reset.
	/// </summary>
	public void ShowDevTestComplete(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "TEST COMPLETE";
		_subLabel.Text = $"{defeatedBossName} defeated.\n[Dev mode — run state will be reset]";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Back to Overworld  ▶",
			new Color(0.10f, 0.14f, 0.18f),
			new Color(0.28f, 0.60f, 0.80f),
			OnDevTestCompletePressed));

		Visible = true;
		GetTree().Paused = true;
	}

	void OnDevTestCompletePressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	// ── button callbacks ──────────────────────────────────────────────────────

	void OnArenaContinuePressed()
	{
		GetTree().Paused = false;
		RunState.Instance.AdvanceBossInDungeon();
		GlobalAutoLoad.Reset();
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	void OnDungeonClearedContinuePressed()
	{
		GetTree().Paused = false;
		RunState.Instance.CompleteDungeon();
		GlobalAutoLoad.Reset();
		GetTree().ChangeSceneToFile("res://levels/Camp.tscn");
	}

	void OnPlayAgainPressed()
	{
		GetTree().Paused = false;
		RunHistoryStore.FinalizeRun(true);
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		RunHistoryStore.FinalizeRun(true);
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── private fields ────────────────────────────────────────────────────────

	Label _titleLabel = null!;
	Label _subLabel = null!;
	Label _xpLabel = null!;
	Control _itemSection = null!;
	HBoxContainer _btnRow = null!;

	// ── helpers ───────────────────────────────────────────────────────────────

	void ClearButtons()
	{
		foreach (var child in _btnRow.GetChildren()) child.QueueFree();
	}

	// ── item drop display ─────────────────────────────────────────────────────

	/// <summary>
	/// Populates (or clears) the item section of the victory screen.
	///
	/// Shows an EquipmentPane with the new item highlighted in the inventory,
	/// so the player can drag it directly into the correct equipment slot.
	/// </summary>
	void BuildItemSection(EquippableItem? item)
	{
		foreach (var child in _itemSection.GetChildren()) child.QueueFree();

		if (item == null)
		{
			_itemSection.Visible = false;
			return;
		}

		_itemSection.Visible = true;

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.50f, 0.40f, 0.22f, 0.55f));
		_itemSection.AddChild(sep);

		var headerLabel = new Label();
		headerLabel.Text = "✦  Item Found!  —  Drag it to its equipment slot to equip";
		headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
		headerLabel.AddThemeFontSizeOverride("font_size", 14);
		headerLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.72f, 0.50f));
		_itemSection.AddChild(headerLabel);

		// EquipmentPane with the new item highlighted — player drag-and-drops to equip.
		var pane = new EquipmentPane(item);
		pane.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		pane.CustomMinimumSize = new Vector2(520f, 190f);
		_itemSection.AddChild(pane);
	}

	static string BuildXpLine(int xpGained, int levelsGained)
	{
		var xpText = $"+{xpGained} XP  •  Level {PlayerProgressStore.Level}  •  " +
		             $"XP: {PlayerProgressStore.CurrentXp}/{PlayerProgressStore.XpToNextLevel(PlayerProgressStore.Level)}";
		if (levelsGained > 0)
		{
			var pointWord = levelsGained == 1 ? "point" : "points";
			xpText += levelsGained == 1
				? $"\n✦  LEVEL UP!  ✦  +1 talent point  •  All party members gain +{PlayerProgressStore.MaxHealthBonusPerLevel:0} max health"
				: $"\n✦  LEVEL UP ×{levelsGained}!  ✦  +{levelsGained} talent {pointWord}  •  All party members gain +{levelsGained * PlayerProgressStore.MaxHealthBonusPerLevel:0} max health";
		}

		return xpText;
	}

	static Button MakeButton(string text, Color bgColor, Color borderColor, System.Action onPressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(190f, 52f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

		var normal = MakeStyle(bgColor, borderColor);
		var hover = MakeStyle(
			new Color(bgColor.R + 0.08f, bgColor.G + 0.06f, bgColor.B + 0.04f),
			borderColor * 1.3f);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);
		btn.Pressed += onPressed;
		return btn;
	}

	static StyleBoxFlat MakeStyle(Color bg, Color border)
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
}