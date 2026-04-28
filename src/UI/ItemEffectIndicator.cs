using Godot;

/// <summary>
/// A small icon badge displayed below the healer's party frame while an item
/// effect is active (e.g. the Pendant of the Void's Embrace charge).
///
/// Differs from <see cref="EffectIndicator"/> in two ways:
/// <list type="bullet">
///   <item>No countdown timer — item effects are binary (active/consumed),
///     not duration-based.</item>
///   <item>Gold border instead of green/red so item effects are visually
///     distinct from spell/talent effects at a glance.</item>
/// </list>
///
/// Hovering the badge shows the shared <see cref="GameTooltip"/> with the
/// effect's display name and description.
/// </summary>
public partial class ItemEffectIndicator : PanelContainer
{
	// ── public ────────────────────────────────────────────────────────────────
	public string EffectId { get; }

	// ── private ───────────────────────────────────────────────────────────────
	readonly string _displayName;
	readonly string _description;
	bool _hovered;

	// Gold/legendary colour — matches the rarity tier of items that show procs.
	static readonly Color ItemEffectBorder = new(0.85f, 0.65f, 0.10f, 0.95f);

	// ── constructor ───────────────────────────────────────────────────────────
	public ItemEffectIndicator(string effectId, Texture2D? icon, string displayName, string description, int size = 32)
	{
		EffectId = effectId;
		_displayName = displayName;
		_description = description;

		CustomMinimumSize = new Vector2(size, size);
		MouseFilter = MouseFilterEnum.Stop; // must be non-Ignore to receive mouse events

		// ── badge style ──────────────────────────────────────────────────────
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		style.SetCornerRadiusAll(3);
		style.SetBorderWidthAll(2); // slightly thicker than spell-effect badges
		style.BorderColor = ItemEffectBorder;
		style.ContentMarginLeft = 1f;
		style.ContentMarginRight = 1f;
		style.ContentMarginTop = 1f;
		style.ContentMarginBottom = 1f;
		AddThemeStyleboxOverride("panel", style);

		// Stacking layer for icon (no countdown label needed)
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(inner);

		if (icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture = icon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = MouseFilterEnum.Ignore;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// ── tooltip wiring ───────────────────────────────────────────────────
		MouseEntered += () =>
		{
			_hovered = true;
			GameTooltip.Show(_description, _displayName);
		};
		MouseExited += () =>
		{
			_hovered = false;
			GameTooltip.Hide();
		};
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (_hovered)
			GameTooltip.Show(_description, _displayName);
	}

}