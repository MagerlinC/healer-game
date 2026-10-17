using Godot;

namespace healerfantasy;

/// <summary>
/// Shared audio setups for boss fights, so every boss's music and sounds sit at
/// the same levels relative to each other. Tune a whole category here rather than
/// per boss.
///
///   • <see cref="Kind.Music"/>      — the fight's track (matches the World scene's player).
///   • <see cref="Kind.AbilityCue"/> — a sound that warns of an ability; must cut through the music.
///   • <see cref="Kind.Voice"/>      — grunts, croaks and other flavour noises; sits under the cues.
/// </summary>
public static class BossAudio
{
	public enum Kind
	{
		Music,
		AbilityCue,
		Voice
	}

	public static float VolumeDbFor(Kind kind) => kind switch
	{
		Kind.Music => -8f,
		Kind.AbilityCue => -4f,
		Kind.Voice => -7f,
		_ => 0f
	};

	/// <summary>Creates a player for <paramref name="path"/> set up as <paramref name="kind"/>, as a child of <paramref name="owner"/>.</summary>
	public static AudioStreamPlayer AddPlayer(Node owner, string path, Kind kind)
	{
		var player = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>(path),
			VolumeDb = VolumeDbFor(kind)
		};
		owner.AddChild(player);
		return player;
	}

	/// <summary>Swaps the fight scene's music player (a sibling named "AudioStreamPlayer") to <paramref name="path"/> and plays it.</summary>
	public static void PlayFightMusic(Node boss, string path)
	{
		if (boss.GetParent()?.GetNodeOrNull<AudioStreamPlayer>("AudioStreamPlayer") is not { } music) return;
		music.Stream = GD.Load<AudioStream>(path);
		music.VolumeDb = VolumeDbFor(Kind.Music);
		music.Play();
	}
}
