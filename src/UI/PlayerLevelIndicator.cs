using Godot;

namespace healerfantasy.UI;

public partial class PlayerLevelIndicator : HBoxContainer
{
	Label _levelLabel;
	ProgressBar _xpBar;
	Label _talentPointsLabel;

	public override void _Ready()
	{
		// Root spacing
		AddThemeConstantOverride("separation", 10);

		// --- Circle (Level) ---
		var circlePanel = new Panel
		{
			CustomMinimumSize = new Vector2(64, 64)
		};

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.15f, 0.15f),
			CornerRadiusTopLeft = 32,
			CornerRadiusTopRight = 32,
			CornerRadiusBottomLeft = 32,
			CornerRadiusBottomRight = 32
		};

		circlePanel.AddThemeStyleboxOverride("panel", style);

		var center = new CenterContainer();

		_levelLabel = new Label
		{
			Text = PlayerProgressStore.Level.ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};

		center.AddChild(_levelLabel);
		circlePanel.AddChild(center);

		// --- Right side ---
		var rightContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};

		// XP Bar (use ProgressBar instead of TextureProgressBar)
		_xpBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 100,
			Value = PlayerProgressStore.CurrentXp / PlayerProgressStore.XpPerLevel * 100f,
			CustomMinimumSize = new Vector2(200, 20),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};

		rightContainer.AddChild(_xpBar);

		// Talent Points Indicator
		_talentPointsLabel = new Label
		{
			Text = $"{PlayerProgressStore.TalentPoints} Talent Points Available!",
			Visible = true
		};

		rightContainer.AddChild(_talentPointsLabel);

		// --- Assemble ---
		AddChild(circlePanel);
		AddChild(rightContainer);
	}
}