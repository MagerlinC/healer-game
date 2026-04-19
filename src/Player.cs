using System.Collections.Generic;
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
	
	[Signal] public delegate void CastStartedEventHandler(SpellResource spell);
	
	bool  _isCasting  = false;
    float _castTimer  = 0f;

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastStarted));
	}
	public override void _Process(double delta)
	{
		base._Process(delta); // runs health drain from Character

		if (!IsAlive) return;
		
		var spellToCast = Input.IsActionJustPressed("spell_1") ? Spell1 : null; // TODO: support multiple spells and switching between them

		if (spellToCast is not null && !_isCasting)
		{
			var hasMana = CurrentMana >= spellToCast.ManaCost;
			if (hasMana)
			{
				SpendMana(spellToCast.ManaCost);
				EmitSignal(SignalName.CastStarted, spellToCast);
				_isCasting = true;
				_castTimer = spellToCast.CastTime;
			}
		}
		// Tick cast timer
		if (_isCasting)
		{
			_castTimer -= (float)delta;
			if (_castTimer <= 0f)
			{
				FireSpell(Spell1);
				_isCasting = false;
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// GetVector normalises diagonal movement automatically
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction != Vector2.Zero ? direction * Speed : Vector2.Zero;
		MoveAndSlide();
	}

}
