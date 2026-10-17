using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.LastLight.Hud;

/// <summary>
/// Full-screen death overlay shown when every party member has died: the last
/// light goes out. A <c>faded_light</c> shader closes the darkness in over the
/// frozen battlefield, then the title and recap rise out of it.
///
/// Sits on CanvasLayer 20 (above GameUI at layer 10) and is hidden by default.
/// <see cref="ShowDeathScreen"/> snapshots the CombatLog, clears it, then
/// builds a damage-taken recap before making the overlay visible and pausing
/// the scene tree so all game logic freezes while the overlay is up.
///
/// ProcessMode is Always so the overlay and its buttons keep receiving input
/// even while the tree is paused.
/// </summary>
public partial class DeathScreen : CanvasLayer
{
	/// <summary>The four friendly party member names used to filter damage-taken events.</summary>
	static readonly HashSet<string> PartyMemberNames = new()
	{
		GameConstants.LightkeeperName,
		GameConstants.DawnwardenName,
		GameConstants.DuskbladeName,
		GameConstants.StarcallerName
	};

	readonly HashSet<string> _deadPartyMembers = new();

	/// <summary>How long the darkness takes to swallow the last light.</summary>
	const float DarkenDuration = 3.2f;

	/// <summary>Container populated by <see cref="BuildDamageTakenRecap"/> at death-time.</summary>
	VBoxContainer _damageSectionContainer = null!;

	ShaderMaterial _darkMat = null!;
	Label _title = null!;
	Label _subtitle = null!;
	Control _details = null!;
	readonly List<Button> _buttons = new();
	readonly AudioStreamPlayer _audioPlayer = new();
	float _time;

	void OnCharacterDied(Character character)
	{
		if (character.IsFriendly)
			_deadPartyMembers.Add(character.CharacterName);

		if (_deadPartyMembers.Count >= 4)
			ShowDeathScreen();
	}

	public override void _Ready()
	{
		Layer = 20;
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;

		_audioPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeathSoundPath);
		_audioPlayer.VolumeDb = -4f;
		AddChild(_audioPlayer);

		GlobalAutoLoad.SubscribeToSignal(nameof(Character.Died), Callable.From((Character character) => OnCharacterDied(character)));

		// ── The dark closing in — also blocks all mouse events from the game ─
		_darkMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/LastLight/faded_light.gdshader") };
		var overlay = new ColorRect();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Material = _darkMat;
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// ── Centred content column ────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 10);
		overlay.AddChild(vbox);

		// Title — dim ash, with the last of the ember's glow behind it
		_title = new Label();
		_title.Text = "Your light has faded";
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.AddThemeFontOverride("font", LastLightHud.Serif);
		_title.AddThemeFontSizeOverride("font_size", 46);
		_title.AddThemeColorOverride("font_color", new Color(0.78f, 0.70f, 0.60f));
		_title.AddThemeColorOverride("font_shadow_color", new Color(LastLightHud.Ember, 0.35f));
		_title.AddThemeConstantOverride("shadow_offset_x", 0);
		_title.AddThemeConstantOverride("shadow_offset_y", 0);
		_title.AddThemeConstantOverride("shadow_outline_size", 14);
		vbox.AddChild(_title);

		_subtitle = new Label();
		_subtitle.Text = "The darkness has claimed your party.";
		_subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		_subtitle.AddThemeFontSizeOverride("font_size", 16);
		_subtitle.AddThemeColorOverride("font_color", LastLightHud.TextDim);
		vbox.AddChild(_subtitle);

		// Everything below the title rises in once the dark has settled.
		var details = new VBoxContainer();
		details.AddThemeConstantOverride("separation", 24);
		vbox.AddChild(details);
		_details = details;

		details.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f) });

		// ── Damage recap (populated lazily in ShowDeathScreen) ────────────────
		_damageSectionContainer = new VBoxContainer();
		_damageSectionContainer.AddThemeConstantOverride("separation", 6);
		details.AddChild(_damageSectionContainer);

		// ── Button row ────────────────────────────────────────────────────────
		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		details.AddChild(btnRow);

		btnRow.AddChild(MakeButton("New Run", new Color(0.55f, 0.26f, 0.12f), OnNewRunPressed));
		btnRow.AddChild(MakeButton("Main Menu", new Color(0.35f, 0.30f, 0.22f), OnMainMenuPressed));
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;

		// The ember behind the title gutters like a flame about to go out.
		_time += (float)delta;
		var flicker = 0.22f + 0.10f * Mathf.Sin(_time * 2.3f) + 0.06f * Mathf.Sin(_time * 7.1f + 1.3f);
		_title.AddThemeColorOverride("font_shadow_color", new Color(LastLightHud.Ember, Mathf.Max(flicker, 0f)));
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Makes the overlay visible and pauses all game logic.
	/// Snapshots the CombatLog before clearing it so the damage recap can be shown.
	/// Safe to call multiple times (subsequent calls are no-ops).
	/// </summary>
	public void ShowDeathScreen()
	{
		if (Visible) return;

		// Snapshot the rolling log BEFORE clearing it so the recap has data.
		var snapshot = CombatLog.Snapshot();

		RunHistoryStore.RecordBossEncounter(RunState.Instance?.CurrentBossName ?? "Unknown");
		CombatLog.Clear();
		RunHistoryStore.FinalizeRun(false);

		BuildDamageTakenRecap(snapshot);

		Visible = true;
		GetTree().Paused = true;
		PlayFadeOut();
		_audioPlayer.Play();
	}

	// ── private — the light going out ─────────────────────────────────────────

	/// <summary>
	/// Darkness swallows the screen, then the title surfaces, then the recap and
	/// buttons. Buttons ignore the mouse until they are visible so a panicked
	/// click at the moment of death can't skip the screen.
	/// </summary>
	void PlayFadeOut()
	{
		_title.Modulate = Colors.Transparent;
		_subtitle.Modulate = Colors.Transparent;
		_details.Modulate = Colors.Transparent;
		foreach (var btn in _buttons)
			btn.MouseFilter = Control.MouseFilterEnum.Ignore;

		var tween = CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.SetParallel();

		_darkMat.SetShaderParameter("progress", 0f);
		tween.TweenMethod(Callable.From((float p) => _darkMat.SetShaderParameter("progress", p)), 0f, 1f, DarkenDuration)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);

		tween.TweenProperty(_title, "modulate", Colors.White, 1.6f).SetDelay(DarkenDuration * 0.3f);
		tween.TweenProperty(_subtitle, "modulate", Colors.White, 1.2f).SetDelay(DarkenDuration * 0.55f);
		tween.TweenProperty(_details, "modulate", Colors.White, 0.9f).SetDelay(DarkenDuration * 0.8f);

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			foreach (var btn in _buttons)
				btn.MouseFilter = Control.MouseFilterEnum.Stop;
		}));
	}

	// ── private — damage recap ────────────────────────────────────────────────

	void BuildDamageTakenRecap(List<CombatEventRecord> events)
	{
		// Only care about damage that landed on a party member.
		var partyDamage = events
			.Where(e => e.Type == CombatEventType.Damage && PartyMemberNames.Contains(e.TargetName))
			.ToList();

		if (partyDamage.Count == 0) return;

		// Aggregate per (ability, source).
		var rows = partyDamage
			.GroupBy(e => (e.AbilityName, e.SourceName))
			.Select(g => (
				Ability: g.Key.AbilityName,
				Source: g.Key.SourceName,
				Total: g.Sum(e => e.Amount),
				Hits: g.Count(),
				Crits: g.Count(e => e.IsCrit),
				Description: g.First().Description
			))
			.OrderByDescending(r => r.Total)
			.ToList();

		var grandTotal = rows.Sum(r => r.Total);

		// ── top divider ───────────────────────────────────────────────────────
		AddSeparator(new Color(LastLightHud.Iron, 0.7f));

		// ── section label ─────────────────────────────────────────────────────
		var header = new Label();
		header.Text = "WHAT DIMMED YOUR LIGHT";
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.AddThemeFontSizeOverride("font_size", 14);
		header.AddThemeColorOverride("font_color", LastLightHud.Ember);
		_damageSectionContainer.AddChild(header);

		// ── scrollable table ──────────────────────────────────────────────────
		// Show at most ~7 rows before requiring scroll (each row ≈ 28 px).
		const float RowHeight = 28f;
		const float HeaderHeight = 30f;
		const float TableWidth = 560f;

		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(
			TableWidth,
			Math.Min(rows.Count + 1, 7) * RowHeight + HeaderHeight
		);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_damageSectionContainer.AddChild(scroll);

		var table = new VBoxContainer();
		table.AddThemeConstantOverride("separation", 1);
		scroll.AddChild(table);

		// Column header row
		table.AddChild(MakeRow("Ability", "Source", "Hits", "★", "Light Lost", true));
		AddTableSeparator(table);

		// Data rows
		var alt = false;
		foreach (var row in rows)
		{
			table.AddChild(MakeRow(
				row.Ability,
				row.Source,
				row.Hits.ToString(),
				row.Crits > 0 ? row.Crits.ToString() : "–",
				$"{row.Total:0}",
				false,
				alt,
				abilityDescription: row.Description
			));
			alt = !alt;
		}

		// Grand total row
		AddTableSeparator(table);
		table.AddChild(MakeRow("Total", "", "", "", $"{grandTotal:0}", false, isTotal: true));

		// ── bottom divider ────────────────────────────────────────────────────
		AddSeparator(new Color(LastLightHud.Iron, 0.7f));
	}

	// ── row / cell builders ───────────────────────────────────────────────────

	/// <summary>
	/// Builds one table row as a PanelContainer containing five labelled cells.
	/// Column widths: Ability 195 | Source 130 | Hits 44 | Crits 36 | Total 80
	/// </summary>
	static Control MakeRow(
		string ability, string source, string hits, string crits, string total,
		bool isHeader, bool isAlt = false, bool isTotal = false,
		string? abilityDescription = null)
	{
		// Background colours
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

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		panel.AddChild(row);

		// Named colours
		var bodyColor = new Color(0.88f, 0.84f, 0.78f);
		var headerColor = new Color(0.58f, 0.54f, 0.48f);
		var totalColor = new Color(0.95f, 0.55f, 0.35f);
		var sourceColor = new Color(0.65f, 0.60f, 0.55f);
		var critColor = new Color(0.95f, 0.72f, 0.28f);
		var dimColor = new Color(0.48f, 0.44f, 0.40f);

		var baseSize = isHeader ? 12 : 13;

		// ── Ability name ─────────────────────────────────────────────────────
		var abilityColor = isHeader ? headerColor : isTotal ? totalColor : bodyColor;
		var abilityCell = Cell(ability, 195f, abilityColor, isTotal ? 14 : baseSize, HorizontalAlignment.Left);
		// Wire description tooltip on data rows that have one
		if (!isHeader && !isTotal && !string.IsNullOrEmpty(abilityDescription))
		{
			abilityCell.MouseFilter = Control.MouseFilterEnum.Stop;
			abilityCell.MouseEntered += () => GameTooltip.Show(ability, abilityDescription);
			abilityCell.MouseExited += () => GameTooltip.Hide();
		}

		row.AddChild(abilityCell);

		// ── Source ───────────────────────────────────────────────────────────
		var srcColor = isHeader ? headerColor : isTotal ? new Color(0f, 0f, 0f, 0f) : sourceColor;
		row.AddChild(Cell(source, 130f, srcColor, baseSize, HorizontalAlignment.Left));

		// ── Hits ─────────────────────────────────────────────────────────────
		row.AddChild(Cell(hits, 44f, isHeader ? headerColor : isTotal ? dimColor : bodyColor, baseSize, HorizontalAlignment.Center));

		// ── Crits (gold when non-zero) ────────────────────────────────────────
		var critCellColor = isHeader ? headerColor
			: isTotal ? dimColor
			: crits != "–" ? critColor
			: dimColor;
		row.AddChild(Cell(crits, 36f, critCellColor, baseSize, HorizontalAlignment.Center));

		// ── Total damage ─────────────────────────────────────────────────────
		row.AddChild(Cell(total, 80f,
			isHeader ? headerColor : isTotal ? totalColor : bodyColor,
			isTotal ? 14 : baseSize,
			HorizontalAlignment.Right));

		return panel;
	}

	static Label Cell(string text, float minWidth, Color color, int fontSize, HorizontalAlignment align)
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

	void AddSeparator(Color color)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", color);
		_damageSectionContainer.AddChild(sep);
	}

	static void AddTableSeparator(VBoxContainer parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.42f, 0.38f, 0.33f, 0.55f));
		parent.AddChild(sep);
	}

	// ── button callbacks ──────────────────────────────────────────────────────

	/// <summary>Restart from the Overworld with the same RunState loadout.</summary>
	void OnNewRunPressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	/// <summary>Return to Main Menu and reset RunState for a fresh run.</summary>
	void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	Button MakeButton(string text, Color borderColor, Action onPressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(180f, 52f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

		var normal = MakeStyle(new Color(0.16f, 0.11f, 0.11f), borderColor);
		var hover = MakeStyle(new Color(0.26f, 0.16f, 0.14f), borderColor * 1.4f);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);

		btn.Pressed += onPressed;
		UiSfx.WireClick(btn);
		_buttons.Add(btn);
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