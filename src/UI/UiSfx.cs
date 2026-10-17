using Godot;

namespace healerfantasy;

/// <summary>
/// Shared UI sound effects. The players live under <see cref="GlobalAutoLoad"/>,
/// so a click still plays out when the button that triggered it changes or
/// reloads the scene (e.g. "Play", "Main Menu", "New Run").
///
/// Opt a button in with <see cref="WireClick"/> — usually from a button factory.
/// </summary>
public static class UiSfx
{
	const float ClickVolumeDb = -6f;

	static AudioStreamPlayer _click;

	/// <summary>Called once by <see cref="GlobalAutoLoad"/> to create the persistent players.</summary>
	public static void Install(Node owner)
	{
		_click = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>(AssetConstants.MenuClickSfxPath),
			VolumeDb = ClickVolumeDb,
			MaxPolyphony = 4,
			// Keep working while the tree is paused (pause menu).
			ProcessMode = Node.ProcessModeEnum.Always
		};
		owner.AddChild(_click);
	}

	public static void PlayClick()
	{
		if (_click != null && GodotObject.IsInstanceValid(_click))
			_click.Play();
	}

	/// <summary>Plays the menu click whenever <paramref name="btn"/> is pressed.</summary>
	public static T WireClick<T>(T btn) where T : BaseButton
	{
		btn.Pressed += PlayClick;
		return btn;
	}
}
