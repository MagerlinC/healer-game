using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.LastLight.Hud;
using healerfantasy.Runes;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Infinite Abyss — the secret final boss of The Abyss (Rune of the Abyss).
/// Darkness itself: a black mass towering out of sight with two huge pale eyes,
/// attacking with tendrils from the dark above. It never moves.
///
/// Abilities
/// ─────────
///   • Crushing Dark (melee, tank)  — every <see cref="MeleeInterval"/> s; stacks a
///     dispellable damage-taken debuff (<see cref="BossAbyssCrushingDarkSpell"/>).
///   • Falling Tendrils             — every <see cref="TendrilInterval"/> s; circles grow
///     under two allies (three with Starless Night) and under the Lightkeeper's spot,
///     then tendrils spear down (<see cref="FallingTendrilStrike"/>). Allies can't dodge;
///     the Lightkeeper can.
///   • The Abyss Stares             — every <see cref="StareInterval"/> s; a long eye-flare
///     wind-up, then a party-wide hit unless deflected (<see cref="BossAbyssStareSpell"/>).
///   • Eclipse                      — every <see cref="EclipseInterval"/> s; the light shrinks
///     and allies outside it burn (<see cref="EclipsePhase"/>). Flame casts push it back.
///   • The Abyss Opens (50%)        — the Abyss becomes untouchable while three
///     <see cref="AbyssalTendril"/>s rise and lash the party until cut down.
/// </summary>
public partial class TheInfiniteAbyss : EnemyCharacter
{
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(1.8f, Loop: true),
		["attack"] = new(0.9f),
		["cast"] = new(1.2f, Loop: true)
	};

	[Signal] public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);
	[Signal] public delegate void CastWindupEndedEventHandler();

	[Export] public float MeleeInterval = 2.6f;
	[Export] public float TendrilInterval = 13f;
	[Export] public float StareInterval = 34f;
	[Export] public float StareWindup = 4f;
	[Export] public float EclipseInterval = 46f;
	[Export] public float EclipseDuration = 7f;

	const float OpensAtHealth = 0.5f;
	const float TendrilStrikeDamage = 45f;
	const float AddHealth = 420f;
	const float AddLashDamage = 18f;

	/// <summary>Eye positions relative to the boss origin (rig design ±88,-150 at scale 0.5, feet 20 below).</summary>
	static readonly Vector2[] EyePositions = { new(-44f, -55f), new(44f, -55f) };

	/// <summary>World y of the Abyss's base; nobody walks up into the dark past it.</summary>
	const float BaseLineY = -28f;

	/// <summary>Where the Abyssal Tendrils rise, relative to the arena (world space).</summary>
	static readonly Vector2[] AddPositions = { new(-200f, 18f), new(195f, 28f), new(-15f, 58f) };

	readonly BossAbyssCrushingDarkSpell _crush = new();
	readonly BossAbyssStareSpell _stare = new();
	readonly List<AbyssalTendril> _adds = new();
	readonly List<Sprite2D> _eyeGlows = new();

	CharacterVisual _visual = null!;
	AudioStreamPlayer _riser = null!;
	Character _pendingMelee;
	float _meleeTimer, _tendrilTimer, _stareTimer, _eclipseTimer, _stareWindup;
	bool _staring, _eclipseActive, _opened, _phaseActive, _starless;

	/// <summary>Can't be harmed or focused while The Abyss Opens.</summary>
	public override bool IsUntargetable => _phaseActive;

	public TheInfiniteAbyss()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.AbyssTier][0];
	}

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.AbyssBossName;
		// Back-centre of the arena: it rises from here up and out of sight.
		Position = new Vector2(0f, -45f);
		AddBaseWall();
		_starless = RunState.Instance.IsRuneActive(RuneIndex.StarlessNight);

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riser = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath) };
		AddChild(_riser);

		_visual = CharacterVisual.Attach(this, Clips);
		_visual.AnimationFinished += OnAnimationFinished;

		_meleeTimer = MeleeInterval;
		_tendrilTimer = 8f;
		_stareTimer = 20f;
		_eclipseTimer = 30f;
		ApplyRuneModifiers();

		// The last fight has its own track (looping, set in its import settings).
		if (GetParent()?.GetNodeOrNull<AudioStreamPlayer>("AudioStreamPlayer") is { } music)
		{
			music.Stream = GD.Load<AudioStream>(AssetConstants.InfiniteAbyssMusicPath);
			music.Play();
		}
	}

	/// <summary>
	/// An invisible wall along the Abyss's base, so melee members fight at its foot
	/// instead of walking up into the dark while they close in.
	/// </summary>
	void AddBaseWall()
	{
		var wall = new StaticBody2D();
		wall.AddChild(new CollisionShape2D
		{
			Shape = new SegmentShape2D { A = new Vector2(-600f, BaseLineY), B = new Vector2(600f, BaseLineY) }
		});
		Callable.From(() =>
		{
			GetParent()?.AddChild(wall);
			// Anyone who starts up in the dark steps down below the base first.
			foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
				if (node is Node2D member && member.GlobalPosition.Y < BaseLineY + 12f)
					member.GlobalPosition = new Vector2(member.GlobalPosition.X, BaseLineY + 16f);
		}).CallDeferred();
	}

	/// <summary>Dev preview: start an ability now ("stare", "eclipse", "tendrils", "open").</summary>
	public void DevTrigger(string ability)
	{
		switch (ability)
		{
			case "stare": BeginStare(); break;
			case "eclipse": BeginEclipse(); break;
			case "tendrils": CastFallingTendrils(); break;
			case "open":
				_opened = true;
				BeginAbyssOpens();
				break;
		}
	}

	public override void TakeDamage(float amount)
	{
		if (_phaseActive) return;
		base.TakeDamage(amount);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;
		var dt = (float)delta;

		if (!_opened && CurrentHealth <= MaxHealth * OpensAtHealth)
		{
			_opened = true;
			BeginAbyssOpens();
		}

		if (_staring)
		{
			UpdateStare(dt);
			return;
		}

		_meleeTimer -= dt;
		_tendrilTimer -= dt;
		_stareTimer -= dt;
		if (!_eclipseActive) _eclipseTimer -= dt;

		// Never stack the two big mechanics on top of each other, or on the add phase.
		if (_stareTimer <= 0f && !_eclipseActive && !_phaseActive)
		{
			BeginStare();
			return;
		}
		if (_eclipseTimer <= 0f && !_phaseActive && !_eclipseActive)
			BeginEclipse();

		if (_tendrilTimer <= 0f)
		{
			_tendrilTimer = TendrilInterval;
			CastFallingTendrils();
		}

		if (_meleeTimer <= 0f && _pendingMelee == null)
		{
			_meleeTimer = MeleeInterval;
			_pendingMelee = SelectCurrentMeleeTarget();
			if (_pendingMelee != null) _visual.Play("attack");
		}
	}

	void OnAnimationFinished()
	{
		if (_visual.Animation != "attack") return;
		if (_pendingMelee is { IsAlive: true } && IsAlive)
			SpellPipeline.Cast(_crush, this, _pendingMelee);
		_pendingMelee = null;
		if (IsAlive) _visual.Play(_staring || _phaseActive ? "cast" : "idle");
	}

	// ── Falling Tendrils ─────────────────────────────────────────────────────

	void CastFallingTendrils()
	{
		var allies = CollectAlivePartyMembers()
			.Where(c => c.CharacterName != GameConstants.LightkeeperName)
			.Take(_starless ? 3 : 2)
			.ToList();
		foreach (var ally in allies)
			SpawnStrike(ally, follow: true);
		if (FindHealer() is { } healer)
			SpawnStrike(healer, follow: false);

		RaiseBossSpellTargets(allies.Select(a => a.CharacterName).ToArray());
	}

	void SpawnStrike(Character target, bool follow)
	{
		var strike = new FallingTendrilStrike
		{
			Target = target, FollowTarget = follow, Damage = TendrilStrikeDamage, BossName = CharacterName
		};
		GetParent().AddChild(strike);
		strike.GlobalPosition = FallingTendrilStrike.FeetOf(target);
	}

	// ── The Abyss Stares ─────────────────────────────────────────────────────

	void BeginStare()
	{
		_staring = true;
		_stareWindup = StareWindup;
		_stareTimer = StareInterval;
		_pendingMelee = null;

		ParryWindowManager.OpenWindow(_stare.Name, _stare.Icon, StareWindup);
		EmitSignalCastWindupStarted(_stare.Name, _stare.Icon, StareWindup);
		_riser.Play();
		_visual.Play("cast");

		var g = new Gradient();
		g.SetColor(0, new Color(0.95f, 0.92f, 1f, 0.9f));
		g.SetColor(1, new Color(0.6f, 0.45f, 0.85f, 0f));
		var tex = new GradientTexture2D
		{
			Gradient = g, Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
		};
		foreach (var pos in EyePositions)
		{
			var glow = new Sprite2D
			{
				Texture = tex, Position = pos, ZIndex = 4, Modulate = new Color(1f, 1f, 1f, 0f),
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
			};
			AddChild(glow);
			_eyeGlows.Add(glow);
		}
	}

	void UpdateStare(float dt)
	{
		_stareWindup -= dt;
		var p = 1f - Mathf.Clamp(_stareWindup / StareWindup, 0f, 1f);
		foreach (var glow in _eyeGlows)
		{
			glow.Modulate = new Color(1f, 1f, 1f, p);
			glow.Scale = Vector2.One * Mathf.Lerp(0.25f, 0.9f, p * p);
		}
		if (_stareWindup > 0f) return;

		EndStare();
		if (!ParryWindowManager.ConsumeResult())
			SpellPipeline.Cast(_stare, this, FindTank() ?? PickRandomPartyMember());
	}

	void EndStare()
	{
		_staring = false;
		EmitSignalCastWindupEnded();
		foreach (var glow in _eyeGlows) glow.QueueFree();
		_eyeGlows.Clear();
		if (IsAlive) _visual.Play(_phaseActive ? "cast" : "idle");
	}

	// ── Eclipse ──────────────────────────────────────────────────────────────

	void BeginEclipse()
	{
		_eclipseActive = true;
		_eclipseTimer = EclipseInterval;
		var duration = _starless ? EclipseDuration + 2f : EclipseDuration;
		var eclipse = new EclipsePhase { Duration = duration, Boss = this };
		eclipse.Finished += () =>
		{
			_eclipseActive = false;
			if (IsInstanceValid(this)) EmitSignalCastWindupEnded();
		};
		GetParent().AddChild(eclipse);
		EmitSignalCastWindupStarted("Eclipse", LastLightIcons.ForAbyss("eclipse"), duration);
	}

	// ── The Abyss Opens (50%) ────────────────────────────────────────────────

	void BeginAbyssOpens()
	{
		_phaseActive = true;
		if (_staring)
		{
			ParryWindowManager.ConsumeResult(); // cancelled: the stare never lands
			EndStare();
		}
		_visual.Play("cast");

		var ui = GetParent()?.GetNodeOrNull<GameUI>("PartyUI");
		for (var i = 0; i < AddPositions.Length; i++)
		{
			var add = new AbyssalTendril($"Abyssal Tendril {i + 1}", AddHealth, AddLashDamage)
			{
				Position = AddPositions[i]
			};
			GetParent().AddChild(add);
			_adds.Add(add);
			ui?.AddMiniEnemyHealthBar(add, add.CharacterName);
			add.Died += OnTendrilDied;
		}
	}

	void OnTendrilDied(Character tendril)
	{
		var ui = GetParent()?.GetNodeOrNull<GameUI>("PartyUI");
		ui?.RemoveMiniEnemyHealthBar(tendril.CharacterName);
		// Let the death fade play, then clear the corpse away.
		GetTree().CreateTimer(2.5f).Timeout += () =>
		{
			if (IsInstanceValid(tendril)) tendril.QueueFree();
		};

		if (_adds.Any(a => IsInstanceValid(a) && a.IsAlive)) return;
		_phaseActive = false;
		if (IsAlive && !_staring) _visual.Play("idle");
	}

	protected override void OnApplyHasteRune()
	{
		var m = GameConstants.RuneMidnightHasteMultiplier;
		MeleeInterval /= m;
		TendrilInterval /= m;
		StareInterval /= m;
		EclipseInterval /= m;
	}
}
