using System;
using Godot;
using healerfantasy;

/// <summary>
/// Self-contained node that orchestrates the Tallow Hulk's dramatic entrance
/// when the Tallow Queen reaches 50% health.
///
/// Sequence
/// ────────
///   1. Camera shakes and a rumble sound plays for <see cref="ShakeDuration"/>
///      seconds while the tree is paused.  (Optionally swaps to phase-2 music.)
///   2. Tree is unpaused.
///   3. <see cref="TheTallowHulk"/> is spawned in the darkness left of the arena,
///      invisible, with <c>SuppressCombat = true</c>.  Its health bar is registered with the GameUI.
///   4. It rises up out of the darkness, arms raised, fading in.
///   5. Its fists come down onto the platform.
///   6. <c>SuppressCombat</c> is cleared — the boss enters normal combat.
///   7. <see cref="OnPhaseComplete"/> is invoked and this node frees itself.
///
/// The Queen's <c>_isMidPhase</c> flag should be set before calling
/// <see cref="Start"/> and cleared inside <see cref="OnPhaseComplete"/>.
/// </summary>
public partial class TallowHulkEmergePhase : Node
{
	// ── constants ─────────────────────────────────────────────────────────────

	const float ShakeDuration = 3.0f;
	const float ShakeStrengthMax = 12f;
	const float ShakeStrengthMin = 4f;

	/// <summary>How far below its place the boss starts rising from.</summary>
	const float RiseDistance = 40f;

	/// <summary>How long it takes to rise out of the wax (seconds). Should feel slow and heavy.</summary>
	const float RiseDuration = 1.6f;

	/// <summary>How long the fists-down slam plays before combat begins.</summary>
	const float LandAnimDuration = 0.5f;

	/// <summary>
	/// Where the Hulk's waist (its rig's origin) is: just off the platform's left edge,
	/// level with it, as fractions of the arena's radii from its centre. Its legs sink
	/// into the darkness below; its torso leans in over the arena, leaving the middle to the Queen.
	/// </summary>
	static readonly Vector2 FeetFraction = new(-1.18f, 0.18f);

	/// <summary>Fallback feet position with no arena boundary.</summary>
	static readonly Vector2 FallbackFeet = new(-270f, 50f);

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Invoked when the full sequence has finished and combat has started.
	/// Set by the Queen before calling <see cref="Start"/>.
	/// </summary>
	public Action OnPhaseComplete { get; set; }

	/// <summary>
	/// When non-null the world music player will be swapped to this stream at
	/// the start of the camera-shake beat (same pattern as TWSTS phase 2).
	/// Set by the Queen if a phase-2 music track is desired.
	/// </summary>
	public AudioStreamOggVorbis PhaseMusic { get; set; }

	// ── runtime state ─────────────────────────────────────────────────────────

	Character _queen;
	float _shakeTimer;
	bool _shakeActive;
	Camera2D _camera;
	Vector2 _cameraBaseOffset;
	AudioStreamPlayer _rumblePlayer;
	AudioStreamPlayer _worldMusicPlayer;
	TheTallowHulk _terror;

	// ── public entry point ────────────────────────────────────────────────────

	/// <summary>
	/// Begin the entrance sequence. <paramref name="queen"/> is used to locate
	/// the scene tree, camera, and music player.  Add this node to the scene
	/// tree before calling Start.
	/// </summary>
	public void Start(Character queen)
	{
		_queen = queen;

		// ── Audio ──────────────────────────────────────────────────────────
		_rumblePlayer = new AudioStreamPlayer();
		var rumble = GD.Load<AudioStream>(AssetConstants.RumbleSoundPath);
		_rumblePlayer.Stream = rumble;
		_rumblePlayer.VolumeDb = 0f;
		AddChild(_rumblePlayer);


		_worldMusicPlayer = queen.GetParent()?.GetNodeOrNull<AudioStreamPlayer>("AudioStreamPlayer");

		// ── Camera grab ────────────────────────────────────────────────────
		_camera = queen.GetViewport().GetCamera2D();
		if (_camera != null)
			_cameraBaseOffset = _camera.Offset;

		// ── Start shaking (runs while tree is paused) ──────────────────────
		ProcessMode = ProcessModeEnum.Always;
		_shakeTimer = ShakeDuration;
		_shakeActive = true;

		// Optional music swap
		if (_worldMusicPlayer != null && PhaseMusic != null)
		{
			_worldMusicPlayer.ProcessMode = ProcessModeEnum.Always;
			_worldMusicPlayer.Stop();
			_worldMusicPlayer.Stream = PhaseMusic;
			_worldMusicPlayer.Play();
		}

		_rumblePlayer.Play();
		queen.GetTree().Paused = true;
	}

	public override void _Process(double delta)
	{
		if (!_shakeActive) return;

		_shakeTimer -= (float)delta;
		UpdateCameraShake();

		if (_shakeTimer > 0f) return;

		// ── Shake done — unpause and begin the jump-in ─────────────────────
		_shakeActive = false;
		RestoreCamera();

		_queen.GetTree().Paused = false;
		ProcessMode = ProcessModeEnum.Inherit;
		if (_worldMusicPlayer != null)
			_worldMusicPlayer.ProcessMode = ProcessModeEnum.Inherit;

		SpawnAndRise();
	}

	// ── Shake helpers ─────────────────────────────────────────────────────────

	void UpdateCameraShake()
	{
		if (_camera == null) return;
		var frac = Mathf.Clamp(_shakeTimer / ShakeDuration, 0f, 1f);
		var strength = Mathf.Lerp(ShakeStrengthMin, ShakeStrengthMax, frac);
		_camera.Offset = _cameraBaseOffset + new Vector2(
			(float)GD.RandRange(-strength, strength),
			(float)GD.RandRange(-strength, strength));
	}

	void RestoreCamera()
	{
		if (_camera != null)
			_camera.Offset = _cameraBaseOffset;
	}

	// ── Rise sequence ─────────────────────────────────────────────────────────

	void SpawnAndRise()
	{
		// Its place: standing beside the platform, on the left.
		var feetPos = PartyMember.ArenaBoundary is { } b
			? b.Center + new Vector2(b.RadiusX * FeetFraction.X, b.RadiusY * FeetFraction.Y)
			: FallbackFeet;

		// ── Create the boss node ───────────────────────────────────────────
		// SuppressCombat is set before _Ready fires so the boss starts dormant.
		_terror = new TheTallowHulk { SuppressCombat = true };

		// Add to the scene first — _Ready fires here and self-creates the visual
		// and collision shape.  Set GlobalPosition afterward (requires being in tree).
		_queen.GetParent().AddChild(_terror);
		_terror.GlobalPosition = feetPos + new Vector2(0f, RiseDistance);
		_terror.Modulate = new Color(1f, 1f, 1f, 0f);

		// ── Register health bar with GameUI ────────────────────────────────
		RegisterWithGameUI(_queen, _terror);

		// ── Rise up out of the wax, arms raised ─────────────────────────────
		_terror.PlayRiseAnim();

		var tween = _terror.CreateTween().SetParallel();
		tween.TweenProperty(_terror, "global_position", feetPos, RiseDuration)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(_terror, "modulate:a", 1f, RiseDuration * 0.6f);
		tween.Chain().TweenCallback(Callable.From(OnBossRisen));
	}

	void OnBossRisen()
	{
		if (_terror == null || !IsInstanceValid(_terror)) return;

		// Fists come down onto the rim.
		_terror.PlayArrivalSlam();
		GetTree().CreateTimer(LandAnimDuration).Timeout += OnLandAnimComplete;
	}

	void OnLandAnimComplete()
	{
		if (_terror == null || !IsInstanceValid(_terror)) return;

		// Unlock combat
		_terror.SuppressCombat = false;

		GD.Print("[TallowHulkEmergePhase] The Tallow Hulk has risen — combat begins!");

		OnPhaseComplete?.Invoke();
		QueueFree();
	}

	// ── GameUI registration ───────────────────────────────────────────────────

	/// <summary>
	/// Finds the GameUI in the scene tree and registers the Tallow Hulk so it
	/// gets a health bar and can be targeted by hover.
	/// </summary>
	static void RegisterWithGameUI(Character queen, TheTallowHulk terror)
	{
		// GameUI lives as "PartyUI" under the World root.
		var ui = terror.GetTree().Root.GetNodeOrNull<GameUI>("World/PartyUI")
		         ?? terror.GetTree().Root.FindChild("PartyUI", true, false) as GameUI;

		if (ui == null)
		{
			GD.PrintErr("[TallowHulkEmergePhase] Could not find GameUI — health bar will not be shown.");
			return;
		}

		// Show a secondary health bar for the Tallow Hulk.
		ui.ShowSecondaryBossBar(terror);

		// Update hover-targeting so the player can target either boss with the mouse.
		// The Queen is still the primary boss; the Terror becomes the secondary.
		ui.SetBossCharacters(queen, terror);
	}
}