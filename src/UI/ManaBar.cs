using Godot;
using healerfantasy;

public partial class ManaBar : ProgressBar
{
	float _max;
	float _current;

	public override void _Ready()
	{
		base._Ready();
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.ManaChanged),
			Callable.From((float current, float max) => OnManaChanged(current, max))
		);

		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = Colors.Blue;
		AddThemeColorOverride("fill_color", Colors.Blue);
		AddThemeStyleboxOverride("fill", fillStyle);
		MaxValue = _max;
		Value = _current;
		Size = new Vector2(250, 8);
		Visible = true;
	}

	public void OnManaChanged(float current, float max)
	{
		Value = current;
		MaxValue = max;
	}
}