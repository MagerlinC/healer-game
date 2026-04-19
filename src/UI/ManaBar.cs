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
		MaxValue = _max;
		Value = _current;
		Size = new Vector2(200, 10);
		Visible = true;
	}

	public void OnManaChanged(float current, float max)
	{
		Value = current;
		MaxValue = max;
	}
}