using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Flying Skull — third and final boss of The Forsaken Citadel.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Death Chomp: the skull
///   snaps at the tank for heavy void damage; uses the "attack" animation.
/// • Every <see cref="ScreechInterval"/> seconds — Void Screech: fires a
///   bolt of void energy at a random party member; uses the "cast" animation.
/// • Every <see cref="PoolInterval"/> seconds — Necrotic Pool: plants a
///   lingering area-of-effect void pool on the healer's position that pulses
///   20 damage per second for 4 seconds; uses the "cast" animation.
/// • Every <see cref="WailInterval"/> seconds — Banshee Wail: a 3.5-second
///   telegraphed AoE that unleashes a devastating void wail across all party
///   members for 65 damage unless the player deflects in time;
///   uses the "cast" animation.
/// • Every <see cref="WavesInterval"/> seconds — Necrotic Waves: after a
///   1-second cast wind-up, sends <see cref="WaveCount"/> sweeping void-energy
///   waves across the screen one at a time. Each wave has a small gap the party
///   must stand in to avoid <see cref="WavesDamage"/> void damage per wave;
///   uses the "cast" animation.
///
/// Animations use individual PNG frames from:
///   res://assets/enemies/flying-skull/{anim}{n}.png
///   idle   (3 frames, looping)
///   attack (5 frames, one-shot → idle)
///   cast   (4 frames, one-shot → idle)
/// </summary>
public partial class FlyingSkull : Character
{
	public FlyingSkull()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.ForsakenCitadelTier][2];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeInterval = 2.5f;
	[Export] public float ScreechInterval = 5.0f;
	[Export] public float PoolInterval = 11.0f;
	[Export] public float WailInterval = 16.0f;
	[Export] public float WailWindup = 3.5f;
	[Export] public float WavesInterval = 22.0f;

	[Export] public float MeleeDamage = 40f;
	[Export] public float ScreechDamage = 38f;
	[Export] public float PoolPulse = 20f;
	[Export] public float WailDamage = 65f;

	// ── Necrotic Waves tunables ────────────────────────────────────────────────

	/// <summary>Number of waves fired per cast.</summary>
	[Export] public int WaveCount = 4;

	/// <summary>Delay in seconds between each successive wave.</summary>
	[Export] public float WaveDelay = 1.4f;

	/// <summary>Void damage dealt to any party member caught outside the gap.</summary>
	[Export] public float WavesDamage = 32f;

	/// <summary>Wind-up cast time before the first wave fires.</summary>
	[Export] public float WavesCastTime = 1.0f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _screechTimer;
	float _poolTimer;
	float _wailTimer;
	float _wailWindupTimer;
	float _wavesTimer;

	BossDeathChompSpell _deathChompSpell;
	BossVoidScreechSpell _voidScreechSpell;
	BossNecroticPoolSpell _necroticPoolSpell;
	BossBansheeWailSpell _bansheeWailSpell;
	BossNecroticWavesSpell _necroticWavesSpell;

	// ── Necrotic Waves sequence state ──────────────────────────────────────────

	/// <summary>True while the Necrotic Waves cast or wave sequence is active.</summary>
	bool _wavesActive;

	/// <summary>Counts down the initial cast wind-up before the first wave fires.</summary>
	float _wavesCastTimer;

	/// <summary>Counts down the delay between successive waves.</summary>
	float _wavesFireTimer;

	/// <summary>How many waves have been spawned so far in the current cast.</summary>
	int _wavesFired;

	/// <summary>Alternates wave direction each cast (horizontal / vertical).</summary>
	bool _nextWaveIsHorizontal = true;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		VoidScreech,
		NecroticPool,
		NecroticWaves
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string BasePath = "res://assets/enemies/flying-skull/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.ForsakenBoss3Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		_meleeTimer = MeleeInterval;
		_screechTimer = ScreechInterval;
		_poolTimer = PoolInterval;
		_wailTimer = WailInterval;
		_wavesTimer = WavesInterval;

		_deathChompSpell = new BossDeathChompSpell { DamageAmount = MeleeDamage };
		_voidScreechSpell = new BossVoidScreechSpell { DamageAmount = ScreechDamage };
		_necroticPoolSpell = new BossNecroticPoolSpell { DamagePerPulse = PoolPulse };
		_bansheeWailSpell = new BossBansheeWailSpell { DamageAmount = WailDamage };
		_necroticWavesSpell = new BossNecroticWavesSpell();

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Banshee Wail wind-up countdown ────────────────────────────────────
		if (_wailWindupTimer > 0f)
		{
			_wailWindupTimer -= (float)delta;
			if (_wailWindupTimer <= 0f)
				ExecuteWail();
			return;
		}

		// ── Necrotic Waves sequence (runs concurrently with normal attacks) ──────
		if (_wavesActive)
			UpdateWavesSequence((float)delta);

		_meleeTimer -= (float)delta;
		_screechTimer -= (float)delta;
		_poolTimer -= (float)delta;
		_wailTimer -= (float)delta;
		_wavesTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformDeathChomp();
		}
		else if (_screechTimer <= 0f)
		{
			_screechTimer = ScreechInterval;
			CastVoidScreech();
		}
		else if (_poolTimer <= 0f)
		{
			_poolTimer = PoolInterval;
			CastNecroticPool();
		}
		else if (_wailTimer <= 0f)
		{
			_wailTimer = WailInterval;
			BeginWail();
		}
		else if (_wavesTimer <= 0f && !_wavesActive)
		{
			_wavesTimer = WavesInterval;
			BeginNecroticWaves();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformDeathChomp()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("attack");
	}

	void CastVoidScreech()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.VoidScreech;
		_sprite.Play("cast");
	}

	void CastNecroticPool()
	{
		// Necrotic Pool always targets the healer (handled in ResolveTargets),
		// but we pass any alive member as the nominal target to satisfy the pipeline.
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.NecroticPool;
		_sprite.Play("cast");
	}

	void BeginWail()
	{
		_wailWindupTimer = WailWindup;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_bansheeWailSpell.Name, _bansheeWailSpell.Icon, WailWindup);
		EmitSignalCastWindupStarted(_bansheeWailSpell.Name, _bansheeWailSpell.Icon, WailWindup);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("cast");
	}

	void ExecuteWail()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[FlyingSkull] Banshee Wail was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_bansheeWailSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _deathChompSpell,
				PendingAttack.VoidScreech => _voidScreechSpell,
				PendingAttack.NecroticPool => _necroticPoolSpell,
				_ => null
			};

			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;
		_sprite.Play("idle");
	}

	// ── Necrotic Waves ────────────────────────────────────────────────────────

	void BeginNecroticWaves()
	{
		_wavesActive = true;
		_wavesCastTimer = WavesCastTime;
		_wavesFireTimer = 0f;
		_wavesFired = 0;

		// Show a cast-bar for the wind-up phase.
		EmitSignalCastWindupStarted(_necroticWavesSpell.Name, _necroticWavesSpell.Icon, WavesCastTime);

		_sprite.Play("cast");
	}

	/// <summary>
	/// Drives the Necrotic Waves sequence each frame while <see cref="_wavesActive"/>.
	/// Phase 1: wait for the cast wind-up to expire.
	/// Phase 2: fire waves at <see cref="WaveDelay"/> intervals until all <see cref="WaveCount"/> are out.
	/// </summary>
	void UpdateWavesSequence(float delta)
	{
		if (_wavesCastTimer > 0f)
		{
			_wavesCastTimer -= delta;
			if (_wavesCastTimer <= 0f)
			{
				// Cast time finished — signal the UI and fire the first wave immediately.
				EmitSignalCastWindupEnded();
				_wavesFireTimer = 0f;
			}

			return;
		}

		_wavesFireTimer -= delta;
		if (_wavesFireTimer > 0f) return;

		if (_wavesFired < WaveCount)
		{
			SpawnNextWave();
			_wavesFired++;
			_wavesFireTimer = WaveDelay;
		}

		if (_wavesFired >= WaveCount)
		{
			// All waves fired — end the sequence.
			_wavesActive = false;
			_wavesFired = 0;
			if (_sprite.Animation == "cast")
				_sprite.Play("idle");
		}
	}

	/// <summary>
	/// Spawns a single <see cref="NecroticWave"/> node as a sibling of this boss,
	/// alternating between horizontal (left↔right) and vertical (top↔bottom) sweeps,
	/// and randomising which edge the wave enters from each time.
	/// </summary>
	void SpawnNextWave()
	{
		// Convert the screen rect to world-space so gap positions are placed
		// correctly within the visible arena (mirrors the fix in NecroticWave._Ready).
		var screenRect    = GetViewportRect();
		var toWorld       = GetCanvasTransform().AffineInverse();
		var wTL           = toWorld * screenRect.Position;
		var wBR           = toWorld * screenRect.End;
		float worldLeft   = Mathf.Min(wTL.X, wBR.X);
		float worldRight  = Mathf.Max(wTL.X, wBR.X);
		float worldTop    = Mathf.Min(wTL.Y, wBR.Y);
		float worldBottom = Mathf.Max(wTL.Y, wBR.Y);
		float worldWidth  = worldRight  - worldLeft;
		float worldHeight = worldBottom - worldTop;

		var wave = new NecroticWave { DamageAmount = WavesDamage };

		if (_nextWaveIsHorizontal)
		{
			// Horizontal wave: sweeps left→right or right→left; gap is a Y position.
			var   ltr    = GD.Randi() % 2 == 0;
			float margin = NecroticWave.WaveThickness * 2f;
			float gapY   = worldTop + margin + (float)(GD.Randf() * (worldHeight - margin * 2f));

			wave.Direction = ltr ? NecroticWave.WaveDirection.LeftToRight : NecroticWave.WaveDirection.RightToLeft;
			wave.GapCenter = gapY;
		}
		else
		{
			// Vertical wave: sweeps top→bottom or bottom→top; gap is an X position.
			var   ttb    = GD.Randi() % 2 == 0;
			float margin = NecroticWave.WaveThickness * 2f;
			float gapX   = worldLeft + margin + (float)(GD.Randf() * (worldWidth - margin * 2f));

			wave.Direction = ttb ? NecroticWave.WaveDirection.TopToBottom : NecroticWave.WaveDirection.BottomToTop;
			wave.GapCenter = gapX;
		}

		_nextWaveIsHorizontal = !_nextWaveIsHorizontal;

		// Render above characters and boss sprites so the wave is never hidden.
		wave.ZIndex = 1;

		// Position at world origin — _Draw uses world-space coordinates derived
		// from the canvas transform, so local and world space must match (no offset).
		wave.GlobalPosition = Vector2.Zero;
		GetParent().AddChild(wave);
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.TemplarName && c.IsAlive)
				return c;
		return null;
	}

	Character PickRandomPartyMember()
	{
		var alive = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				alive.Add(c);
		if (alive.Count == 0) return null;
		return alive[(int)(GD.Randi() % (uint)alive.Count)];
	}

	// ── animation setup ───────────────────────────────────────────────────────

	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", 3, 6f, true);
		AddAnimFromFiles(frames, "attack", 5, 10f, false);
		AddAnimFromFiles(frames, "cast", 4, 8f, false);

		_sprite.SpriteFrames = frames;
	}

	void AddAnimFromFiles(SpriteFrames frames, string animName, int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var path = $"{BasePath}{animName}{i}.png";
			var texture = GD.Load<Texture2D>(path);
			frames.AddFrame(animName, texture);
		}
	}
}