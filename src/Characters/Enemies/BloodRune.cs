using Godot;
using healerfantasy;
using healerfantasy.SpellResources;

/// <summary>
/// Blood Rune — a short-lived add that spawns during the <see cref="BloodKnight"/>
/// fight every <see cref="BloodKnight.BloodRuneSpawnInterval"/> seconds.
///
/// On spawn the rune immediately begins an 8-second channel of Blood Burst.
/// If the party kills it before the cast completes the burst is cancelled.
/// If the cast finishes, all alive party members take 60 damage and the rune
/// despawns shortly after.
///
/// The rune is NOT a boss — it is removed from the boss group in <see cref="_Ready"/>
/// so victory / defeat conditions and VinesManager's alive-boss check are
/// unaffected by it.
///
/// Cast bar integration
/// ────────────────────
/// <see cref="CastBar"/> is injected by <see cref="BloodKnight.SpawnBloodRune"/>
/// after <see cref="healerfantasy.UI.GameUI.AddMiniEnemyHealthBar"/> returns the
/// embedded <see cref="BloodRuneCastBar"/> widget.  The rune calls
/// <see cref="CastBarBase.StartCast"/> in <see cref="_Ready"/> and
/// <see cref="CastBarBase.StopCast"/> on death or cast completion.
///
/// Cleanup
/// ───────
/// <see cref="OnDespawn"/> is also injected by <see cref="BloodKnight"/> and is
/// invoked just before <c>QueueFree</c> in both the fire-and-despawn path and the
/// death path.  This lets BloodKnight remove the health-bar frame and its own
/// rune-tracking list without needing a separate signal or group query.
/// </summary>
public partial class BloodRune : EnemyCharacter
{
	// ── public state ──────────────────────────────────────────────────────────

	/// <summary>Display name shown on the compact health bar.</summary>
	public string DisplayName { get; } = "Blood Rune";

	/// <summary>
	/// Duration (in seconds) of the Blood Burst cast.
	/// Set by <see cref="BloodKnight"/> before adding this node to the scene tree.
	/// </summary>
	public float CastDuration { get; set; } = 8f;

	/// <summary>
	/// Embedded cast bar widget living inside this rune's health-bar frame.
	/// Injected by <see cref="BloodKnight.SpawnBloodRune"/> after
	/// <see cref="healerfantasy.UI.GameUI.AddMiniEnemyHealthBar"/> returns it.
	/// May be null if no cast bar was created (e.g. during testing).
	///
	/// The setter starts the cast bar immediately when assigned after <c>_Ready</c>
	/// has run, passing the remaining cast time so the bar's countdown is accurate
	/// even though it was assigned one frame after the timer started.
	/// </summary>
	public CastBarBase? CastBar
	{
		get => _castBar;
		set
		{
			_castBar = value;
			// If _Ready has already fired, start the bar now with the real remaining time.
			if (_isReady && _castBar != null && !_hasFired && IsAlive)
				_castBar.StartCast(_burstSpell.Name, _burstSpell.Icon, _castTimer);
		}
	}
	CastBarBase? _castBar;

	/// <summary>
	/// Called immediately before the rune removes itself from the scene (whether
	/// from a completed cast or from death).  Injected by BloodKnight so it can
	/// clean up the health-bar frame and tracking list without a separate signal.
	/// </summary>
	public System.Action? OnDespawn { get; set; }

	// ── internal ──────────────────────────────────────────────────────────────

	float _castTimer;
	bool _hasFired;
	bool _isReady;

	readonly BossBloodBurstSpell _burstSpell = new();

	AnimatedSprite2D _sprite = null!;

	const string BloodRuneGroupName = "blood_rune";
	const string BloodRuneSpritePath = "res://assets/enemies/blood-knight/blood-rune.png";

	// ── ctor ──────────────────────────────────────────────────────────────────

	public BloodRune(string instanceName)
	{
		MaxHealth      = 100f;
		IsFriendly     = false;
		CharacterName  = instanceName;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready(); // registers signals, adds to boss group

		// Remove from the boss group so VinesManager's alive-boss check,
		// victory conditions, and BossHealthBar routing are unaffected.
		RemoveFromGroup(GameConstants.BossGroupName);
		AddToGroup(BloodRuneGroupName);

		// ── sprite ───────────────────────────────────────────────────────────
		_sprite = new AnimatedSprite2D();
		var frames = new SpriteFrames();
		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", 1f);
		frames.AddFrame("idle", GD.Load<Texture2D>(BloodRuneSpritePath));
		_sprite.SpriteFrames = frames;
		_sprite.Scale        = new Vector2(0.25f, 0.25f);
		AddChild(_sprite);
		_sprite.Play("idle");

		// ── begin casting Blood Burst immediately ─────────────────────────────
		// Start the countdown timer now. The cast bar is started via the CastBar
		// property setter once BloodKnight injects it (which happens after AddChild,
		// i.e. after this _Ready call). The setter passes _castTimer so the bar
		// shows the correct remaining time from the moment it appears.
		_castTimer = CastDuration;
		_isReady   = true;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || _hasFired) return;

		_castTimer -= (float)delta;
		if (_castTimer <= 0f)
		{
			FireBurst();
			return;
		}

		UpdateVisualEffects();
	}

	// ── visual effects ────────────────────────────────────────────────────────

	/// <summary>
	/// Gentle hover (vertical sine wave) and a slow size pulse that grows more
	/// urgent as the Blood Burst cast nears completion.
	/// </summary>
	void UpdateVisualEffects()
	{
		if (_sprite == null) return;

		var t = Time.GetTicksMsec() / 1000f;

		// Hover: slow sine wave offset on the Y axis.
		const float HoverAmplitude = 5f;
		const float HoverFrequency = 0.9f;
		_sprite.Offset = new Vector2(0f, Mathf.Sin(t * HoverFrequency * Mathf.Tau) * HoverAmplitude);

		// Pulse: scale breathes between base and a larger value; frequency and
		// amplitude ramp up as the cast progresses to signal mounting threat.
		var castProgress   = Mathf.Clamp(1f - _castTimer / CastDuration, 0f, 1f);
		var pulseFrequency = Mathf.Lerp(0.6f, 2.0f, castProgress);
		var pulseAmplitude = Mathf.Lerp(0.02f, 0.08f, castProgress);
		var pulse          = Mathf.Sin(t * pulseFrequency * Mathf.Tau) * 0.5f + 0.5f; // 0 → 1
		var scale          = 0.25f + pulse * pulseAmplitude;
		_sprite.Scale = new Vector2(scale, scale);
	}

	// ── casting ───────────────────────────────────────────────────────────────

	void FireBurst()
	{
		_hasFired = true;
		_castBar?.StopCast();

		// Deal damage to every alive party member.
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
			if (node is Character c && c.IsAlive)
				c.TakeDamage(_burstSpell.DamageAmount);

		// Despawn after a brief pause so damage numbers can appear above targets.
		var timer = GetTree().CreateTimer(0.5);
		timer.Timeout += Despawn;
	}

	// ── death visuals ─────────────────────────────────────────────────────────

	protected override void ApplyDeathVisuals()
	{
		// Cancel any active cast when the rune is killed.
		_castBar?.StopCast();

		if (_sprite != null)
			_sprite.Modulate = new Color(0.4f, 0.1f, 0.1f, 0.5f);

		var timer = GetTree().CreateTimer(0.6);
		timer.Timeout += Despawn;
	}

	// ── shared despawn ────────────────────────────────────────────────────────

	void Despawn()
	{
		OnDespawn?.Invoke();
		QueueFree();
	}
}
