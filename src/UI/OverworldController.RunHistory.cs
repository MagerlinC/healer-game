#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.Runes;

/// <summary>
/// Partial class — run history panel and encounter detail modal for <see cref="OverworldController"/>.
///
/// Extracted from <c>OverworldController.cs</c> to keep history/logging concerns
/// in a dedicated file.  All members here are part of the same
/// <see cref="OverworldController"/> class instance.
/// </summary>
public partial class OverworldController
{
	// ── run-history panel ─────────────────────────────────────────────────────
	CanvasLayer? _historyPanel;
	VBoxContainer? _historyContent;

	// ── encounter detail modal ─────────────────────────────────────────────────
	CanvasLayer? _detailModalLayer;
	Label? _detailModalTitle;
	VBoxContainer? _detailModalContent;

	static readonly HashSet<string> PartyMemberNames = new()
	{
		GameConstants.HealerName,
		GameConstants.TemplarName,
		GameConstants.AssassinName,
		GameConstants.WizardName
	};

	// ── panel open ────────────────────────────────────────────────────────────

	void OpenHistoryPanel()
	{
		RebuildHistoryContent();
		OpenPanel(_historyPanel!);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// RUN HISTORY PANE
	// ══════════════════════════════════════════════════════════════════════════

	internal Control BuildRunHistoryPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(scroll);

		_historyContent = new VBoxContainer();
		_historyContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_historyContent.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(_historyContent);

		RebuildHistoryContent();
		return margin;
	}

	void RebuildHistoryContent()
	{
		if (_historyContent == null) return;
		foreach (var child in _historyContent.GetChildren()) child.QueueFree();

		var runs = RunHistoryStore.History;
		if (runs.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No runs recorded yet.\nComplete or attempt a run to see your history here.";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AutowrapMode = TextServer.AutowrapMode.Word;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 15);
			empty.AddThemeColorOverride("font_color", HintColor);
			_historyContent.AddChild(empty);
			return;
		}

		for (var i = runs.Count - 1; i >= 0; i--)
		{
			_historyContent.AddChild(BuildRunEntry(i + 1, runs[i]));
			if (i > 0)
			{
				var sep = new HSeparator();
				sep.AddThemeColorOverride("color", SepColor);
				_historyContent.AddChild(sep);
			}
		}
	}

	Control BuildRunEntry(int runNumber, RunHistoryStore.RunRecord run)
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// ── Run header row ────────────────────────────────────────────────────
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(header);

		var runLabel = new Label();
		runLabel.Text = $"Run #{runNumber}  •  {run.CompletedAt:MMM d, yyyy  h:mm tt}";
		runLabel.AddThemeFontSizeOverride("font_size", 14);
		runLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.78f, 0.72f));
		runLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(runLabel);

		var m = (int)run.Duration.TotalMinutes;
		var s = run.Duration.Seconds;
		var durationLabel = new Label();
		durationLabel.Text = $"{m}:{s:D2}";
		durationLabel.AddThemeFontSizeOverride("font_size", 13);
		durationLabel.AddThemeColorOverride("font_color", HintColor);
		header.AddChild(durationLabel);

		var outcome = new Label();
		outcome.Text = run.IsVictory ? "VICTORY" : "DEFEAT";
		outcome.AddThemeFontSizeOverride("font_size", 14);
		outcome.AddThemeColorOverride("font_color",
			run.IsVictory ? new Color(0.40f, 0.85f, 0.35f) : new Color(0.85f, 0.28f, 0.22f));
		header.AddChild(outcome);

		// ── Rune icons ────────────────────────────────────────────────────────
		// Only shown on records that have rune data (null = legacy save, skip).
		if (run.ActiveRuneIndices != null)
		{
			var runeRow = new HBoxContainer();
			runeRow.AddThemeConstantOverride("separation", 3);
			header.AddChild(runeRow);

			if (run.ActiveRuneIndices.Count == 0)
			{
				var noRunesLabel = new Label();
				noRunesLabel.Text = "No runes";
				noRunesLabel.AddThemeFontSizeOverride("font_size", 11);
				noRunesLabel.AddThemeColorOverride("font_color", new Color(0.38f, 0.34f, 0.28f));
				runeRow.AddChild(noRunesLabel);
			}
			else
			{
				foreach (var runeIndex in run.ActiveRuneIndices)
				{
					var icon = new TextureRect();
					icon.CustomMinimumSize = new Vector2(22f, 22f);
					icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
					icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
					icon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(runeIndex));
					icon.TooltipText = ((RuneIndex)runeIndex).ToString();
					runeRow.AddChild(icon);
				}
			}
		}

		// ── Per-boss encounter rows grouped by dungeon ───────────────────────
		// Group encounters by DungeonName, preserving their original order.
		// Encounters from old saves (DungeonName == null) are grouped together
		// under a null key and rendered without a dungeon header.
		var dungeonGroups = run.BossEncounters
			.Select((enc, idx) => (enc, idx))
			.GroupBy(x => x.enc.DungeonName)
			.ToList();

		// Re-order groups so null (legacy) encounters appear first, then named
		// dungeons in the order of their first encounter.
		var orderedGroups = dungeonGroups
			.OrderBy(g => g.Min(x => x.idx))
			.ToList();

		foreach (var group in orderedGroups)
		{
			// ── Dungeon subheader (only when a name is known) ─────────────────
			if (group.Key != null)
			{
				var dungeonHeader = new Label();
				dungeonHeader.Text = group.Key;
				dungeonHeader.AddThemeFontSizeOverride("font_size", 12);
				dungeonHeader.AddThemeColorOverride("font_color", new Color(0.65f, 0.55f, 0.35f));
				dungeonHeader.MouseFilter = Control.MouseFilterEnum.Ignore;
				vbox.AddChild(dungeonHeader);
			}

			foreach (var (enc, _) in group)
			{
				var encNormal = new StyleBoxFlat();
				encNormal.BgColor = new Color(0f, 0f, 0f, 0f);
				var encHover = new StyleBoxFlat();
				encHover.BgColor = new Color(1f, 1f, 1f, 0.04f);
				encHover.SetCornerRadiusAll(4);

				var encPanel = new PanelContainer();
				encPanel.AddThemeStyleboxOverride("panel", encNormal);
				encPanel.MouseFilter = Control.MouseFilterEnum.Stop;
				encPanel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
				vbox.AddChild(encPanel);

				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 16);
				// Prevent the inner HBox from stealing clicks from the panel
				row.MouseFilter = Control.MouseFilterEnum.Ignore;
				encPanel.AddChild(row);

				var pad = new Control();
				pad.CustomMinimumSize = new Vector2(24f, 0f);
				pad.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(pad);

				var bossName = new Label();
				bossName.Text = enc.BossName;
				bossName.CustomMinimumSize = new Vector2(160f, 0f);
				bossName.AddThemeFontSizeOverride("font_size", 13);
				bossName.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
				bossName.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(bossName);

				var healLabel = new Label();
				healLabel.Text = $"Healing: {enc.TotalHealing:N0}";
				healLabel.AddThemeFontSizeOverride("font_size", 13);
				healLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.85f, 0.55f));
				healLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(healLabel);

				var dmgDealtLabel = new Label();
				dmgDealtLabel.Text = $"Damage dealt: {enc.TotalDamageDealt:N0}";
				dmgDealtLabel.AddThemeFontSizeOverride("font_size", 13);
				dmgDealtLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.44f, 0.28f));
				dmgDealtLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(dmgDealtLabel);

				var dmgTakenLabel = new Label();
				dmgTakenLabel.Text = $"Damage taken: {enc.TotalDamageTaken:N0}";
				dmgTakenLabel.AddThemeFontSizeOverride("font_size", 13);
				dmgTakenLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.44f, 0.28f));
				dmgTakenLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(dmgTakenLabel);

				// Right-aligned "Details ▸" hint
				var spacer = new Control();
				spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				spacer.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(spacer);

				var detailsHint = new Label();
				detailsHint.Text = "Details ▸";
				detailsHint.AddThemeFontSizeOverride("font_size", 12);
				detailsHint.AddThemeColorOverride("font_color", new Color(0.50f, 0.46f, 0.38f));
				detailsHint.MouseFilter = Control.MouseFilterEnum.Ignore;
				row.AddChild(detailsHint);

				// Hover and click wiring
				var capturedEnc = enc;
				encPanel.MouseEntered += () =>
				{
					encPanel.AddThemeStyleboxOverride("panel", encHover);
					detailsHint.AddThemeColorOverride("font_color", TitleColor);
				};
				encPanel.MouseExited += () =>
				{
					encPanel.AddThemeStyleboxOverride("panel", encNormal);
					detailsHint.AddThemeColorOverride("font_color", new Color(0.50f, 0.46f, 0.38f));
				};
				encPanel.GuiInput += (ev) =>
				{
					if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
						ShowEncounterDetail(capturedEnc);
				};
			}
		}

		// ── Loadout section (spells, talents, items) ─────────────────────────
		// Only shown on records that have loadout data (null = legacy save, skip).
		if (run.SpellLoadout != null || run.TalentsAcquired != null)
		{
			var loadoutSep = new HSeparator();
			loadoutSep.AddThemeColorOverride("color", SepColor);
			vbox.AddChild(loadoutSep);

			// Toggle button row
			var toggleBtn = new Button();
			toggleBtn.Text = "Loadout  ▸";
			toggleBtn.Flat = true;
			toggleBtn.Alignment = HorizontalAlignment.Left;
			toggleBtn.AddThemeFontSizeOverride("font_size", 13);
			toggleBtn.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.42f));
			toggleBtn.AddThemeColorOverride("font_hover_color", TitleColor);
			toggleBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			vbox.AddChild(toggleBtn);

			// Collapsible body
			var loadoutBody = new VBoxContainer();
			loadoutBody.AddThemeConstantOverride("separation", 8);
			loadoutBody.Visible = false;
			vbox.AddChild(loadoutBody);

			toggleBtn.Pressed += () =>
			{
				loadoutBody.Visible = !loadoutBody.Visible;
				toggleBtn.Text = loadoutBody.Visible ? "Loadout  ▾" : "Loadout  ▸";
			};

			// ── SPELLS ──────────────────────────────────────────────────────────
			if (run.SpellLoadout is { Count: > 0 })
			{
				var spellHeader = new Label();
				spellHeader.Text = "SPELLS";
				spellHeader.AddThemeFontSizeOverride("font_size", 11);
				spellHeader.AddThemeColorOverride("font_color", new Color(0.45f, 0.42f, 0.35f));
				loadoutBody.AddChild(spellHeader);

				var spellFlow = new HFlowContainer();
				spellFlow.AddThemeConstantOverride("h_separation", 8);
				spellFlow.AddThemeConstantOverride("v_separation", 4);
				loadoutBody.AddChild(spellFlow);

				foreach (var spell in run.SpellLoadout)
				{
					var spellLabel = new Label();
					spellLabel.Text = spell.Name;
					spellLabel.AddThemeFontSizeOverride("font_size", 13);
					spellLabel.AddThemeColorOverride("font_color", SchoolColor(spell.School));
					spellLabel.TooltipText = spell.School;
					spellFlow.AddChild(spellLabel);
				}
			}

			// ── TALENTS ─────────────────────────────────────────────────────────
			if (run.TalentsAcquired is { Count: > 0 })
			{
				var talentHeader = new Label();
				talentHeader.Text = "TALENTS";
				talentHeader.AddThemeFontSizeOverride("font_size", 11);
				talentHeader.AddThemeColorOverride("font_color", new Color(0.45f, 0.42f, 0.35f));
				loadoutBody.AddChild(talentHeader);

				var talentFlow = new HFlowContainer();
				talentFlow.AddThemeConstantOverride("h_separation", 8);
				talentFlow.AddThemeConstantOverride("v_separation", 4);
				loadoutBody.AddChild(talentFlow);

				foreach (var talent in run.TalentsAcquired)
				{
					var talentLabel = new Label();
					talentLabel.Text = talent.Name;
					talentLabel.AddThemeFontSizeOverride("font_size", 13);
					talentLabel.AddThemeColorOverride("font_color", SchoolColor(talent.School));
					talentLabel.MouseFilter = Control.MouseFilterEnum.Stop;
					talentLabel.TooltipText = $"{talent.School}\n{talent.Description}";
					talentFlow.AddChild(talentLabel);
				}
			}

			// ── ITEMS ────────────────────────────────────────────────────────────
			if (run.ItemsUsed is { Count: > 0 })
			{
				var itemHeader = new Label();
				itemHeader.Text = "ITEMS";
				itemHeader.AddThemeFontSizeOverride("font_size", 11);
				itemHeader.AddThemeColorOverride("font_color", new Color(0.45f, 0.42f, 0.35f));
				loadoutBody.AddChild(itemHeader);

				var itemFlow = new HFlowContainer();
				itemFlow.AddThemeConstantOverride("h_separation", 8);
				itemFlow.AddThemeConstantOverride("v_separation", 4);
				loadoutBody.AddChild(itemFlow);

				foreach (var item in run.ItemsUsed)
				{
					var itemLabel = new Label();
					itemLabel.Text = item;
					itemLabel.AddThemeFontSizeOverride("font_size", 13);
					itemLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.72f, 0.55f));
					itemFlow.AddChild(itemLabel);
				}
			}
		}

		return vbox;
	}

	/// <summary>Maps a spell school name string to its display colour.</summary>
	static Color SchoolColor(string school) => school switch
	{
		"Holy"        => new Color(0.95f, 0.88f, 0.35f),   // gold
		"Nature"      => new Color(0.38f, 0.88f, 0.48f),   // green
		"Void"        => new Color(0.72f, 0.45f, 0.95f),   // purple
		"Chronomancy" => new Color(0.38f, 0.78f, 0.98f),   // cyan-blue
		_             => new Color(0.85f, 0.80f, 0.75f)    // neutral
	};

	// ══════════════════════════════════════════════════════════════════════════
	// ENCOUNTER DETAIL MODAL
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Builds the reusable encounter detail modal at CanvasLayer 15.
	/// Content is cleared and repopulated each time <see cref="ShowEncounterDetail"/> runs.
	/// </summary>
	internal CanvasLayer BuildDetailModal()
	{
		var layer = new CanvasLayer { Layer = 15 };
		layer.Visible = false;

		// Full-screen dimmer — also acts as a click-outside-to-close target
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.60f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		dimmer.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				layer.Visible = false;
		};

		// Inset margin so the panel doesn't stretch edge-to-edge
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 180);
		margin.AddThemeConstantOverride("margin_right", 180);
		margin.AddThemeConstantOverride("margin_top", 60);
		margin.AddThemeConstantOverride("margin_bottom", 60);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(margin);

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
		// Stop clicks from falling through to the dimmer close handler
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		margin.AddChild(panel);

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

		// Scrollable content area — populated in ShowEncounterDetail
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

	/// <summary>
	/// Populates the detail modal with stats and per-ability tables for
	/// <paramref name="enc"/>, then makes the modal visible.
	/// </summary>
	void ShowEncounterDetail(RunHistoryStore.BossEncounterRecord enc)
	{
		if (_detailModalContent == null || _detailModalLayer == null) return;

		// Clear previous content
		foreach (var child in _detailModalContent.GetChildren()) child.QueueFree();

		_detailModalTitle!.Text = $"{enc.BossName}  —  Fight Details";

		// ── Summary stat pills ────────────────────────────────────────────────
		var statsRow = new HBoxContainer();
		statsRow.Alignment = BoxContainer.AlignmentMode.Center;
		statsRow.AddThemeConstantOverride("separation", 48);
		_detailModalContent.AddChild(statsRow);

		statsRow.AddChild(MakeStatPill("Healing Done", $"{enc.TotalHealing:N0}", new Color(0.35f, 0.80f, 0.50f)));
		statsRow.AddChild(MakeStatPill("Damage Dealt", $"{enc.TotalDamageDealt:N0}", new Color(0.88f, 0.55f, 0.28f)));
		statsRow.AddChild(MakeStatPill("Damage Taken", $"{enc.TotalDamageTaken:N0}", new Color(0.85f, 0.28f, 0.22f)));

		// ── Per-ability tables ────────────────────────────────────────────────
		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.DamageDealt);
		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.DamageTaken);
		BuildDetailSection(_detailModalContent, enc.Events, DetailSection.Healing);

		_detailModalLayer.Visible = true;
	}

	// ── detail modal helpers ──────────────────────────────────────────────────

	/// <summary>
	/// A vertical label pair: large coloured value above a small grey descriptor.
	/// </summary>
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

	/// <summary>
	/// Appends a section separator, header label, and ability-breakdown table to
	/// <paramref name="parent"/>.
	///
	/// Damage taken: grouped by (ability, source), filtered to party-member targets.
	/// Healing done: grouped by (ability, target), all healing events.
	/// </summary>
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

		// For healing, context = who was healed (target).
		// For damage,  context = what ability from which source (source).
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

		// ── Separator + section header ────────────────────────────────────────
		var topSep = new HSeparator();
		topSep.AddThemeColorOverride("color", SepColor);
		parent.AddChild(topSep);

		var sectionHeader = new Label();
		sectionHeader.Text = section switch
		{
			DetailSection.Healing => "HEALING DONE",
			DetailSection.DamageDealt => "DAMAGE DEALT",
			DetailSection.DamageTaken => "DAMAGE TAKEN"
		};
		sectionHeader.HorizontalAlignment = HorizontalAlignment.Center;
		sectionHeader.AddThemeFontSizeOverride("font_size", 14);
		sectionHeader.AddThemeColorOverride("font_color",
			isHealing ? new Color(0.35f, 0.80f, 0.50f) : new Color(0.85f, 0.45f, 0.20f));
		parent.AddChild(sectionHeader);

		// ── Table ─────────────────────────────────────────────────────────────
		var table = new VBoxContainer();
		table.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		table.AddThemeConstantOverride("separation", 1);
		parent.AddChild(table);

		var contextColHeader = isHealing ? "Target" : "Source";
		var totalColHeader = isHealing ? "Total Healed" : "Total Dmg";

		table.AddChild(MakeDetailRow("Ability", contextColHeader, "Hits", "Crits", totalColHeader,
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
				abilityDescription: row.Description
			));
			alt = !alt;
		}

		AddDetailSeparator(table, new Color(0.42f, 0.38f, 0.33f, 0.55f));
		table.AddChild(MakeDetailRow("Total", "", "", "", $"{grandTotal:0}",
			false, isTotal: true, isHealing: isHealing));
	}

	/// <summary>
	/// Builds a five-column table row: Ability | Context | Hits | Crits | Total.
	/// Column widths are identical for damage and healing tables so they align.
	/// </summary>
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
		// Wire description tooltip on data rows that have one
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

		// Total (right-aligned, larger on the total row)
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
}