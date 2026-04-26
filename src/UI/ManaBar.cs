using Godot;
using healerfantasy;

public partial class ManaBar : Control
{
	static readonly Color GlobeShadowColor = new(0.02f, 0.04f, 0.09f, 0.45f);
	static readonly Color GlobeOuterColor = new(0.08f, 0.11f, 0.18f, 0.98f);
	static readonly Color GlobeInnerColor = new(0.03f, 0.05f, 0.09f, 0.98f);
	static readonly Color RimColor = new(0.55f, 0.78f, 0.98f, 0.95f);
	static readonly Color RimGlowColor = new(0.45f, 0.70f, 1.00f, 0.22f);
	static readonly Color ManaBottomColor = new(0.04f, 0.18f, 0.78f, 0.95f);
	static readonly Color ManaTopColor = new(0.44f, 0.82f, 1.00f, 0.92f);
	static readonly Color WaterlineColor = new(0.78f, 0.95f, 1.00f, 0.40f);
	static readonly Color GlossColor = new(0.82f, 0.94f, 1.00f, 0.16f);
	static readonly Color TextShadowColor = new(0f, 0f, 0f, 0.55f);
	static readonly Color TextColor = new(0.92f, 0.97f, 1.00f, 0.98f);

	Label _valueLabel = null!;
	float _currentMana;
	float _maxMana = 1f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(132f, 132f);

		_valueLabel = new Label();
		_valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_valueLabel.VerticalAlignment = VerticalAlignment.Center;
		_valueLabel.MouseFilter = MouseFilterEnum.Ignore;
		_valueLabel.AddThemeColorOverride("font_color", TextColor);
		_valueLabel.AddThemeColorOverride("font_shadow_color", TextShadowColor);
		_valueLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_valueLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(_valueLabel);

		Resized += OnResized;
		OnResized();
		UpdateValueLabel();

		// Subscribe only to the player (slot 1) so enemy characters that also
		// emit ManaChanged (e.g. the Crystal Knight) don't pollute this globe.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.ManaChanged),
			Callable.From((string characterName, float current, float max) =>
			{
				if (characterName == GameConstants.PlayerName) OnManaChanged(current, max);
			})
		);
	}

	public override void _Draw()
	{
		var drawSize = Size;
		var diameter = Mathf.Min(drawSize.X, drawSize.Y) - 8f;
		if (diameter <= 0f)
			return;

		var center = drawSize / 2f;
		var radius = diameter / 2f;
		var ratio = _maxMana <= 0f ? 0f : Mathf.Clamp(_currentMana / _maxMana, 0f, 1f);
		var fillTopY = center.Y + radius - ratio * radius * 2f;

		DrawCircle(center + new Vector2(0f, 4f), radius + 4f, GlobeShadowColor);
		DrawCircle(center, radius, GlobeOuterColor);
		DrawCircle(center, radius - 5f, GlobeInnerColor);

		DrawManaFill(center, radius - 6f, fillTopY, ratio);
		DrawGloss(center, radius - 10f);

		DrawArc(center, radius - 3f, 0f, Mathf.Tau, 96, RimGlowColor, 6f, true);
		DrawArc(center, radius - 1f, 0f, Mathf.Tau, 96, RimColor, 2f, true);
	}

	void OnManaChanged(float current, float max)
	{
		_currentMana = Mathf.Max(current, 0f);
		_maxMana = Mathf.Max(max, 1f);
		UpdateValueLabel();
		QueueRedraw();
	}

	void OnResized()
	{
		if (_valueLabel == null)
			return;

		var diameter = Mathf.Min(Size.X, Size.Y) - 18f;
		var labelHeight = Mathf.Clamp(diameter * 0.28f, 24f, 40f);
		var fontSize = Mathf.Clamp((int)(diameter * 0.16f), 14, 22);

		_valueLabel.Position = new Vector2(0f, Size.Y * 0.5f - labelHeight * 0.5f + diameter * 0.18f);
		_valueLabel.Size = new Vector2(Size.X, labelHeight);
		_valueLabel.AddThemeFontSizeOverride("font_size", fontSize);

		QueueRedraw();
	}

	void UpdateValueLabel()
	{
		if (_valueLabel == null)
			return;

		_valueLabel.Text = $"{Mathf.RoundToInt(_currentMana)} / {Mathf.RoundToInt(_maxMana)}";
	}

	void DrawManaFill(Vector2 center, float radius, float fillTopY, float ratio)
	{
		var top = Mathf.Max(fillTopY, center.Y - radius);
		var bottom = center.Y + radius;
		if (top >= bottom)
			return;

		var startY = Mathf.FloorToInt(top);
		var endY = Mathf.CeilToInt(bottom);

		for (var y = startY; y < endY; y++)
		{
			var sampleY = y + 0.5f;
			var verticalOffset = sampleY - center.Y;
			var halfWidthSquared = radius * radius - verticalOffset * verticalOffset;
			if (halfWidthSquared <= 0f)
				continue;

			var halfW = Mathf.Sqrt(halfWidthSquared);
			var left = center.X - halfW;
			var width = halfW * 2f;
			var t = Mathf.InverseLerp(bottom, top, sampleY);
			var color = ManaBottomColor.Lerp(ManaTopColor, t);

			DrawRect(new Rect2(left, y, width, 1.6f), color);
		}

		if (ratio <= 0f || ratio >= 1f)
			return;

		var waterlineOffset = top - center.Y;
		var waterlineWidthSquared = radius * radius - waterlineOffset * waterlineOffset;
		if (waterlineWidthSquared <= 0f)
			return;

		var halfWidth = Mathf.Sqrt(waterlineWidthSquared);
		DrawLine(
			new Vector2(center.X - halfWidth, top + 1f),
			new Vector2(center.X + halfWidth, top + 1f),
			WaterlineColor,
			2f,
			true);
	}

	void DrawGloss(Vector2 center, float radius)
	{
		for (var i = 0; i < 4; i++)
		{
			var glossRadius = radius * (0.46f - i * 0.06f);
			if (glossRadius <= 0f)
				continue;

			var glossCenter = center + new Vector2(-radius * 0.22f, -radius * 0.28f);
			var alpha = GlossColor.A * (1f - i * 0.22f);
			DrawCircle(glossCenter, glossRadius, new Color(GlossColor.R, GlossColor.G, GlossColor.B, alpha));
		}
	}
}