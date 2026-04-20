#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using SpellResource = healerfantasy.SpellResources.SpellResource;


/// <summary>
/// The player-controlled healer.
/// Inherits health drain from Character and adds WASD movement + spell casting.
/// </summary>
public partial class Player : Character
{
	// ── movement ─────────────────────────────────────────────────────────────
	[Export] public float Speed = 80.0f;
	[Export] public float GlobalCooldown = 0.5f;
	[Export] public SpellResource Spell1 = new TouchOfLightSpell(); // TODO: I cannot seem to create this resource in the UI
	[Export] public SpellResource Spell2 = new WaveOfIncandescenceSpell();
	[Export] public SpellResource Spell3 = new HealOverTimeSpellResource();
	[Export] public SpellResource Spell4 = new ReinvigorateSpell();
	[Export] public SpellResource Spell5 = new BurstOfLightSpell();
	[Export] public SpellResource Spell6 = new DecaySpellResource();

	[Signal]
	public delegate void CastStartedEventHandler(SpellResource spell);

	[Signal]
	public delegate void CastCancelledEventHandler();

	/// <summary>
	/// Set by World after the scene is ready.
	/// Used to resolve which party member the mouse is hovering at cast time.
	/// </summary>
	public GameUI GameUI { get; set; }

	bool _isCasting = false;
	float _castTimer = 0f;
	float _globalCooldownTimer = 0f;
	SpellResource _castSpell;
	Character _castTarget;

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.PlayerName;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastCancelled));
	}

	SpellResource? GetSpellForInput()
	{
		if (Input.IsActionJustPressed("spell_1")) return Spell1;
		if (Input.IsActionJustPressed("spell_2")) return Spell2;
		if (Input.IsActionJustPressed("spell_3")) return Spell3;
		if (Input.IsActionJustPressed("spell_4")) return Spell4;
		if (Input.IsActionJustPressed("spell_5")) return Spell5;
		if (Input.IsActionJustPressed("spell_6")) return Spell6;
		return null;
	}

	void FireSpell(SpellResource spell, Character target)
	{
		SpendMana(spell.ManaCost);
		SpellPipeline.Cast(spell, this, target);
		_isCasting = false;
		_castSpell = null;
		_castTarget = null;
	}
	public override void _Process(double delta)
	{
		base._Process(delta); // runs health drain from Character
		if (_globalCooldownTimer > 0f)
			_globalCooldownTimer = Mathf.Max(_globalCooldownTimer - (float)delta, 0.0f);

		if (!IsAlive)
		{
			CancelCast();
			return;
		}

		// Tick cast timer — any movement input interrupts the cast
		if (_isCasting)
		{
			if (Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Zero)
			{
				CancelCast();
			}
			else
			{
				_castTimer -= (float)delta;
				if (_castTimer <= 0f)
				{
					FireSpell(_castSpell, _castTarget);
				}
			}

			return;
		}


		var canCast = IsAlive && _globalCooldownTimer == 0f;
		if (!canCast) return;

		var spellToCast = GetSpellForInput();

		if (spellToCast is not null)
		{
			var hasMana = CurrentMana >= spellToCast.ManaCost;
			if (hasMana)
			{
				// Lock in the target at cast-start: whichever health frame is under
				// the cursor, or self if the cursor is not over any frame.

				var hoveredCharacter = ResolveTargetWithFallback(GameUI?.GetHoveredCharacter(), spellToCast);
				if (spellToCast.TargetType == TargetType.Enemy && hoveredCharacter.IsFriendly)
				{
					return;
				}

				_castTarget = hoveredCharacter;
				_castSpell = spellToCast;

				var spellIsInstant = spellToCast.CastTime == 0.0f;
				if (spellIsInstant)
				{
					FireSpell(_castSpell, _castTarget);
				}
				else
				{
					EmitSignal(SignalName.CastStarted, spellToCast);
					_isCasting = true;
					_castTimer = spellToCast.CastTime;
				}

				_globalCooldownTimer = GlobalCooldown;
			}
		}
	}

	Character ResolveTargetWithFallback(Character? target, SpellResource spell)
	{
		if (target == null)
		{
			return spell.TargetType == TargetType.Friendly
				? this
				: GetTree().GetFirstNodeInGroup(GameConstants.BossGroupName) as Character;
		}

		return target;

	}

	// ── private helpers ──────────────────────────────────────────────────────
	/// <summary>
	/// Abort the current cast and notify listeners.
	/// No mana is refunded because none was spent yet.
	/// </summary>
	void CancelCast()
	{
		EmitSignalCastCancelled();
		_isCasting = false;
		_castSpell = null;
		_castTarget = null;
		_castTimer = 0f;
	}

	/// <summary>
	/// Returns each spell paired with its input action name, in slot order.
	/// Used by World to initialise the ActionBar.
	/// </summary>
	public (SpellResource Spell, string Action)[] GetSpellBindings()
	{
		return new[]
		{
			(Spell1, "spell_1"),
			(Spell2, "spell_2"),
			(Spell3, "spell_3"),
			(Spell4, "spell_4"),
			(Spell5, "spell_5"),
			(Spell6, "spell_6")
		};
	}
	public override void _PhysicsProcess(double delta)
	{
		if (!IsAlive) return;
		// GetVector normalises diagonal movement automatically
		var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction != Vector2.Zero ? direction * Speed : Vector2.Zero;
		MoveAndSlide();
	}

}