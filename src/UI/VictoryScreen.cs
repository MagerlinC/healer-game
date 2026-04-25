using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.Items;

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
		Layer       = 20;
		Visible     = false;
		ProcessMode = ProcessModeEnum.Always;

		_audioPlayer.Stream   = GD.Load<AudioStream>(AssetConstants.VictorySoundPath);
		_audioPlayer.VolumeDb = -4f;
		AddChild(_audioPlayer);

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.Died),
			Callable.From((Character character) =>
			{
				if (character.IsFriendly) return;

				// Snapshot this boss encounter before clearing the rolling log.
				RunHistoryStore.RecordBossEncounter(character.CharacterName);
				CombatLog.Clear();

				// Award XP.
				var dungeon    = RunState.Instance.CurrentDungeon;
				var bossIndex  = RunState.Instance.CurrentBossIndexInDungeon;
				var xpReward   = bossIndex < dungeon.XpRewards.Length
					? dungeon.XpRewards[bossIndex]
					: 100;
				var levelsGained = PlayerProgressStore.AddXp(xpReward);

				// Roll for item drop.
				var droppedItem = ItemRegistry.RollDrop(character.CharacterName);
				if (droppedItem != null)
					ItemStore.AddToInventory(droppedItem);

				if (!RunState.Instance.IsLastBossInDungeon)
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
		overlay.Color       = new Color(0f, 0f, 0f, 0.80f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// ── Centred content column ────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical   = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 20);
		overlay.AddChild(vbox);

		_titleLabel = new Label();
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 48);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.50f));
		vbox.AddChild(_titleLabel);

		_subLabel = new Label();
		_subLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_subLabel.AutowrapMode        = TextServer.AutowrapMode.Word;
		_subLabel.AddThemeFontSizeOverride("font_size", 18);
		_subLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		vbox.AddChild(_subLabel);

		_xpLabel = new Label();
		_xpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_xpLabel.AutowrapMode        = TextServer.AutowrapMode.Word;
		_xpLabel.AddThemeFontSizeOverride("font_size", 16);
		_xpLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.95f));
		vbox.AddChild(_xpLabel);

		// Item drop section — populated on boss death when loot drops, hidden otherwise.
		_itemSection = new VBoxContainer();
		_itemSection.Visible = false;
		vbox.AddChild(_itemSection);

		_btnRow = new HBoxContainer();
		_btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		_btnRow.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(_btnRow);
	}

	// ── public API ────────────────────────────────────────────────────────────

	public void ShowArenaCleared(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "ARENA CLEARED!";
		_subLabel.Text   = $"{defeatedBossName} has been defeated.\nPrepare for the next battle.";
		_xpLabel.Text    = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Continue  ▶",
			new Color(0.10f, 0.16f, 0.10f),
			new Color(0.30f, 0.65f, 0.28f),
			OnArenaContinuePressed));

		Visible          = true;
		GetTree().Paused = true;
	}

	public void ShowDungeonCleared(string defeatedBossName, int xpGained, int levelsGained,
		EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "DUNGEON CLEARED!";
		_subLabel.Text   = $"{defeatedBossName} has been defeated.\nHead to camp and prepare for the next dungeon.";
		_xpLabel.Text    = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Rest at Camp  ▶",
			new Color(0.10f, 0.14f, 0.18f),
			new Color(0.28f, 0.52f, 0.75f),
			OnDungeonClearedContinuePressed));

		Visible          = true;
		GetTree().Paused = true;
	}

	public void ShowVictoryScreen(int xpGained, int levelsGained, EquippableItem? droppedItem = null)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "VICTORY!";
		_subLabel.Text   = "All dungeons have been conquered. The realm is saved!";
		_xpLabel.Text    = BuildXpLine(xpGained, levelsGained);
		BuildItemSection(droppedItem);

		ClearButtons();
		_btnRow.AddChild(MakeButton("Play Again",
			new Color(0.18f, 0.14f, 0.10f), new Color(0.65f, 0.52f, 0.28f), OnPlayAgainPressed));
		_btnRow.AddChild(MakeButton("Main Menu",
			new Color(0.14f, 0.11f, 0.09f), new Color(0.45f, 0.38f, 0.22f), OnMainMenuPressed));

		Visible          = true;
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

	// ── private fields ────────────────────────────────────────────────────────

	Label _titleLabel = null!;
	Label _subLabel   = null!;
	Label _xpLabel    = null!;
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
	/// Shows the dropped item's icon, name (rarity-coloured), and an equip button
	/// if the slot is currently empty — otherwise "Added to Armory".
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

		var dropLabel = new Label();
		dropLabel.Text = "✦  Item Found!";
		dropLabel.HorizontalAlignment = HorizontalAlignment.Center;
		dropLabel.AddThemeFontSizeOverride("font_size", 14);
		dropLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.72f, 0.50f));
		_itemSection.AddChild(dropLabel);

		// Icon + name row
		var row = new HBoxContainer();
		row.Alignment = BoxContainer.AlignmentMode.Center;
		row.AddThemeConstantOverride("separation", 10);
		_itemSection.AddChild(row);

		if (item.Icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture     = item.Icon;
			iconRect.CustomMinimumSize = new Vector2(40f, 40f);
			iconRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			row.AddChild(iconRect);
		}

		var nameCol = new VBoxContainer();
		nameCol.AddThemeConstantOverride("separation", 2);
		row.AddChild(nameCol);

		var rarityLabel = new Label();
		rarityLabel.Text = item.Rarity.ToString().ToUpper();
		rarityLabel.AddThemeFontSizeOverride("font_size", 10);
		rarityLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		rarityLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		nameCol.AddChild(rarityLabel);

		var nameLabel = new Label();
		nameLabel.Text = item.Name;
		nameLabel.AddThemeFontSizeOverride("font_size", 16);
		nameLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		nameCol.AddChild(nameLabel);

		var descLabel = new Label();
		descLabel.Text = item.Description;
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.68f));
		descLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		nameCol.AddChild(descLabel);

		// Equip / swap button — always available.
		// If the slot is occupied, ItemStore.Equip displaces the old item to inventory.
		var currentlyEquipped = ItemStore.GetEquipped(item.Slot);
		var isEquipped        = currentlyEquipped != null && currentlyEquipped == item;

		if (!isEquipped)
		{
			if (currentlyEquipped != null)
			{
				// Show what's currently equipped so the player can make an informed swap.
				var vsLabel = new Label();
				vsLabel.Text = "▼  Currently Equipped";
				vsLabel.HorizontalAlignment = HorizontalAlignment.Center;
				vsLabel.AddThemeFontSizeOverride("font_size", 11);
				vsLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.48f, 0.44f));
				_itemSection.AddChild(vsLabel);

				var oldRow = new HBoxContainer();
				oldRow.Alignment = BoxContainer.AlignmentMode.Center;
				oldRow.AddThemeConstantOverride("separation", 10);
				_itemSection.AddChild(oldRow);

				if (currentlyEquipped.Icon != null)
				{
					var oldIcon = new TextureRect();
					oldIcon.Texture           = currentlyEquipped.Icon;
					oldIcon.CustomMinimumSize = new Vector2(40f, 40f);
					oldIcon.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
					oldIcon.StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered;
					oldIcon.MouseFilter       = Control.MouseFilterEnum.Ignore;
					oldRow.AddChild(oldIcon);
				}

				var oldCol = new VBoxContainer();
				oldCol.AddThemeConstantOverride("separation", 2);
				oldRow.AddChild(oldCol);

				var oldRarityLabel = new Label();
				oldRarityLabel.Text = currentlyEquipped.Rarity.ToString().ToUpper();
				oldRarityLabel.AddThemeFontSizeOverride("font_size", 10);
				oldRarityLabel.AddThemeColorOverride("font_color", RarityColor(currentlyEquipped.Rarity));
				oldRarityLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				oldCol.AddChild(oldRarityLabel);

				var oldNameLabel = new Label();
				oldNameLabel.Text = currentlyEquipped.Name;
				oldNameLabel.AddThemeFontSizeOverride("font_size", 15);
				oldNameLabel.AddThemeColorOverride("font_color", RarityColor(currentlyEquipped.Rarity));
				oldNameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				oldCol.AddChild(oldNameLabel);

				var oldDescLabel = new Label();
				oldDescLabel.Text = currentlyEquipped.Description;
				oldDescLabel.AddThemeFontSizeOverride("font_size", 12);
				oldDescLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
				oldDescLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				oldCol.AddChild(oldDescLabel);

				var intoArmoryLabel = new Label();
				intoArmoryLabel.Text = "→ moves to your Armory";
				intoArmoryLabel.HorizontalAlignment = HorizontalAlignment.Center;
				intoArmoryLabel.AddThemeFontSizeOverride("font_size", 11);
				intoArmoryLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.48f, 0.44f));
				_itemSection.AddChild(intoArmoryLabel);
			}

			var equipBtn = MakeButton(currentlyEquipped == null ? "Equip Now" : "Swap",
				new Color(0.10f, 0.10f, 0.16f),
				RarityColor(item.Rarity),
				() =>
				{
					ItemStore.Equip(item); // displaces old item to inventory automatically
					foreach (var child in _itemSection.GetChildren()) child.QueueFree();
					BuildItemSection(item);
				});
			equipBtn.CustomMinimumSize = new Vector2(160f, 38f);
			var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			btnRow.AddChild(equipBtn);
			_itemSection.AddChild(btnRow);
		}
		else
		{
			// Item was just equipped — confirm to player.
			var equippedLabel = new Label();
			equippedLabel.Text = "✓  Equipped";
			equippedLabel.HorizontalAlignment = HorizontalAlignment.Center;
			equippedLabel.AddThemeFontSizeOverride("font_size", 13);
			equippedLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.80f, 0.45f));
			_itemSection.AddChild(equippedLabel);
		}
	}

	static Color RarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Rare      => new Color(0.35f, 0.55f, 1.00f),   // blue
		ItemRarity.Epic      => new Color(0.70f, 0.30f, 0.90f),   // purple
		ItemRarity.Legendary => new Color(1.00f, 0.55f, 0.05f),   // orange
		_                    => new Color(0.80f, 0.78f, 0.72f)
	};

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
		btn.Text              = text;
		btn.CustomMinimumSize = new Vector2(190f, 52f);
		btn.SizeFlagsHorizontal     = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color",       new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

		var normal = MakeStyle(bgColor, borderColor);
		var hover  = MakeStyle(
			new Color(bgColor.R + 0.08f, bgColor.G + 0.06f, bgColor.B + 0.04f),
			borderColor * 1.3f);
		btn.AddThemeStyleboxOverride("normal",  normal);
		btn.AddThemeStyleboxOverride("hover",   hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus",   normal);
		btn.Pressed += onPressed;
		return btn;
	}

	static StyleBoxFlat MakeStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor          = border;
		s.ContentMarginLeft    = s.ContentMarginRight  = 16f;
		s.ContentMarginTop     = s.ContentMarginBottom = 10f;
		return s;
	}
}
