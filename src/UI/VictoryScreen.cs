#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.Items;
using healerfantasy.Runes;
using healerfantasy.UI;

/// <summary>
/// Overlay shown when a boss dies.
///
/// Layout: two-column card design.
///   Left column  — victory title, sub-text, XP / level-up info,
///                  <see cref="PlayerLevelIndicator"/>, and the action button.
///   Right column — two cards:
///                    • Items Acquired  (item drop + rune drop, if any)
///                    • Run Logs        (all encounters so far this run,
///                                       with clickable Details drill-down)
///
/// Four possible outcomes after each kill:
///
///   1. Non-final boss in current dungeon → "ARENA CLEARED!"
///      Continue button: advance boss index and reload World.tscn.
///
///   2. Final boss of a non-final dungeon → "DUNGEON CLEARED!"
///      Continue button: mark dungeon as completed and load Camp.tscn.
///      (Applies after dungeons 0, 1, and 2; leads to camps 0, 1, and 2
///      respectively before the next dungeon becomes available.)
///
///   3. Final boss of the final dungeon (The Frozen Peak, dungeon 3) → "VICTORY!"
///      Play Again / Main Menu buttons: finalize the run.
///
///   4. Dev test fight → "TEST COMPLETE"
///      Returns to Overworld without affecting run progression.
///
/// XP is awarded on every kill. Level-up notifications are displayed.
///
/// Sits on CanvasLayer 20 and is hidden by default.
/// ProcessMode is Always so buttons keep receiving input while the tree is paused.
/// </summary>
public partial class VictoryScreen : CanvasLayer
{
	// ── palette (matches LoadoutController / OverworldController) ─────────────
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);

	static readonly HashSet<string> PartyMemberNames = new()
	{
		GameConstants.HealerName,
		GameConstants.TemplarName,
		GameConstants.AssassinName,
		GameConstants.WizardName
	};

	AudioStreamPlayer _audioPlayer = new();

	// ── UI refs ───────────────────────────────────────────────────────────────
	Label _titleLabel = null!;
	Label _subLabel = null!;
	Label _xpGainLabel = null!;
	Label _levelUpLabel = null!;
	PlayerLevelIndicator _levelIndicator = null!;
	VBoxContainer _itemsCardContent = null!;
	VBoxContainer _runLogsContent = null!;
	HBoxContainer _btnRow = null!;

	// ── encounter detail modal ────────────────────────────────────────────────
	CanvasLayer _detailModalLayer = null!;
	Label _detailModalTitle = null!;
	VBoxContainer _detailModalContent = null!;

	// ── lifecycle ─────────────────────────────────────────────────────────────

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
					ShowDevTestComplete(character.CharacterName, xpReward, levelsGained, droppedItem);
				else if (!RunState.Instance.IsLastBossInDungeon)
					ShowArenaCleared(character.CharacterName, xpReward, levelsGained, droppedItem);
				else if (!RunState.Instance.IsLastDungeon)
					ShowDungeonCleared(character.CharacterName, xpReward, levelsGained, droppedItem);
				else
					ShowVictoryScreen(xpReward, levelsGained, droppedItem);
			}));

		BuildLayout();

		// Encounter detail modal sits above the victory screen (layer 21).
		_detailModalLayer = BuildDetailModal();
		AddChild(_detailModalLayer);
	}

	// ── public show API ───────────────────────────────────────────────────────

	public void ShowArenaCleared(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "ARENA CLEARED!";
		_subLabel.Text = $"{defeatedBossName} has been defeated.\nPrepare for the next battle.";
		RefreshXpSection(xpGained, levelsGained);
		PopulateItemsCard(droppedItem);
		PopulateRunLogsCard();

		ClearButtons();
		_btnRow.AddChild(MakeButton("Continue  ▶",
			new Color(0.10f, 0.16f, 0.10f), new Color(0.30f, 0.65f, 0.28f), OnArenaContinuePressed));

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
		RefreshXpSection(xpGained, levelsGained);
		PopulateItemsCard(droppedItem);
		PopulateRunLogsCard();

		ClearButtons();
		_btnRow.AddChild(MakeButton("Rest at Camp  ▶",
			new Color(0.10f, 0.14f, 0.18f), new Color(0.28f, 0.52f, 0.75f), OnDungeonClearedContinuePressed));

		Visible = true;
		GetTree().Paused = true;
	}

	public void ShowVictoryScreen(int xpGained, int levelsGained, EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();

		// ── Rune progression ──────────────────────────────────────────────────
		// The Queen of the Frozen Wastes drops the next rune if the player ran
		// with ALL previously acquired runes active (i.e. active count ==
		// acquired count) and there are still runes left to unlock.
		var acquired = RuneStore.AcquiredRuneCount;
		var active = RunState.Instance.ActiveRuneCount;
		var runeDropped = false;
		var runeDropName = "";
		var runeIndex = 0;
		if (acquired < RuneStore.TotalRunes && active == acquired)
		{
			RuneStore.UnlockNextRune();
			runeDropped = true;
			runeIndex = acquired + 1;
			runeDropName = runeIndex switch
			{
				1 => "Rune of the Void",
				2 => "Rune of Nature",
				3 => "Rune of Time",
				4 => "Rune of Purity",
				_ => "Unknown Rune"
			};
		}

		_titleLabel.Text = "VICTORY!";
		_subLabel.Text = "The Queen of the Frozen Wastes has fallen.\nAll dungeons conquered — the realm is saved!";
		RefreshXpSection(xpGained, levelsGained);
		PopulateItemsCard(droppedItem, runeDropped, runeDropName, runeIndex);
		PopulateRunLogsCard();

		ClearButtons();
		_btnRow.AddChild(MakeButton("Play Again",
			new Color(0.18f, 0.14f, 0.10f), new Color(0.65f, 0.52f, 0.28f), OnPlayAgainPressed));
		_btnRow.AddChild(MakeButton("Main Menu",
			new Color(0.14f, 0.11f, 0.09f), new Color(0.45f, 0.38f, 0.22f), OnMainMenuPressed));

		Visible = true;
		GetTree().Paused = true;
	}

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
		RefreshXpSection(xpGained, levelsGained);
		PopulateItemsCard(droppedItem);
		PopulateRunLogsCard();

		ClearButtons();
		_btnRow.AddChild(MakeButton("Back to Overworld  ▶",
			new Color(0.10f, 0.14f, 0.18f), new Color(0.28f, 0.60f, 0.80f), OnDevTestCompletePressed));

		Visible = true;
		GetTree().Paused = true;
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

	void OnDevTestCompletePressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	// ── layout builders ───────────────────────────────────────────────────────

	void BuildLayout()
	{
		// Dark overlay — blocks clicks to the game underneath.
		var overlay = new ColorRect();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0f, 0f, 0f, 0.82f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// Outer margin keeps cards away from the screen edges.
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 60);
		margin.AddThemeConstantOverride("margin_right", 60);
		margin.AddThemeConstantOverride("margin_top", 48);
		margin.AddThemeConstantOverride("margin_bottom", 48);
		overlay.AddChild(margin);

		// Two-column HBox.
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 16);
		margin.AddChild(hbox);

		// Left column (36 % width) — title, XP, level indicator, button.
		var left = BuildLeftColumn();
		left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		left.SizeFlagsStretchRatio = 0.36f;
		hbox.AddChild(left);

		// Right column (64 % width) — items card + run-log card.
		var right = BuildRightColumn();
		right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		right.SizeFlagsStretchRatio = 0.64f;
		hbox.AddChild(right);
	}

	/// <summary>
	/// Left panel: title, sub-text, separator, XP gained, level-up notice,
	/// PlayerLevelIndicator, flexible spacer, and the action button(s).
	/// </summary>
	Control BuildLeftColumn()
	{
		var panel = MakeCardPanel();
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Victory title
		_titleLabel = new Label();
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 48);
		_titleLabel.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(_titleLabel);

		// Sub-text (boss name / flavour)
		_subLabel = new Label();
		_subLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_subLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_subLabel.AddThemeFontSizeOverride("font_size", 16);
		_subLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		vbox.AddChild(_subLabel);

		AddSep(vbox);

		// XP gained (large, blue)
		_xpGainLabel = new Label();
		_xpGainLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_xpGainLabel.AddThemeFontSizeOverride("font_size", 26);
		_xpGainLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.95f));
		vbox.AddChild(_xpGainLabel);

		// Level-up notice (hidden unless a level was gained)
		_levelUpLabel = new Label();
		_levelUpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_levelUpLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_levelUpLabel.AddThemeFontSizeOverride("font_size", 15);
		_levelUpLabel.AddThemeColorOverride("font_color", new Color(1.00f, 0.82f, 0.20f));
		_levelUpLabel.Visible = false;
		vbox.AddChild(_levelUpLabel);

		// PlayerLevelIndicator widget (reflects current level / XP / talent points)
		_levelIndicator = new PlayerLevelIndicator();
		_levelIndicator.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.AddChild(_levelIndicator);

		// Flexible spacer — pushes the button to the bottom of the panel.
		var spacer = new Control();
		spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddChild(spacer);

		// Button row (centred; buttons are added dynamically by the show methods)
		_btnRow = new HBoxContainer();
		_btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		_btnRow.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(_btnRow);

		return panel;
	}

	/// <summary>
	/// Right side: two vertically stacked cards — Items Acquired and Run Logs.
	/// </summary>
	Control BuildRightColumn()
	{
		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 12);

		// ── Items Acquired card ───────────────────────────────────────────────
		var itemsPanel = MakeCardPanel();
		itemsPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		itemsPanel.SizeFlagsStretchRatio = 0.42f;
		{
			var inner = new VBoxContainer();
			inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			inner.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			inner.AddThemeConstantOverride("separation", 8);
			itemsPanel.AddChild(inner);

			AddCardTitle(inner, "Items Acquired");
			AddSep(inner);

			var scroll = new ScrollContainer();
			scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			inner.AddChild(scroll);

			_itemsCardContent = new VBoxContainer();
			_itemsCardContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_itemsCardContent.AddThemeConstantOverride("separation", 10);
			scroll.AddChild(_itemsCardContent);
		}
		vbox.AddChild(itemsPanel);

		// ── Run Logs card ─────────────────────────────────────────────────────
		var logsPanel = MakeCardPanel();
		logsPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		logsPanel.SizeFlagsStretchRatio = 0.58f;
		{
			var inner = new VBoxContainer();
			inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			inner.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			inner.AddThemeConstantOverride("separation", 8);
			logsPanel.AddChild(inner);

			AddCardTitle(inner, "Run Logs");
			AddSep(inner);

			var scroll = new ScrollContainer();
			scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			inner.AddChild(scroll);

			_runLogsContent = new VBoxContainer();
			_runLogsContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_runLogsContent.AddThemeConstantOverride("separation", 4);
			scroll.AddChild(_runLogsContent);
		}
		vbox.AddChild(logsPanel);

		return vbox;
	}

	// ── data populators ───────────────────────────────────────────────────────

	/// <summary>
	/// Updates the XP gain label, shows/hides the level-up notice, and
	/// refreshes the <see cref="PlayerLevelIndicator"/>.
	/// </summary>
	void RefreshXpSection(int xpGained, int levelsGained)
	{
		if (PlayerProgressStore.Level >= PlayerProgressStore.MaxLevel)
		{
			// At the level cap AddXp() is a no-op, so there's nothing meaningful
			// to show for XP gained. Display a max-level indicator instead.
			_xpGainLabel.Text = "Max Level";
			_xpGainLabel.AddThemeColorOverride("font_color", TitleColor);
			_levelUpLabel.Visible = false;
		}
		else
		{
			_xpGainLabel.Text = $"+{xpGained} XP";
			_xpGainLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.95f));

			if (levelsGained > 0)
			{
				var pts = levelsGained == 1 ? "point" : "points";
				_levelUpLabel.Text = levelsGained == 1
					? "✦  Level up!  ✦  +1 talent point gained"
					: $"✦  Level up ×{levelsGained}!  ✦  +{levelsGained} talent {pts} gained";
				_levelUpLabel.Visible = true;
			}
			else
			{
				_levelUpLabel.Visible = false;
			}
		}

		_levelIndicator.Refresh();
	}

	/// <summary>
	/// Fills the Items Acquired card.
	/// Shows item drag-to-equip pane and/or rune entry; empty hint if nothing dropped.
	/// </summary>
	void PopulateItemsCard(EquippableItem? item,
		bool runeDropped = false, string runeDropName = "", int runeIndex = 0)
	{
		foreach (var child in _itemsCardContent.GetChildren()) child.QueueFree();

		if (item == null && !runeDropped)
		{
			var empty = new Label();
			empty.Text = "No items found this fight.";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.VerticalAlignment = VerticalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 14);
			empty.AddThemeColorOverride("font_color", HintColor);
			_itemsCardContent.AddChild(empty);
			return;
		}

		// ── Equipment drop ────────────────────────────────────────────────────
		if (item != null)
		{
			var hint = new Label();
			hint.Text = "✦  Item Found!  —  Drag to equipment slot to equip";
			hint.HorizontalAlignment = HorizontalAlignment.Center;
			hint.AddThemeFontSizeOverride("font_size", 12);
			hint.AddThemeColorOverride("font_color", new Color(0.80f, 0.72f, 0.50f));
			//_itemsCardContent.AddChild(hint);

			var pane = new EquipmentPane(item);
			pane.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_itemsCardContent.AddChild(pane);
		}

		// ── Rune drop (full-victory only) ─────────────────────────────────────
		if (runeDropped)
		{
			if (item != null) AddSep(_itemsCardContent);

			var runeRow = new HBoxContainer();
			runeRow.Alignment = BoxContainer.AlignmentMode.Center;
			runeRow.AddThemeConstantOverride("separation", 14);
			_itemsCardContent.AddChild(runeRow);

			var icon = new TextureRect();
			icon.CustomMinimumSize = new Vector2(44f, 44f);
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
			icon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(runeIndex));
			runeRow.AddChild(icon);

			var textVBox = new VBoxContainer();
			textVBox.AddThemeConstantOverride("separation", 3);
			runeRow.AddChild(textVBox);

			var nameLabel = new Label();
			nameLabel.Text = runeDropName;
			nameLabel.AddThemeFontSizeOverride("font_size", 17);
			nameLabel.AddThemeColorOverride("font_color", TitleColor);
			textVBox.AddChild(nameLabel);

			var subLabel = new Label();
			subLabel.Text = "You found a mysterious rune…";
			subLabel.AddThemeFontSizeOverride("font_size", 12);
			subLabel.AddThemeColorOverride("font_color", HintColor);
			textVBox.AddChild(subLabel);
		}
	}

	/// <summary>
	/// Fills the Run Logs card with all encounters from the current run,
	/// grouped by dungeon.  Each row is clickable for a detail drill-down.
	/// </summary>
	void PopulateRunLogsCard()
	{
		foreach (var child in _runLogsContent.GetChildren()) child.QueueFree();

		var encounters = RunHistoryStore.CurrentRunEncounters;
		if (encounters.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No encounters recorded yet.";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 14);
			empty.AddThemeColorOverride("font_color", HintColor);
			_runLogsContent.AddChild(empty);
			return;
		}

		// Group by dungeon name, preserving encounter order.
		var groups = encounters
			.Select((enc, idx) => (enc, idx))
			.GroupBy(x => x.enc.DungeonName)
			.OrderBy(g => g.Min(x => x.idx))
			.ToList();

		foreach (var group in groups)
		{
			if (group.Key != null)
			{
				var dungeonLabel = new Label();
				dungeonLabel.Text = group.Key;
				dungeonLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				dungeonLabel.AddThemeFontSizeOverride("font_size", 12);
				dungeonLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.55f, 0.35f));
				_runLogsContent.AddChild(dungeonLabel);
			}

			foreach (var (enc, _) in group)
				_runLogsContent.AddChild(BuildEncounterRow(enc));
		}
	}

	/// <summary>
	/// One hoverable/clickable row per boss encounter in the run log.
	/// </summary>
	Control BuildEncounterRow(RunHistoryStore.BossEncounterRecord enc)
	{
		var styleNormal = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) };
		var styleHover = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.04f) };
		styleHover.SetCornerRadiusAll(4);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", styleNormal);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(row);

		Label Stat(string text, Color color)
		{
			var lbl = new Label();
			lbl.Text = text;
			lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
			lbl.AddThemeFontSizeOverride("font_size", 13);
			lbl.AddThemeColorOverride("font_color", color);
			return lbl;
		}

		var bossNameLbl = Stat(enc.BossName, new Color(0.88f, 0.84f, 0.78f));
		bossNameLbl.CustomMinimumSize = new Vector2(130f, 0f);
		row.AddChild(bossNameLbl);
		row.AddChild(Stat($"Healing: {enc.TotalHealing:N0}", new Color(0.40f, 0.85f, 0.55f)));
		row.AddChild(Stat($"Damage dealt: {enc.TotalDamageDealt:N0}", new Color(0.88f, 0.44f, 0.28f)));
		row.AddChild(Stat($"Damage taken: {enc.TotalDamageTaken:N0}", new Color(0.88f, 0.44f, 0.28f)));

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		spacer.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(spacer);

		var detailsHint = new Label();
		detailsHint.Text = "Details ▸";
		detailsHint.MouseFilter = Control.MouseFilterEnum.Ignore;
		detailsHint.AddThemeFontSizeOverride("font_size", 12);
		detailsHint.AddThemeColorOverride("font_color", HintColor);
		row.AddChild(detailsHint);

		var captured = enc;
		panel.MouseEntered += () =>
		{
			panel.AddThemeStyleboxOverride("panel", styleHover);
			detailsHint.AddThemeColorOverride("font_color", TitleColor);
		};
		panel.MouseExited += () =>
		{
			panel.AddThemeStyleboxOverride("panel", styleNormal);
			detailsHint.AddThemeColorOverride("font_color", HintColor);
		};
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				ShowEncounterDetail(captured);
		};

		return panel;
	}

	// ── encounter detail modal ────────────────────────────────────────────────

	/// <summary>
	/// Builds the reusable encounter detail modal at CanvasLayer 21
	/// (above the victory screen at 20).  Content is repopulated each time
	/// <see cref="ShowEncounterDetail"/> is called.
	/// </summary>
	CanvasLayer BuildDetailModal()
	{
		var layer = new CanvasLayer { Layer = 21 };
		layer.Visible = false;

		// Full-screen dimmer — click outside to close.
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.60f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);
		dimmer.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				layer.Visible = false;
		};

		// Inset panel
		var outerMargin = new MarginContainer();
		outerMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		outerMargin.AddThemeConstantOverride("margin_left", 180);
		outerMargin.AddThemeConstantOverride("margin_right", 180);
		outerMargin.AddThemeConstantOverride("margin_top", 60);
		outerMargin.AddThemeConstantOverride("margin_bottom", 60);
		outerMargin.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(outerMargin);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = PanelBorder;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 24f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 18f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		outerMargin.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Title bar
		var titleBar = new HBoxContainer();
		vbox.AddChild(titleBar);

		_detailModalTitle = new Label();
		_detailModalTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_detailModalTitle.AddThemeFontSizeOverride("font_size", 20);
		_detailModalTitle.AddThemeColorOverride("font_color", TitleColor);
		titleBar.AddChild(_detailModalTitle);

		var closeBtn = new Button();
		closeBtn.Text = "✕  Close";
		closeBtn.Flat = true;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 14);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		closeBtn.Pressed += () => layer.Visible = false;
		titleBar.AddChild(closeBtn);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(scroll);

		_detailModalContent = new VBoxContainer();
		_detailModalContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_detailModalContent.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(_detailModalContent);

		return layer;
	}

	void ShowEncounterDetail(RunHistoryStore.BossEncounterRecord enc)
	{
		foreach (var child in _detailModalContent.GetChildren()) child.QueueFree();

		_detailModalTitle.Text = $"{enc.BossName}  —  Fight Details";

		// Summary stat pills
		var statsRow = new HBoxContainer();
		statsRow.Alignment = BoxContainer.AlignmentMode.Center;
		statsRow.AddThemeConstantOverride("separation", 48);
		_detailModalContent.AddChild(statsRow);

		statsRow.AddChild(MakeStatPill("Healing Done", $"{enc.TotalHealing:N0}", new Color(0.35f, 0.80f, 0.50f)));
		statsRow.AddChild(MakeStatPill("Damage Dealt", $"{enc.TotalDamageDealt:N0}", new Color(0.88f, 0.55f, 0.28f)));
		statsRow.AddChild(MakeStatPill("Damage Taken", $"{enc.TotalDamageTaken:N0}", new Color(0.85f, 0.28f, 0.22f)));

		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.DamageDealt);
		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.DamageTaken);
		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.Healing);

		_detailModalLayer.Visible = true;
	}

	// ── detail section helpers (mirrors OverworldController.RunHistory.cs) ────

	static Control MakeStatPill(string descriptor, string value, Color valueColor)
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);

		var valueLabel = new Label();
		valueLabel.Text = value;
		valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		valueLabel.AddThemeFontSizeOverride("font_size", 22);
		valueLabel.AddThemeColorOverride("font_color", valueColor);
		vbox.AddChild(valueLabel);

		var descLabel = new Label();
		descLabel.Text = descriptor;
		descLabel.HorizontalAlignment = HorizontalAlignment.Center;
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.AddThemeColorOverride("font_color", new Color(0.48f, 0.44f, 0.38f));
		vbox.AddChild(descLabel);

		return vbox;
	}

	enum DetailSection
	{
		DamageTaken,
		DamageDealt,
		Healing
	}

	static void BuildDetailSection(VBoxContainer parent,
		List<CombatEventRecord> events, DetailSection section)
	{
		var isHealing = section == DetailSection.Healing;
		var filtered = section switch
		{
			DetailSection.DamageTaken => events.Where(e => PartyMemberNames.Contains(e.TargetName)).ToList(),
			DetailSection.Healing => events.Where(e => e.Type == CombatEventType.Healing).ToList(),
			DetailSection.DamageDealt => events
				.Where(e => e.Type == CombatEventType.Damage && !PartyMemberNames.Contains(e.TargetName))
				.ToList(),
			_ => events
		};
		if (filtered.Count == 0) return;

		var rows = filtered
			.GroupBy(e => (e.AbilityName, Context: isHealing ? e.TargetName : e.SourceName))
			.Select(g => (
				Ability: g.Key.AbilityName,
				Context: g.Key.Context,
				Total: g.Sum(e => e.Amount),
				Hits: g.Count(),
				Crits: g.Count(e => e.IsCrit),
				Description: g.First().Description
			))
			.OrderByDescending(r => r.Total)
			.ToList();

		var grandTotal = rows.Sum(r => r.Total);

		var topSep = new HSeparator();
		topSep.AddThemeColorOverride("color", SepColor);
		parent.AddChild(topSep);

		var sectionHeader = new Label();
		sectionHeader.Text = section switch
		{
			DetailSection.Healing => "HEALING DONE",
			DetailSection.DamageDealt => "DAMAGE DEALT",
			DetailSection.DamageTaken => "DAMAGE TAKEN",
			_ => ""
		};
		sectionHeader.HorizontalAlignment = HorizontalAlignment.Center;
		sectionHeader.AddThemeFontSizeOverride("font_size", 14);
		sectionHeader.AddThemeColorOverride("font_color",
			isHealing ? new Color(0.35f, 0.80f, 0.50f) : new Color(0.85f, 0.45f, 0.20f));
		parent.AddChild(sectionHeader);

		var table = new VBoxContainer();
		table.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		table.AddThemeConstantOverride("separation", 1);
		parent.AddChild(table);

		var ctxHeader = isHealing ? "Target" : "Source";
		var totalHeader = isHealing ? "Total Healed" : "Total Dmg";

		table.AddChild(MakeDetailRow("Ability", ctxHeader, "Hits", "Crits", totalHeader,
			true, isHealing: isHealing));
		AddDetailSeparator(table, new Color(0.42f, 0.38f, 0.33f, 0.55f));

		var alt = false;
		foreach (var row in rows)
		{
			table.AddChild(MakeDetailRow(
				row.Ability,
				row.Context,
				row.Hits.ToString(),
				row.Crits > 0 ? row.Crits.ToString() : "–",
				$"{row.Total:0}",
				false, alt, isHealing: isHealing,
				abilityDescription: row.Description));
			alt = !alt;
		}

		AddDetailSeparator(table, new Color(0.42f, 0.38f, 0.33f, 0.55f));
		table.AddChild(MakeDetailRow("Total", "", "", "", $"{grandTotal:0}",
			false, isTotal: true, isHealing: isHealing));
	}

	static Control MakeDetailRow(
		string ability, string context, string hits, string crits, string total,
		bool isHeader, bool isAlt = false, bool isTotal = false, bool isHealing = false,
		string? abilityDescription = null)
	{
		var bg = isHeader ? new Color(0.10f, 0.08f, 0.07f)
			: isTotal ? new Color(0.20f, 0.14f, 0.11f)
			: isAlt ? new Color(0.18f, 0.13f, 0.11f)
			: new Color(0.14f, 0.10f, 0.09f);

		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = bg;
		style.ContentMarginLeft = style.ContentMarginRight = 8f;
		style.ContentMarginTop = style.ContentMarginBottom = 4f;
		panel.AddThemeStyleboxOverride("panel", style);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.AddChild(row);

		var bodyColor = new Color(0.88f, 0.84f, 0.78f);
		var headerColor = new Color(0.58f, 0.54f, 0.48f);
		var totalColor = isHealing ? new Color(0.40f, 0.90f, 0.55f) : new Color(0.95f, 0.55f, 0.35f);
		var contextColor = new Color(0.65f, 0.60f, 0.55f);
		var critColor = new Color(0.95f, 0.72f, 0.28f);
		var dimColor = new Color(0.48f, 0.44f, 0.40f);
		var baseSize = isHeader ? 12 : 13;

		// Ability name
		var abilityColor = isHeader ? headerColor : isTotal ? totalColor : bodyColor;
		var abilityCell = DetailCell(ability, 195f, abilityColor, isTotal ? 14 : baseSize, HorizontalAlignment.Left);
		if (!isHeader && !isTotal && !string.IsNullOrEmpty(abilityDescription))
		{
			abilityCell.MouseFilter = Control.MouseFilterEnum.Stop;
			abilityCell.MouseEntered += () => GameTooltip.Show(ability, abilityDescription);
			abilityCell.MouseExited += () => GameTooltip.Hide();
		}

		row.AddChild(abilityCell);

		// Context (source or target)
		var ctxColor = isHeader ? headerColor : isTotal ? new Color(0f, 0f, 0f, 0f) : contextColor;
		row.AddChild(DetailCell(context, 140f, ctxColor, baseSize, HorizontalAlignment.Left));

		// Hits
		row.AddChild(DetailCell(hits, 44f,
			isHeader ? headerColor : isTotal ? dimColor : bodyColor, baseSize, HorizontalAlignment.Center));

		// Crits (gold when non-zero)
		var critCellColor = isHeader ? headerColor
			: isTotal ? dimColor
			: crits != "–" ? critColor
			: dimColor;
		row.AddChild(DetailCell(crits, 36f, critCellColor, baseSize, HorizontalAlignment.Center));

		// Total
		row.AddChild(DetailCell(total, 90f,
			isHeader ? headerColor : isTotal ? totalColor : bodyColor,
			isTotal ? 14 : baseSize,
			HorizontalAlignment.Right));

		return panel;
	}

	static Label DetailCell(string text, float minWidth, Color color, int fontSize, HorizontalAlignment align)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.CustomMinimumSize = new Vector2(minWidth, 0f);
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		lbl.HorizontalAlignment = align;
		lbl.AddThemeFontSizeOverride("font_size", fontSize);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.ClipText = true;
		return lbl;
	}

	static void AddDetailSeparator(VBoxContainer parent, Color color)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", color);
		parent.AddChild(sep);
	}

	// ── button helpers ────────────────────────────────────────────────────────

	void ClearButtons()
	{
		foreach (var child in _btnRow.GetChildren()) child.QueueFree();
	}

	static Button MakeButton(string text, Color bgColor, Color borderColor, System.Action onPressed)
	{
		var style = new StyleBoxFlat();
		style.BgColor = bgColor;
		style.SetBorderWidthAll(2);
		style.BorderColor = borderColor;
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = style.ContentMarginRight = 18f;
		style.ContentMarginTop = style.ContentMarginBottom = 10f;

		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(190f, 52f);
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeStyleboxOverride("hover", style);
		btn.AddThemeStyleboxOverride("pressed", style);
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.85f));
		btn.Pressed += onPressed;
		return btn;
	}

	// ── shared layout helpers ─────────────────────────────────────────────────

	/// <summary>Creates a styled card PanelContainer with the game's gold border.</summary>
	static PanelContainer MakeCardPanel()
	{
		var style = new StyleBoxFlat();
		style.BgColor = PanelBg;
		style.SetCornerRadiusAll(8);
		style.SetBorderWidthAll(2);
		style.BorderColor = PanelBorder;
		style.ContentMarginLeft = style.ContentMarginRight = 22f;
		style.ContentMarginTop = style.ContentMarginBottom = 18f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", style);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		return panel;
	}

	/// <summary>Adds a gold card-title label to <paramref name="parent"/>.</summary>
	static void AddCardTitle(VBoxContainer parent, string text)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeFontSizeOverride("font_size", 16);
		lbl.AddThemeColorOverride("font_color", TitleColor);
		parent.AddChild(lbl);
	}

	/// <summary>Adds a horizontal separator with the standard gold tint.</summary>
	static void AddSep(VBoxContainer parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		parent.AddChild(sep);
	}
}