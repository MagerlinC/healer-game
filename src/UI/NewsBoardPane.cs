#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy.UI;

/// <summary>
/// Content pane for the News Board interactible.
///
/// Extends <see cref="VBoxContainer"/> so Godot's container layout automatically
/// sizes its children — the same pattern used by the other content panes that are
/// passed to <see cref="LoadoutController.BuildOverlayPanel"/>.
///
/// Two internal views:
///   1. <b>Topic list</b> — a scrollable list of available entries, each with a
///      "NEW!" badge when unlocked but not yet read.  Clicking an entry navigates
///      to the detail view and marks the entry as seen.
///   2. <b>Detail view</b> — full content for the selected topic, with a "← Back"
///      button that returns to the topic list.
///
/// The pane holds a reference to the scene's exclamation <see cref="Sprite2D"/> and
/// refreshes its visibility whenever an entry is marked as seen.
///
/// Call <see cref="ResetToTopicList"/> before opening so the list is always shown
/// first, even if the pane was left on a detail view previously.
/// </summary>
public partial class NewsBoardPane : VBoxContainer
{
	// ── palette ───────────────────────────────────────────────────────────────
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color BodyColor = new(0.82f, 0.78f, 0.72f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);
	static readonly Color NewBadgeBg = new(0.55f, 0.18f, 0.10f);
	static readonly Color NewBadgeFg = new(1.00f, 0.85f, 0.65f);

	static readonly Color RowNormalBg = new(0.10f, 0.08f, 0.06f, 0.80f);
	static readonly Color RowHoverBg = new(0.18f, 0.14f, 0.10f, 0.90f);
	static readonly Color RowBorder = new(0.40f, 0.32f, 0.18f);
	static readonly Color RowBorderHover = new(0.75f, 0.60f, 0.28f);

	// ── injected reference ────────────────────────────────────────────────────

	/// <summary>
	/// World-space sprite shown above the News Board interactible.
	/// Updated whenever board entries are read so the exclamation disappears
	/// as soon as the player views the last unread entry.
	/// </summary>
	public Sprite2D? ExclamationSprite { get; set; }

	// ── Godot lifecycle ───────────────────────────────────────────────────────

	public override void _Ready()
	{
		AddThemeConstantOverride("separation", 0);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		BuildTopicList();
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Rebuilds the topic list view.  Call this each time the panel is opened
	/// so the player always starts at the index rather than a stale detail view.
	/// </summary>
	public void ResetToTopicList()
	{
		BuildTopicList();
	}

	// ── topic list view ───────────────────────────────────────────────────────

	void BuildTopicList()
	{
		ClearChildren();

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(scroll);

		var list = new VBoxContainer();
		list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);

		// Tutorial — always visible
		list.AddChild(BuildTopicRow(
			"📖  Welcome to Healer Fantasy",
			"Tutorial: your guide to dungeon delving",
			false,
			() => ShowDetail(BuildTutorialDetail())));

		// Ultimate Abilities — unlocked after 3 school talents in a run
		if (PlayerProgressStore.HasUnlockedUltimateEntry)
		{
			var isNew = !PlayerProgressStore.HasSeenUltimateEntry;
			list.AddChild(BuildTopicRow(
				"✦  Ultimate Abilities",
				"Powerful school-specific spells that unlock when you deepen your mastery of a magic school.",
				isNew,
				() =>
				{
					PlayerProgressStore.MarkUltimateEntrySeen();
					RefreshExclamation();
					ShowDetail(BuildUltimateDetail());
				}));
		}

		// Runes — unlocked after defeating the Queen
		if (PlayerProgressStore.HasUnlockedRuneEntry)
		{
			var isNew = !PlayerProgressStore.HasSeenRuneEntry;
			list.AddChild(BuildTopicRow(
				"◈  Runes",
				"Ancient artefacts unlocking the deepest depths of dungeons.",
				isNew,
				() =>
				{
					PlayerProgressStore.MarkRuneEntrySeen();
					RefreshExclamation();
					ShowDetail(BuildRuneDetail());
				}));
		}

		// Footer hint
		var hint = new Label();
		hint.Text = "Click an entry to read more.";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		list.AddChild(hint);
	}

	Control BuildTopicRow(string title, string subtitle, bool isNew, Action onClicked)
	{
		var rowStyle = MakeStyleBox(RowNormalBg, RowBorder, 6);
		var rowStyleHover = MakeStyleBox(RowHoverBg, RowBorderHover, 6);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", rowStyle);
		panel.MouseDefaultCursorShape = CursorShape.PointingHand;
		panel.MouseFilter = MouseFilterEnum.Stop;

		var inner = new HBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		panel.AddChild(inner);

		var textCol = new VBoxContainer();
		textCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		textCol.AddThemeConstantOverride("separation", 3);
		inner.AddChild(textCol);

		// Title + optional NEW badge
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		textCol.AddChild(titleRow);

		var titleLabel = new Label();
		titleLabel.Text = title;
		titleLabel.AddThemeFontSizeOverride("font_size", 14);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleLabel.MouseFilter = MouseFilterEnum.Ignore;
		titleRow.AddChild(titleLabel);

		if (isNew)
		{
			var badge = new Label();
			badge.Text = " NEW ";
			badge.VerticalAlignment = VerticalAlignment.Center;
			badge.AddThemeFontSizeOverride("font_size", 10);
			badge.AddThemeColorOverride("font_color", NewBadgeFg);

			var badgePanel = new PanelContainer();
			badgePanel.AddThemeStyleboxOverride("panel", MakeStyleBox(NewBadgeBg, NewBadgeBg, 3));
			badgePanel.MouseFilter = MouseFilterEnum.Ignore;
			badgePanel.AddChild(badge);
			titleRow.AddChild(badgePanel);
		}

		var subtitleLabel = new Label();
		subtitleLabel.Text = subtitle;
		subtitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		subtitleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		subtitleLabel.AddThemeFontSizeOverride("font_size", 12);
		subtitleLabel.AddThemeColorOverride("font_color", BodyColor);
		subtitleLabel.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(subtitleLabel);

		// Arrow chevron
		var arrow = new Label();
		arrow.Text = "›";
		arrow.VerticalAlignment = VerticalAlignment.Center;
		arrow.AddThemeFontSizeOverride("font_size", 20);
		arrow.AddThemeColorOverride("font_color", new Color(0.55f, 0.48f, 0.35f));
		arrow.MouseFilter = MouseFilterEnum.Ignore;
		inner.AddChild(arrow);

		panel.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", rowStyleHover);
		panel.MouseExited += () => panel.AddThemeStyleboxOverride("panel", rowStyle);
		panel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				panel.AcceptEvent();
				onClicked();
			}
		};

		return panel;
	}

	// ── detail view ───────────────────────────────────────────────────────────

	void ShowDetail(Control content)
	{
		ClearChildren();

		// ← Back button
		var backBtn = new Button();
		backBtn.Text = "←  Back to board";
		backBtn.Flat = true;
		backBtn.MouseDefaultCursorShape = CursorShape.PointingHand;
		backBtn.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		backBtn.AddThemeFontSizeOverride("font_size", 13);
		backBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		backBtn.AddThemeColorOverride("font_hover_color", TitleColor);
		backBtn.Pressed += ResetToTopicList;
		AddChild(backBtn);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		AddChild(sep);

		content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		content.SizeFlagsVertical = SizeFlags.ExpandFill;
		AddChild(content);
	}

	// ── entry content builders ────────────────────────────────────────────────

	static Control BuildTutorialDetail()
	{
		return TutorialContent.Build();
	}

	static Control BuildUltimateDetail()
	{
		var scroll = new ScrollContainer();
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 20);
		scroll.AddChild(vbox);

		vbox.AddChild(TutorialContent.MakeSection("✦ Ultimate Abilities",
			"Ultimate Abilities are powerful spells tied to a specific school of magic, unlocked after selecting 3 talents in that school.\n" +
			"Ultimate abilities are separate from your regular spell slots and occupy a dedicated Ultimate slot in your Spellbook."));

		return scroll;
	}

	static Control BuildRuneDetail()
	{
		var scroll = new ScrollContainer();
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 20);
		scroll.AddChild(vbox);

		vbox.AddChild(TutorialContent.MakeSection("◈  Runes",
			"Runes are ancient artefacts that you can activate before a run to delve deeper into more challenging runs.\n" +
			"You can toggle runes on or off at the Rune Table in the Overworld. " +
			"Runes you haven't unlocked yet are hidden. Complete a run with all current runes active to unlock the next one."));

		return scroll;
	}

	static StyleBoxFlat MakeStyleBox(Color bg, Color border, int cornerRadius = 4)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(cornerRadius);
		s.SetBorderWidthAll(1);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 12f;
		s.ContentMarginTop = s.ContentMarginBottom = 10f;
		return s;
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	/// <summary>Removes all children of this pane so a fresh view can be built.</summary>
	void ClearChildren()
	{
		foreach (var child in GetChildren())
			child.QueueFree();
	}

	void RefreshExclamation()
	{
		if (ExclamationSprite != null)
			ExclamationSprite.Visible = PlayerProgressStore.HasUnreadBoardEntries;
	}
}