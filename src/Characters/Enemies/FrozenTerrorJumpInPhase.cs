using System;
using Godot;
using healerfantasy;

/// <summary>
/// Self-contained node that orchestrates the Frozen Terror's dramatic entrance
/// when the Queen of the Frozen Wastes reaches 50% health.
///
/// Sequence
/// ────────
///   1. Camera shakes and a rumble sound plays for <see cref="ShakeDuration"/>
///      seconds while the tree is paused.  (Optionally swaps to phase-2 music.)
///   2. Tree is unpaused.
///   3. <see cref="TheFrozenTerror"/> is spawned off-screen above the arena with
///      <c>SuppressCombat = true</c>.  Its health bar is registered with the GameUI.
///   4. The boss plays its jump animation (jump1–4) while tweening downward into the arena.
///   5. Landing animation (land1–2) plays at the target position.
///   6. <c>SuppressCombat</c> is cleared — the boss enters normal combat.
///   7. <see cref="OnPhaseComplete"/> is invoked and this node frees itself.
///
/// The Queen's <c>_isMidPhase</c> flag should be set before calling
/// <see cref="Start"/> and cleared inside <see cref="OnPhaseComplete"/>.
/// </summary>
public partial class FrozenTerrorJumpInPhase : Node
{
	// ── constants ─────────────────────────────────────────────────────────────

	const float ShakeDuration = 3.0f;
	const float ShakeStrengthMax = 12f;
	const float ShakeStrengthMin = 4f;

	/// <summary>How far above the top of the visible area the boss spawns.</summary>
	const float OffscreenOffsetY = -520f;

	/// <summary>
	/// Duration of the tween from the spawn point to the landing spot (seconds).
	/// Should feel like a weighty fall.
	/// </summary>
	const float JumpTweenDuration = 1.1f;

	/// <summary>How long the landing animation plays before combat begins.</summary>
	const float LandAnimDuration = 0.5f;

	/// <summary>
	/// World-space X offset from arena centre for the landing position.
	/// Positive = right of centre (away from the Queen, who typically sits left).
	/// </summary>
	const float LandingXOffset = 160f;

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
	TheFrozenTerror _terror;

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

		SpawnAndJumpIn();
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

	// ── Jump-in sequence ──────────────────────────────────────────────────────

	void SpawnAndJumpIn()
	{
		// Determine the landing position: slightly right of arena centre.
		Vector2 landingPos;
		if (PartyMember.ArenaBoundary is { } b)
			landingPos = new Vector2(b.Center.X + LandingXOffset, b.Center.Y);
		else
			landingPos = new Vector2(LandingXOffset, 0f);

		var spawnPos = new Vector2(landingPos.X, landingPos.Y + OffscreenOffsetY);

		// ── Create the boss node ───────────────────────────────────────────
		// SuppressCombat is set before _Ready fires so the boss starts dormant.
		_terror = new TheFrozenTerror { SuppressCombat = true };

		// Add to the scene first — _Ready fires here and self-creates the sprite
		// and collision shape.  Set GlobalPosition afterward (requires being in tree).
		_queen.GetParent().AddChild(_terror);
		_terror.GlobalPosition = spawnPos;

		// ── Register health bar with GameUI ────────────────────────────────
		RegisterWithGameUI(_queen, _terror);

		// ── Play jump animation while tweening down ─────────────────────────
		// _Ready fires after AddChild — the sprite and animations are now set up.
		_terror.PlayJumpAnim();

		var tween = _terror.CreateTween();
		tween.TweenProperty(_terror, "global_position", landingPos, JumpTweenDuration)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(OnBossLanded));
	}

	void OnBossLanded()
	{
		if (_terror == null || !IsInstanceValid(_terror)) return;

		// Land animation
		_terror.PlayLandAnim();
		GetTree().CreateTimer(LandAnimDuration).Timeout += OnLandAnimComplete;
	}

	void OnLandAnimComplete()
	{
		if (_terror == null || !IsInstanceValid(_terror)) return;

		// Unlock combat
		_terror.SuppressCombat = false;

		GD.Print("[FrozenTerrorJumpInPhase] The Frozen Terror has landed — combat begins!");

		OnPhaseComplete?.Invoke();
		QueueFree();
	}

	// ── GameUI registration ───────────────────────────────────────────────────

	/// <summary>
	/// Finds the GameUI in the scene tree and registers the Frozen Terror so it
	/// gets a health bar and can be targeted by hover.
	/// </summary>
	static void RegisterWithGameUI(Character queen, TheFrozenTerror terror)
	{
		// GameUI lives as "PartyUI" under the World root.
		var ui = terror.GetTree().Root.GetNodeOrNull<GameUI>("World/PartyUI")
		         ?? terror.GetTree().Root.FindChild("PartyUI", true, false) as GameUI;

		if (ui == null)
		{
			GD.PrintErr("[FrozenTerrorJumpInPhase] Could not find GameUI — health bar will not be shown.");
			return;
		}

		// Show a secondary health bar for the Frozen Terror.
		ui.ShowSecondaryBossBar(terror);

		// Update hover-targeting so the player can target either boss with the mouse.
		// The Queen is still the primary boss; the Terror becomes the secondary.
		ui.SetBossCharacters(queen, terror);
	}
}