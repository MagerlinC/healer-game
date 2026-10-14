using Godot;
using healerfantasy;
using healerfantasy.LastLight.Hud;

/// <summary>
/// The healer's mana: black glass holding molten light (drawn by <see cref="LightOrbFill"/>),
/// with the current / max value written across it.
/// </summary>
public partial class ManaOrb : Control
{
	Label _valueLabel = null!;
	LightOrbFill _lightFill = null!;
	float _currentMana;
	float _maxMana = 1f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(160f, 160f);

		_lightFill = new LightOrbFill();
		AddChild(_lightFill);

		_valueLabel = new Label();
		_valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_valueLabel.VerticalAlignment = VerticalAlignment.Center;
		_valueLabel.MouseFilter = MouseFilterEnum.Ignore;
		LastLightHud.Style(_valueLabel, 18, LastLightHud.LightPale);
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
				if (characterName == GameConstants.LightkeeperName) OnManaChanged(current, max);
			})
		);
	}

	void OnManaChanged(float current, float max)
	{
		_currentMana = Mathf.Max(current, 0f);
		_maxMana = max;
		_lightFill.SetRatio(max > 0f ? _currentMana / max : 0f);
		UpdateValueLabel();
	}

	void OnResized()
	{
		if (_valueLabel == null)
			return;

		var diameter = Mathf.Min(Size.X, Size.Y) - 18f;
		var labelHeight = Mathf.Clamp(diameter * 0.28f, 24f, 40f);
		var fontSize = Mathf.Clamp((int)(diameter * 0.16f), 14, 22);

		// The shader's glass is 80% of its rect, so overscan to fill the control.
		var overscan = Size * 0.1f;
		_lightFill.Position = -overscan;
		_lightFill.Size = Size + overscan * 2f;

		_valueLabel.Position = new Vector2(0f, Size.Y * 0.5f - labelHeight * 0.5f + diameter * 0.18f);
		_valueLabel.Size = new Vector2(Size.X, labelHeight);
		_valueLabel.AddThemeFontSizeOverride("font_size", fontSize - 2);
	}

	void UpdateValueLabel()
	{
		if (_valueLabel == null)
			return;

		_valueLabel.Text = $"{Mathf.RoundToInt(_currentMana)} / {Mathf.RoundToInt(_maxMana)}";
	}
}
