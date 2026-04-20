using Godot;
using healerfantasy;

public partial class ManaBar : ProgressBar
{
	public override void _Ready()
	{
		base._Ready();

		// Subscribe only to the player (slot 1) so enemy characters that also
		// emit ManaChanged (e.g. the Crystal Knight) don't pollute this bar.
		GlobalAutoLoad.SubscribeToPartySignal(
			nameof(Character.ManaChanged),
			slot => Callable.From((float current, float max) =>
			{
				if (slot == 1) OnManaChanged(current, max);
			})
		);

		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = Colors.Blue;
		AddThemeColorOverride("fill_color", Colors.Blue);
		AddThemeStyleboxOverride("fill", fillStyle);
	}

	void OnManaChanged(float current, float max)
	{
		// Set MaxValue before Value so Value is never silently clamped.
		MaxValue = max;
		Value    = current;
	}
}
