using Godot;
using healerfantasy;
using healerfantasy.LastLight;

namespace healerfantasy.Effects;

/// <summary>
/// A ghostly visual duplicate of the player, spawned while Twin Flame is active.
/// Floats beside the caster, mirrors their idle animation, and plays a one-shot
/// cast animation each time the mirror echo fires.
///
/// Added as a child of the player Character node so it inherits world position.
/// Freed automatically when Twin Flame expires via <see cref="TwinFlameEffect.OnExpired"/>.
/// </summary>
public partial class TwinFlameClone : Node2D
{
	/// <summary>Horizontal offset from the parent character, in local space.</summary>
	const float XOffset = -30f;

	/// <summary>Ghostly wax-blue tint (the Wick school) and a gentle shimmer.</summary>
	static readonly Color Tint = new(0.62f, 0.8f, 1f, 0.6f);
	const float PulseSpeed = 1.8f;
	const float PulseStrength = 0.12f;

	CharacterVisual _visual = null!;
	float _t;

	public override void _Ready()
	{
		// The same flame-headed figure as the healer, facing the other way. It glows,
		// but is not the light source — the real healer keeps the scene's light.
		_visual = CharacterVisual.Attach(this, new()
		{
			["idle"] = new(3 / 8f, Loop: true),
			["cast"] = new(4 / 4f)
		});
		_visual.FlipH = true;
		_visual.AnimationFinished += OnAnimationFinished;
		SilhouetteRig.Attach(this, RigLibrary.Healer(), _visual, isLightSource: false);

		Modulate = Tint;

		// Offset so the clone hovers to the side of the caster, not on top of them.
		Position = new Vector2(XOffset, 0f);

		ZIndex = -1; // render behind the player
	}

	public override void _Process(double delta)
	{
		_t += (float)delta;
		Modulate = new Color(Tint, Mathf.Clamp(Tint.A + Mathf.Sin(_t * PulseSpeed) * PulseStrength, 0f, 1f));
	}

	/// <summary>
	/// Play a one-shot cast animation. When the animation finishes, the clone
	/// automatically returns to its idle loop (see <see cref="OnAnimationFinished"/>).
	/// </summary>
	public void PlayCast()
	{
		_visual?.Play("cast");
	}

	void OnAnimationFinished()
	{
		// Cast is a one-shot; return to the idle loop when it completes.
		if (_visual?.Animation == "cast")
			_visual.Play("idle");
	}
}
