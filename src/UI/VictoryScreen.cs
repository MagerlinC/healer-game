using Godot;
using healerfantasy;
using healerfantasy.CombatLog;

/// <summary>
/// Overlay shown when a boss dies.
///
/// For encounters 1 and 2 (index 0–1) it shows an "ARENA CLEARED!" screen
/// with a "Continue" button that advances the run to the next boss.
/// For the final encounter (index 2) it shows the full "VICTORY!" screen
/// with "Play Again" and "Main Menu" options.
///
/// Also awards XP on each boss kill and displays level-up notifications.
///
/// Sits on CanvasLayer 20 (same as DeathScreen) and is hidden by default.
/// ProcessMode is Always so buttons keep receiving input while the tree is
/// paused.
/// </summary>
public partial class VictoryScreen : CanvasLayer
{
	AudioStreamPlayer _audioPlayer = new();
	// play victory sound
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
				if (!character.IsFriendly)
				{
					// Snapshot the combat log for this encounter before clearing it.
					RunHistoryStore.RecordBossEncounter(character.CharacterName);
					CombatLog.Clear();

					// Award XP and process level-ups.
					var bossIndex = RunState.Instance.CurrentBossIndex;
					var xpReward = GameConstants.BossXpRewards[bossIndex];
					var levelsGained = PlayerProgressStore.AddXp(xpReward);

					if (RunState.Instance.CurrentBossIndex < 2)
						ShowArenaCleared(character.CharacterName, xpReward, levelsGained);
					else
						ShowVictoryScreen(xpReward, levelsGained);
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

		// Title
		_titleLabel = new Label();
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 48);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.50f));
		vbox.AddChild(_titleLabel);

		// Subtitle
		_subLabel = new Label();
		_subLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_subLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_subLabel.AddThemeFontSizeOverride("font_size", 18);
		_subLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		vbox.AddChild(_subLabel);

		// XP / level-up info row
		_xpLabel = new Label();
		_xpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_xpLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_xpLabel.AddThemeFontSizeOverride("font_size", 16);
		_xpLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.95f));
		vbox.AddChild(_xpLabel);

		// Button row (populated dynamically when the screen is shown)
		_btnRow = new HBoxContainer();
		_btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		_btnRow.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(_btnRow);
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Shows the intermediate "Arena Cleared!" screen between bosses.
	/// </summary>
	public void ShowArenaCleared(string defeatedBossName, int xpGained, int levelsGained)
	{
		if (Visible) return;
		_audioPlayer.Play();
		_titleLabel.Text = "ARENA CLEARED!";
		_subLabel.Text = $"{defeatedBossName} has been defeated.\nPrepare for the next battle.";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);

		// Clear any old buttons and build arena-cleared set.
		foreach (var child in _btnRow.GetChildren())
			child.QueueFree();

		_btnRow.AddChild(MakeButton(
			"Continue  ▶",
			new Color(0.10f, 0.16f, 0.10f),
			new Color(0.30f, 0.65f, 0.28f),
			OnContinuePressed));

		Visible = true;
		GetTree().Paused = true;
	}

	/// <summary>
	/// Shows the final victory screen after the last boss is defeated.
	/// </summary>
	public void ShowVictoryScreen(int xpGained, int levelsGained)
	{
		if (Visible) return;

		_audioPlayer.Play();

		_titleLabel.Text = "VICTORY!";
		_subLabel.Text = "All three arenas have been conquered.";
		_xpLabel.Text = BuildXpLine(xpGained, levelsGained);

		foreach (var child in _btnRow.GetChildren())
			child.QueueFree();

		_btnRow.AddChild(MakeButton("Play Again", new Color(0.18f, 0.14f, 0.10f), new Color(0.65f, 0.52f, 0.28f),
			OnPlayAgainPressed));
		_btnRow.AddChild(MakeButton("Main Menu", new Color(0.14f, 0.11f, 0.09f), new Color(0.45f, 0.38f, 0.22f), OnMainMenuPressed));

		Visible = true;
		GetTree().Paused = true;
	}

	// ── button callbacks ──────────────────────────────────────────────────────

	void OnContinuePressed()
	{
		GetTree().Paused = false;
		RunState.Instance.AdvanceBoss();
		GlobalAutoLoad.Reset();
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	void OnPlayAgainPressed()
	{
		GetTree().Paused = false;
		RunHistoryStore.FinalizeRun(true);
		GlobalAutoLoad.Reset();
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		RunHistoryStore.FinalizeRun(true);
		GlobalAutoLoad.Reset();
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── private fields ────────────────────────────────────────────────────────

	Label _titleLabel = null!;
	Label _subLabel = null!;
	Label _xpLabel = null!;
	HBoxContainer _btnRow = null!;

	// ── helpers ───────────────────────────────────────────────────────────────

	static string BuildXpLine(int xpGained, int levelsGained)
	{
		var xpText = $"+{xpGained} XP  •  Level {PlayerProgressStore.Level}  •  " +
		             $"XP: {PlayerProgressStore.CurrentXp}/{PlayerProgressStore.XpPerLevel}";
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
		var hover = MakeStyle(new Color(bgColor.R + 0.08f, bgColor.G + 0.06f, bgColor.B + 0.04f), borderColor * 1.3f);
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