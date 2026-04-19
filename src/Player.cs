#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// The player-controlled healer.
/// Inherits health drain from Character and adds WASD movement + spell casting.
/// </summary>
public partial class Player : Character
{
	// ── movement ─────────────────────────────────────────────────────────────
	[Export] public float Speed = 80.0f;
	[Export] public SpellResource Spell1 = new HealSpellResource(); // TODO: I cannot seem to create this resource in the UI
	[Export] public SpellResource Spell2 = new GroupHealSpellResource();
	[Export] public SpellResource Spell3 = new HealOverTimeSpellResource();

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
	SpellResource _castSpell;
	Character _castTarget;

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastCancelled));
	}

	SpellResource? GetSpellForInput()
	{
		// TODO: generalise to an array for more spells
		if (Input.IsActionJustPressed("spell_1")) return Spell1;
		if (Input.IsActionJustPressed("spell_2")) return Spell2;
		if (Input.IsActionJustPressed("spell_3")) return Spell3;
		return null;
	}

	public override void _Process(double delta)
	{
		base._Process(delta); // runs health drain from Character

		if (!IsAlive) return;

		var spellToCast = GetSpellForInput();

		if (spellToCast is not null && !_isCasting)
		{
			var hasMana = CurrentMana >= spellToCast.ManaCost;
			if (hasMana)
			{
				// Lock in the target at cast-start: whichever health frame is under
				// the cursor, or self if the cursor is not over any frame.
				_castTarget = GameUI?.GetHoveredCharacter() ?? this;
				_castSpell = spellToCast;

				EmitSignal(SignalName.CastStarted, spellToCast);
				_isCasting = true;
				_castTimer = spellToCast.CastTime;
			}
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
					_isCasting = false;
					_castSpell = null;
					_castTarget = null;
				}
			}
		}
	}

	// ── private helpers ──────────────────────────────────────────────────────
	/// <summary>
	/// Abort the current cast and notify listeners.
	/// No mana is refunded because none was spent yet.
	/// </summary>
	void CancelCast()
	{
		EmitSignal(SignalName.CastCancelled);
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
			(Spell3, "spell_3")
		};
	}
	public override void _PhysicsProcess(double delta)
	{
		// GetVector normalises diagonal movement automatically
		var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction != Vector2.Zero ? direction * Speed : Vector2.Zero;
		MoveAndSlide();
	}

}