using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;

/// <summary>
/// Full-screen death overlay shown when every party member has died.
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
		GameConstants.HealerName,
		GameConstants.TemplarName,
		GameConstants.AssassinName,
		GameConstants.WizardName
	};

	readonly HashSet<string> _deadPartyMembers = new();

	/// <summary>Container populated by <see cref="BuildDamageTakenRecap"/> at death-time.</summary>
	VBoxContainer _damageSectionContainer = null!;

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

		GlobalAutoLoad.SubscribeToSignal(nameof(Character.Died), Callable.From((Character character) => OnCharacterDied(character)));

		// ── Dark overlay — blocks all mouse events from reaching the game ────
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
		vbox.AddThemeConstantOverride("separation", 24);
		overlay.AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "YOUR PARTY HAS FALLEN";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", new Color(0.85f, 0.22f, 0.22f));
		vbox.AddChild(title);

		// ── Damage recap (populated lazily in ShowDeathScreen) ────────────────
		_damageSectionContainer = new VBoxContainer();
		_damageSectionContainer.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(_damageSectionContainer);

		// ── Button row ────────────────────────────────────────────────────────
		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(btnRow);

		btnRow.AddChild(MakeButton("New Run", new Color(0.50f, 0.20f, 0.20f), OnNewRunPressed));
		btnRow.AddChild(MakeButton("Main Menu", new Color(0.35f, 0.30f, 0.22f), OnMainMenuPressed));
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
		AddSeparator(new Color(0.60f, 0.22f, 0.18f, 0.55f));

		// ── section label ─────────────────────────────────────────────────────
		var header = new Label();
		header.Text = "DAMAGE TAKEN";
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.AddThemeFontSizeOverride("font_size", 14);
		header.AddThemeColorOverride("font_color", new Color(0.85f, 0.45f, 0.20f));
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
		table.AddChild(MakeRow("Ability", "Source", "Hits", "★", "Total Dmg", true));
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
		AddSeparator(new Color(0.60f, 0.22f, 0.18f, 0.55f));
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
			abilityCell.MouseEntered += () => GameTooltip.Show($"{ability}\n{abilityDescription}");
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

	static Button MakeButton(string text, Color borderColor, Action onPressed)
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