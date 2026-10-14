using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// A character's animation clock: named clips with a duration, and the state the
/// fight code drives (<see cref="Play"/>, <see cref="Stop"/>, <see cref="AnimationFinished"/>,
/// facing, offset). It draws nothing — the character's <see cref="SilhouetteRig"/>
/// reads it every frame and poses itself to match.
///
/// Clip durations are gameplay: bosses cast their spell when an attack clip finishes,
/// so a clip's length is that attack's wind-up. Durations are written as
/// <c>frames / fps</c> from the original sprite animations to keep every rhythm.
///
/// Mirrors the parts of <see cref="AnimatedSprite2D"/> the game relied on, including
/// its <c>Play</c> rule: playing the clip that is already running does not restart it.
/// </summary>
public partial class CharacterVisual : Node2D
{
	public const string NodeName = "Visual";

	/// <summary>A named animation: how long it lasts, and whether it loops.</summary>
	public readonly record struct Clip(float Duration, bool Loop = false);

	readonly Dictionary<string, Clip> _clips;
	float _elapsed;
	bool _playing;

	public CharacterVisual(Dictionary<string, Clip> clips)
	{
		_clips = clips;
		Name = NodeName;
	}

	/// <summary>Fired when a non-looping clip reaches its end.</summary>
	public event Action AnimationFinished;

	/// <summary>The current clip's name.</summary>
	public string Animation { get; private set; } = "idle";

	/// <summary>Playback speed multiplier (the healer stretches "cast" to the cast time).</summary>
	public float SpeedScale = 1f;

	/// <summary>Facing flag, as the old sprites used it (the healer: true = facing right).</summary>
	public bool FlipH;

	/// <summary>Extra visual offset on top of <see cref="Node2D.Position"/> (e.g. a hover bob).</summary>
	public Vector2 Offset;

	/// <summary>
	/// How far through the current clip, 0–1. Does not wrap for looping clips, so a long
	/// channel reads as "fully into the pose" rather than restarting every loop.
	/// </summary>
	public float Progress => Duration(Animation) > 0f ? Mathf.Clamp(_elapsed / Duration(Animation), 0f, 1f) : 1f;

	public bool IsPlaying() => _playing;

	/// <summary>Creates the visual as a child of <paramref name="owner"/> and starts "idle".</summary>
	public static CharacterVisual Attach(Node2D owner, Dictionary<string, Clip> clips)
	{
		var visual = new CharacterVisual(clips);
		owner.AddChild(visual);
		visual.Play("idle");
		return visual;
	}

	public void Play(string name)
	{
		// Same rule as AnimatedSprite2D: re-playing the running clip is a no-op.
		if (name == Animation && _playing) return;
		if (!_clips.ContainsKey(name))
			GD.PushWarning($"[CharacterVisual] {GetParent()?.Name} has no clip '{name}'.");
		Animation = name;
		_elapsed = 0f;
		_playing = true;
	}

	public void Stop()
	{
		_playing = false;
		_elapsed = 0f;
	}

	public override void _Process(double delta)
	{
		if (!_playing) return;
		_elapsed += (float)delta * SpeedScale;

		var clip = _clips.TryGetValue(Animation, out var c) ? c : new Clip(0.6f);
		if (clip.Loop || _elapsed < clip.Duration) return;

		_elapsed = clip.Duration;
		_playing = false;
		AnimationFinished?.Invoke();
	}

	float Duration(string name) => _clips.TryGetValue(name, out var c) ? c.Duration : 0.6f;
}
