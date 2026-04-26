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
				var xpReward = bossIndex < dungeon.XpRewards.Length
					? dungeon.XpRewards[bossIndex]
					: 100;
				var levelsGained = PlayerProgressStore.AddXp(xpReward);

				// Roll for item drop.
				var droppedItem = ItemRegistry.RollDrop(character.CharacterName);
				if (droppedItem != null && !ItemStore.HasItem(droppedItem.ItemId))
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
	/// Layout: a horizontal row with two bordered cards side-by-side.
	///   Left  — the newly found item, with an "Equip" button underneath.
	///   Right — the currently equipped item (if any), with a "→ moves to your
	///           Armory" hint underneath. Absent when the slot is empty.
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
		headerLabel.Text = "✦  Item Found!";
		headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
		headerLabel.AddThemeFontSizeOverride("font_size", 14);
		headerLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.72f, 0.50f));
		_itemSection.AddChild(headerLabel);

		// ── Two-card row ──────────────────────────────────────────────────────
		var currentlyEquipped = ItemStore.GetEquipped(item.Slot);
		var isEquipped = currentlyEquipped != null && currentlyEquipped == item;

		var cardRow = new HBoxContainer();
		cardRow.Alignment = BoxContainer.AlignmentMode.Center;
		cardRow.AddThemeConstantOverride("separation", 16);
		_itemSection.AddChild(cardRow);

		// Left card — found item
		cardRow.AddChild(BuildItemCard(
			item,
			null,
			default,
			isEquipped
				? null
				: MakeButton("Equip",
					new Color(0.10f, 0.10f, 0.16f),
					RarityColor(item.Rarity),
					() =>
					{
						ItemStore.Equip(item);
						foreach (var child in _itemSection.GetChildren()) child.QueueFree();
						BuildItemSection(item);
					}),
			isEquipped
		));

		// Right card — currently equipped item (only when slot is occupied)
		if (currentlyEquipped != null && !isEquipped)
		{
			cardRow.AddChild(BuildItemCard(
				currentlyEquipped,
				"→ moves to your Armory",
				new Color(0.50f, 0.48f, 0.44f),
				null,
				false
			));
		}
	}

	/// <summary>
	/// Builds a single bordered item card for use in the two-card comparison row.
	/// </summary>
	Control BuildItemCard(EquippableItem item, string? footerText, Color footerColor,
		Button? button, bool confirmedEquip)
	{
		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = new Color(0.08f, 0.07f, 0.07f, 0.95f);
		cardStyle.SetCornerRadiusAll(6);
		cardStyle.SetBorderWidthAll(2);
		cardStyle.BorderColor = confirmedEquip
			? new Color(0.40f, 0.80f, 0.45f) // green when just equipped
			: RarityColor(item.Rarity);
		cardStyle.ContentMarginLeft = cardStyle.ContentMarginRight = 14f;
		cardStyle.ContentMarginTop = cardStyle.ContentMarginBottom = 12f;

		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", cardStyle);
		card.CustomMinimumSize = new Vector2(200f, 0f);
		card.MouseFilter = Control.MouseFilterEnum.Ignore;

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		col.MouseFilter = Control.MouseFilterEnum.Ignore;
		card.AddChild(col);

		// Icon
		if (item.Icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture = item.Icon;
			iconRect.CustomMinimumSize = new Vector2(48f, 48f);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			col.AddChild(iconRect);
		}

		// Rarity
		var rarityLabel = new Label();
		rarityLabel.Text = item.Rarity.ToString().ToUpper();
		rarityLabel.HorizontalAlignment = HorizontalAlignment.Center;
		rarityLabel.AddThemeFontSizeOverride("font_size", 10);
		rarityLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		rarityLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		col.AddChild(rarityLabel);

		// Name
		var nameLabel = new Label();
		nameLabel.Text = item.Name;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.AddThemeFontSizeOverride("font_size", 15);
		nameLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		col.AddChild(nameLabel);

		// Description / stats
		var descLabel = new Label();
		descLabel.Text = item.Description;
		descLabel.HorizontalAlignment = HorizontalAlignment.Center;
		descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.68f));
		descLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		col.AddChild(descLabel);

		// Optional footer hint (e.g. "→ moves to your Armory")
		if (footerText != null)
		{
			var footer = new Label();
			footer.Text = footerText;
			footer.HorizontalAlignment = HorizontalAlignment.Center;
			footer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			footer.AddThemeFontSizeOverride("font_size", 11);
			footer.AddThemeColorOverride("font_color", footerColor);
			footer.MouseFilter = Control.MouseFilterEnum.Ignore;
			col.AddChild(footer);
		}

		// Optional action button (Equip) or confirmed-equip label
		if (confirmedEquip)
		{
			var doneLabel = new Label();
			doneLabel.Text = "✓  Equipped";
			doneLabel.HorizontalAlignment = HorizontalAlignment.Center;
			doneLabel.AddThemeFontSizeOverride("font_size", 13);
			doneLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.80f, 0.45f));
			doneLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			col.AddChild(doneLabel);
		}
		else if (button != null)
		{
			button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			col.AddChild(button);
		}

		return card;
	}

	static Color RarityColor(ItemRarity rarity)
	{
		return rarity switch
		{
			ItemRarity.Rare => new Color(0.35f, 0.55f, 1.00f), // blue
			ItemRarity.Epic => new Color(0.70f, 0.30f, 0.90f), // purple
			ItemRarity.Legendary => new Color(1.00f, 0.55f, 0.05f), // orange
			_ => new Color(0.80f, 0.78f, 0.72f)
		};
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