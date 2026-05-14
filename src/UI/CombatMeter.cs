using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.CombatLog;

/// <summary>
/// A compact panel showing healing-per-second or damage-per-second for each
/// tracked character, with a relative bar fill and a per-ability tooltip on hover.
///
/// Layout (bottom-up):
///   Title label
///   One row per tracked character, sorted highest→lowest rate:
///     [██████░░] Healer   42/s
///
/// Hover a row to see a per-ability breakdown tooltip.
/// </summary>
public partial class CombatMeter : PanelContainer
{
	public enum MeterType
	{
		Healing,
		Damage
	}

	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color PanelBg = new(0.10f, 0.08f, 0.07f, 0.92f);
	static readonly Color PanelBorder = new(0.40f, 0.33f, 0.22f, 0.90f);
	static readonly Color TitleColor = new(0.80f, 0.72f, 0.50f);
	static readonly Color LabelColor = new(0.90f, 0.87f, 0.83f);
	static readonly Color RowBg = new(0.14f, 0.11f, 0.10f, 1f);
	static readonly Color RowHoverBg = new(0.22f, 0.18f, 0.14f, 1f);
	static readonly Color HealFill = new(0.25f, 0.68f, 0.30f, 0.75f);
	static readonly Color DamageFill = new(0.80f, 0.22f, 0.18f, 0.75f);

	// ── config ────────────────────────────────────────────────────────────────
	const double RefreshInterval = 0.5;

	// ── state ─────────────────────────────────────────────────────────────────
	readonly MeterType _type;
	readonly List<string> _trackedNames = new();

	// Per-row cached node refs so we can update in-place and MoveChild to re-sort.
	readonly Dictionary<string, RowRefs> _rowCache = new();

	VBoxContainer _rowContainer;
	double _refreshTimer;

	// ── types ─────────────────────────────────────────────────────────────────
	record RowRefs(Control Outer, ProgressBar Bar, Label NameLbl, Label ValueLbl, StyleBoxFlat BgStyle);

	// ── constructor ───────────────────────────────────────────────────────────
	public CombatMeter(MeterType type)
	{
		_type = type;
	}

	// ── public API ────────────────────────────────────────────────────────────
	/// <summary>
	/// Add a character name to track. Safe to call before or after <c>_Ready</c>:
	/// if the meter is already in the scene tree the row is created immediately,
	/// otherwise it is deferred to <see cref="_Ready"/> via <see cref="BuildRows"/>.
	/// </summary>
	public void RegisterCharacter(string name)
	{
		if (_trackedNames.Contains(name)) return;
		_trackedNames.Add(name);

		// If _Ready has already run, create the row right now.
		if (_rowContainer != null)
		{
			var refs = CreateRow(name, _type == MeterType.Healing ? HealFill : DamageFill);
			_rowCache[name] = refs;
			_rowContainer.AddChild(refs.Outer);
		}
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		BuildPanel();
		BuildRows();
	}

	public override void _Process(double delta)
	{
		_refreshTimer += delta;
		if (_refreshTimer >= RefreshInterval)
		{
			_refreshTimer = 0;
			Refresh();
		}
	}

	// ── layout builders ───────────────────────────────────────────────────────
	void BuildPanel()
	{
		CustomMinimumSize = new Vector2(185f, 0f);

		var style = new StyleBoxFlat();
		style.BgColor = PanelBg;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = PanelBorder;
		style.ContentMarginLeft = 6f;
		style.ContentMarginRight = 6f;
		style.ContentMarginTop = 5f;
		style.ContentMarginBottom = 5f;
		AddThemeStyleboxOverride("panel", style);

		MouseFilter = MouseFilterEnum.Stop;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 3);
		AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = _type == MeterType.Healing ? "HEALING" : "DAMAGE";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 11);
		title.AddThemeColorOverride("font_color", TitleColor);
		title.MouseFilter = MouseFilterEnum.Ignore;
		vbox.AddChild(title);

		// Separator
		var sep = new HSeparator();
		sep.MouseFilter = MouseFilterEnum.Ignore;
		var sepStyle = new StyleBoxFlat();
		sepStyle.BgColor = PanelBorder;
		sepStyle.ContentMarginTop = 1f;
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		vbox.AddChild(sep);

		// Row container
		_rowContainer = new VBoxContainer();
		_rowContainer.AddThemeConstantOverride("separation", 2);
		_rowContainer.MouseFilter = MouseFilterEnum.Pass;
		vbox.AddChild(_rowContainer);
	}

	void BuildRows()
	{
		var fillColor = _type == MeterType.Healing ? HealFill : DamageFill;

		foreach (var name in _trackedNames)
		{
			var refs = CreateRow(name, fillColor);
			_rowCache[name] = refs;
			_rowContainer.AddChild(refs.Outer);
		}
	}

	RowRefs CreateRow(string characterName, Color fillColor)
	{
		// Outer: a fixed-height Control that stacks a ProgressBar + label overlay.
		var outer = new Control();
		outer.CustomMinimumSize = new Vector2(0f, 20f);
		outer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		outer.MouseFilter = MouseFilterEnum.Stop;

		// Background ProgressBar (fills proportionally to rate)
		var bar = new ProgressBar();
		bar.SetAnchorsPreset(LayoutPreset.FullRect);
		bar.MinValue = 0f;
		bar.MaxValue = 1f;
		bar.Value = 0f;
		bar.ShowPercentage = false;
		bar.MouseFilter = MouseFilterEnum.Ignore;

		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = RowBg;
		bar.AddThemeStyleboxOverride("background", bgStyle);

		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = fillColor;
		bar.AddThemeStyleboxOverride("fill", fillStyle);

		outer.AddChild(bar);

		// Label overlay (HBoxContainer anchored to full rect)
		var hbox = new HBoxContainer();
		hbox.SetAnchorsPreset(LayoutPreset.FullRect);
		hbox.AddThemeConstantOverride("separation", 0);
		hbox.MouseFilter = MouseFilterEnum.Ignore;

		var nameLabel = new Label();
		nameLabel.Text = " " + characterName;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameLabel.AddThemeFontSizeOverride("font_size", 11);
		nameLabel.AddThemeColorOverride("font_color", LabelColor);
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		hbox.AddChild(nameLabel);

		var valueLabel = new Label();
		valueLabel.Text = "0/s ";
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.AddThemeFontSizeOverride("font_size", 11);
		valueLabel.AddThemeColorOverride("font_color", LabelColor);
		valueLabel.MouseFilter = MouseFilterEnum.Ignore;
		hbox.AddChild(valueLabel);

		outer.AddChild(hbox);

		// Hover highlight
		outer.MouseEntered += () => OnRowEntered(characterName, bgStyle);
		outer.MouseExited += () => OnRowExited(bgStyle);

		return new RowRefs(outer, bar, nameLabel, valueLabel, bgStyle);
	}


	// ── refresh ───────────────────────────────────────────────────────────────
	void Refresh()
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var type = _type == MeterType.Healing ? CombatEventType.Healing : CombatEventType.Damage;
		var rates = CombatLog.GetRatePerSource(type, now);

		// Sort tracked names by rate descending
		var sorted = _trackedNames
			.Select(n => (name: n, rate: rates.TryGetValue(n, out var r) ? r : 0f))
			.OrderByDescending(x => x.rate)
			.ToArray();

		var maxRate = sorted.Length > 0 && sorted[0].rate > 0f ? sorted[0].rate : 1f;

		for (var i = 0; i < sorted.Length; i++)
		{
			var (name, rate) = sorted[i];
			if (!_rowCache.TryGetValue(name, out var row)) continue;

			row.Bar.MaxValue = maxRate;
			row.Bar.Value = rate;
			row.ValueLbl.Text = rate >= 0.5f ? $"{rate:F0}/s " : "0/s ";

			_rowContainer.MoveChild(row.Outer, i);
		}
	}

	// ── hover / tooltip ───────────────────────────────────────────────────────
	void OnRowEntered(string name, StyleBoxFlat bgStyle)
	{
		bgStyle.BgColor = RowHoverBg;
		var (title, desc) = BuildTooltipContent(name);
		GameTooltip.Show(title, desc);
	}

	void OnRowExited(StyleBoxFlat bgStyle)
	{
		bgStyle.BgColor = RowBg;
		GameTooltip.Hide();
	}

	(string title, string desc) BuildTooltipContent(string sourceName)
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var type = _type == MeterType.Healing ? CombatEventType.Healing : CombatEventType.Damage;
		var breakdown = CombatLog.GetBreakdown(sourceName, type, now);
		var label = _type == MeterType.Healing ? "HPS" : "DPS";
		var title = $"{sourceName}  ({label}, last {CombatLog.DefaultWindow:F0}s)";

		if (breakdown.Count == 0)
			return (title, $"No data in last {CombatLog.DefaultWindow:F0}s");

		// Sort abilities by total descending, compute grand total for %
		var entries = breakdown
			.OrderByDescending(kv => kv.Value)
			.ToArray();

		var grandTotal = entries.Sum(kv => kv.Value);

		var desc = new System.Text.StringBuilder();
		foreach (var (ability, total) in entries)
		{
			var pct = grandTotal > 0f ? total / grandTotal * 100f : 0f;
			desc.AppendLine($"{ability,-16} {total,6:F0}  ({pct:F0}%)");
		}

		return (title, desc.ToString().TrimEnd());
	}
}