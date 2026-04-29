using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// Queen of the Frozen Wastes — the single boss of The Frozen Peak (Tier 4)
/// and the final encounter of the entire run.
///
/// ════════════════════════════════════════════════════════════════════════════
/// SPELLS
/// ════════════════════════════════════════════════════════════════════════════
///
/// ── Snowstorm ────────────────────────────────────────────────────────────
/// 1-second cast (shown on BossCastBar) followed by an 8-second channel.
/// Every second of the channel every living party member takes
/// <see cref="SnowstormDamagePerTick"/> frost damage.
/// A SnowstormChannelNode drives the channel; the boss cast bar shows a
/// reverse (draining) channel bar for the full 8 seconds.
///
/// ── Volatile Icicle ──────────────────────────────────────────────────────
/// 1-second cast. Spawns a VolatileIcicleProjectile at the Queen's position
/// that floats slowly toward the healer. On contact with any party member it
/// explodes and leaves a permanent IcicleExplosionZone — a blue circular
/// frost hazard dealing 15 damage/sec to anyone inside.
/// Intended design: kite the icicle to the arena edge to detonate it safely.
/// Over time the arena fills with zones, compressing the safe space.
///
/// ── Ice Block ────────────────────────────────────────────────────────────
/// Instant cast. Puts the Queen into her Ice Block state:
///   • Sprite changes to the ice-block.png static frame.
///   • She gains a <see cref="IceBlockShieldAmount"/> absorb shield.
///   • An 8-second Absolute Zero cast begins (shown on BossCastBar).
///   • While encased the Queen cannot use any other abilities.
///   • If the shield is destroyed the Ice Block shatters: Absolute Zero is
///     cancelled and the Queen returns to normal attacks.
///   • If the 8-second cast completes (shield intact), Absolute Zero fires:
///     1 000 damage to every living party member.
///
/// ════════════════════════════════════════════════════════════════════════════
/// ANIMATIONS
/// ════════════════════════════════════════════════════════════════════════════
/// Loaded from res://assets/enemies/queen-of-the-frozen-wastes/.
///
///   "idle"      — idle1–idle3       (looping, 4 fps)
///   "attack"    — attack1–attack3   (one-shot → idle, 10 fps)  [used for cast wind-up]
///   "cast"      — cast1–cast3       (one-shot → idle, 6 fps)   [used for icicle / snowstorm]
///   "ice_block" — ice-block.png     (single frame, loops — static while encased)
/// </summary>
public partial class QueenOfTheFrozenWastes : Character
{
	public QueenOfTheFrozenWastes()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.FrozenPeakTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	/// <summary>Emitted when the Queen begins or ends a pre-channel cast (shared cast bar).</summary>
	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	/// <summary>Emitted when the Snowstorm channel begins — feeds the BossCastBar channel mode.</summary>
	[Signal]
	public delegate void SnowstormChannelStartedEventHandler(BossQueenSnowstormSpell spell);

	/// <summary>Emitted when the Snowstorm channel ends naturally or is interrupted.</summary>
	[Signal]
	public delegate void SnowstormChannelEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float SnowstormInterval    = 20f;
	[Export] public float IcicleInterval       = 12f;
	[Export] public float IceBlockInterval     = 40f;

	[Export] public float SnowstormDamagePerTick = 20f;
	[Export] public float IcicleDamagePerTick    = 15f;
	[Export] public float IceBlockShieldAmount   = 1000f;
	[Export] public float AbsoluteZeroDuration   = 8f;

	// ── internal state ────────────────────────────────────────────────────────

	float _snowstormTimer;
	float _icicleTimer;
	float _iceBlockTimer;

	bool _isSnowstormChanneling;
	bool _isInIceBlock;
	float _absoluteZeroTimer;

	BossQueenSnowstormSpell    _snowstormSpell;
	BossQueenVolatileIcicleSpell _icicleSpell;
	BossQueenIceBlockSpell     _iceBlockSpell;
	BossQueenAbsoluteZeroSpell _absoluteZeroSpell;

	AnimatedSprite2D _sprite;

	enum PendingCast { None, Snowstorm, Icicle }
	PendingCast _pendingCast;

	const string AssetBase = "res://assets/enemies/queen-of-the-frozen-wastes/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.FrozenPeakBossName;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first casts so the fight has a brief breathing window.
		_snowstormTimer = 15f;
		_icicleTimer    = 8f;
		_iceBlockTimer  = 35f;

		_snowstormSpell    = new BossQueenSnowstormSpell    { Boss = this, DamagePerTick = SnowstormDamagePerTick };
		_icicleSpell       = new BossQueenVolatileIcicleSpell { Boss = this, ZoneDamagePerTick = IcicleDamagePerTick };
		_iceBlockSpell     = new BossQueenIceBlockSpell     { Boss = this, IceBlockShield = IceBlockShieldAmount };
		_absoluteZeroSpell = new BossQueenAbsoluteZeroSpell { DamageAmount = 1000f, CastDuration = AbsoluteZeroDuration };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SnowstormChannelStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SnowstormChannelEnded));

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(0.4f, 0.4f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Ice Block — track Absolute Zero countdown ────────────────────────
		if (_isInIceBlock)
		{
			// Shield broken by damage → cancel Ice Block.
			if (CurrentShield <= 0f)
			{
				EndIceBlock(absoluteZeroCompleted: false);
				return;
			}

			_absoluteZeroTimer -= (float)delta;
			if (_absoluteZeroTimer <= 0f)
				EndIceBlock(absoluteZeroCompleted: true);

			return; // no other abilities while encased
		}

		// ── Attack timers ─────────────────────────────────────────────────────
		_snowstormTimer -= (float)delta;
		_icicleTimer    -= (float)delta;
		_iceBlockTimer  -= (float)delta;

		if (_pendingCast != PendingCast.None) return; // wait for animation

		// Priority: Ice Block > Snowstorm > Volatile Icicle.
		if (_iceBlockTimer <= 0f && !_isSnowstormChanneling)
		{
			_iceBlockTimer = IceBlockInterval;
			CastIceBlock();
		}
		else if (_snowstormTimer <= 0f && !_isSnowstormChanneling)
		{
			_snowstormTimer = SnowstormInterval;
			BeginSnowstormCast();
		}
		else if (_icicleTimer <= 0f && !_isSnowstormChanneling)
		{
			_icicleTimer = IcicleInterval;
			BeginIcicleCast();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	/// <summary>
	/// Begin the 1-second Snowstorm cast wind-up (plays cast animation and
	/// shows the cast bar). The channel node is spawned in OnAnimationFinished.
	/// </summary>
	void BeginSnowstormCast()
	{
		_pendingCast = PendingCast.Snowstorm;
		_sprite.Play("cast");
		EmitSignalCastWindupStarted("Snowstorm", _snowstormSpell.Icon, _snowstormSpell.CastTime);
	}

	/// <summary>
	/// Begin the 1-second Volatile Icicle cast wind-up (plays attack animation
	/// and shows the cast bar). The icicle is spawned in OnAnimationFinished.
	/// </summary>
	void BeginIcicleCast()
	{
		_pendingCast = PendingCast.Icicle;
		_sprite.Play("attack");
		EmitSignalCastWindupStarted("Volatile Icicle", _icicleSpell.Icon, _icicleSpell.CastTime);
	}

	/// <summary>
	/// Ice Block is instant — no animation gate. Immediately apply the shield,
	/// switch to the ice-block sprite, and start the Absolute Zero countdown.
	/// </summary>
	void CastIceBlock()
	{
		// Resolve the spell through the pipeline so the SpellContext is wired up
		// correctly, but since BossQueenIceBlockSpell.Apply delegates back to
		// StartIceBlock() the actual work happens there.
		var anyTarget = PickRandomPartyMember();
		if (anyTarget == null) return;
		SpellPipeline.Cast(_iceBlockSpell, this, anyTarget);
	}

	// ── Ice Block state machine ───────────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenIceBlockSpell.Apply"/> to activate the
	/// Ice Block state: apply shield, switch sprite, start Absolute Zero cast.
	/// </summary>
	public void StartIceBlock(float shieldAmount)
	{
		_isInIceBlock      = true;
		_absoluteZeroTimer = AbsoluteZeroDuration;

		AddShield(shieldAmount);
		_sprite.Play("ice_block");

		// Show an Absolute Zero cast bar (8 seconds, fills from 0 → 1).
		EmitSignalCastWindupStarted("Absolute Zero", _absoluteZeroSpell.Icon, AbsoluteZeroDuration);

		GD.Print($"[QueenOfTheFrozenWastes] Ice Block activated — {shieldAmount:F0} shield, " +
		         $"{AbsoluteZeroDuration:F0}s until Absolute Zero.");
	}

	/// <summary>
	/// Ends the Ice Block state.
	/// If <paramref name="absoluteZeroCompleted"/> is true, the 8-second cast
	/// finished without the shield breaking — deal 1 000 damage to the entire party.
	/// Otherwise the shield was destroyed and Absolute Zero is cancelled.
	/// </summary>
	public void EndIceBlock(bool absoluteZeroCompleted)
	{
		if (!_isInIceBlock) return;
		_isInIceBlock = false;

		// Remove any remaining shield.
		if (CurrentShield > 0f)
			RemoveShield(CurrentShield);

		// Clear the cast bar.
		EmitSignalCastWindupEnded();

		if (absoluteZeroCompleted)
		{
			GD.Print("[QueenOfTheFrozenWastes] Absolute Zero completed — wiping party!");
			_absoluteZeroSpell.ResolveNow(this);
		}
		else
		{
			GD.Print("[QueenOfTheFrozenWastes] Ice Block shattered — Absolute Zero cancelled.");
		}

		// Return to idle animation.
		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── Snowstorm channel callbacks ───────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenSnowstormSpell.Apply"/> immediately after
	/// the SnowstormChannelNode is added to the scene.
	/// Clears the cast wind-up bar and emits <see cref="SnowstormChannelStarted"/>
	/// so the BossCastBar switches to channel mode.
	/// </summary>
	public void OnSnowstormChannelStarted(BossQueenSnowstormSpell spell)
	{
		_isSnowstormChanneling = true;
		// NOTE: CastWindupEnded was already emitted in OnAnimationFinished before
		// SpellPipeline.Cast was called — no need to emit it again here.
		EmitSignalSnowstormChannelStarted(spell); // switches BossCastBar to channel mode

		GD.Print($"[QueenOfTheFrozenWastes] Snowstorm channel started ({spell.ChannelDuration:F0}s).");
	}

	/// <summary>
	/// Called by <see cref="SnowstormChannelNode.OnChannelFinished"/> when the
	/// channel ends. Clears the channel bar and allows the next cast.
	/// </summary>
	public void OnSnowstormChannelEnded()
	{
		_isSnowstormChanneling = false;
		EmitSignalSnowstormChannelEnded();

		GD.Print("[QueenOfTheFrozenWastes] Snowstorm channel ended.");
	}

	// ── animation callbacks ───────────────────────────────────────────────────

	void OnAnimationFinished()
	{
		// Resolve whichever spell was queued during the cast animation.
		switch (_pendingCast)
		{
			case PendingCast.Snowstorm:
				EmitSignalCastWindupEnded(); // the spell's Apply will re-emit for the channel
				var snowTarget = PickRandomPartyMember();
				if (snowTarget != null)
					SpellPipeline.Cast(_snowstormSpell, this, snowTarget);
				break;

			case PendingCast.Icicle:
				EmitSignalCastWindupEnded();
				var icicleTarget = PickRandomPartyMember();
				if (icicleTarget != null)
					SpellPipeline.Cast(_icicleSpell, this, icicleTarget);
				break;
		}

		_pendingCast = PendingCast.None;

		// Return to idle unless we just entered Ice Block (which plays its own anim).
		if (IsAlive && !_isInIceBlock)
			_sprite.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	Character PickRandomPartyMember()
	{
		var alive = new System.Collections.Generic.List<Character>();
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

		AddAnimFromFiles(frames, "idle",      "idle",     3, 4f,  looping: true);
		AddAnimFromFiles(frames, "attack",    "attack",   3, 10f, looping: false);
		AddAnimFromFiles(frames, "cast",      "cast",     3, 6f,  looping: false);

		// Ice Block — single static frame; looping so it holds indefinitely.
		frames.AddAnimation("ice_block");
		frames.SetAnimationLoop("ice_block", true);
		frames.SetAnimationSpeed("ice_block", 1f);
		var iceBlockTex = GD.Load<Texture2D>(AssetBase + "ice-block.png");
		if (iceBlockTex != null)
			frames.AddFrame("ice_block", iceBlockTex);

		_sprite.SpriteFrames = frames;
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool looping)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, looping);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var texture = GD.Load<Texture2D>(AssetBase + $"{filePrefix}{i}.png");
			frames.AddFrame(animName, texture);
		}
	}
}
