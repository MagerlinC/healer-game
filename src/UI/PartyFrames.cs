using System.Collections.Generic;
using Godot;
using healerfantasy;

/// <summary>
/// The four party health frames rendered at the bottom of the screen.
///
/// Each slot is a <see cref="PartyFrame"/> — a self-contained
/// <see cref="CharacterFrame"/> that owns its own health bar, shield bar,
/// name label, and effect-indicator row. This class is only responsible for
/// layout, character binding, and hover-target resolution.
///
/// Slot order (must match the order passed to <see cref="BindCharacter"/> from
/// World._Ready):
///   0 = Templar | 1 = Healer (Player) | 2 = Assassin | 3 = Wizard
/// </summary>
public partial class PartyFrames : Control
{
	// ── per-member config ─────────────────────────────────────────────────────
	static readonly (string Name, Color BarColor, float MaxHp)[] MemberDefs =
	{
		(GameConstants.TemplarName, new Color(0.88f, 0.30f, 0.50f), 180f), // rose-red
		(GameConstants.HealerName, new Color(0.35f, 0.78f, 0.22f), 120f), // poison-green
		(GameConstants.AssassinName, new Color(0.85f, 0.78f, 0.15f), 100f), // golden-yellow
		(GameConstants.WizardName, new Color(0.20f, 0.50f, 0.95f), 100f) // sapphire-blue
	};

	// ── node refs ─────────────────────────────────────────────────────────────
	readonly PartyFrame[] _frames = new PartyFrame[4];

	// The party member the player has locked as their default healing target.
	// Null = no lock; spells fall back to the caster when nothing is hovered.
	Character? _defaultTarget;

	// The exact party member currently selected by the boss for melee attacks.
	string? _currentBossMeleeTargetName = GameConstants.TemplarName;

	// ── spell-target tracking ─────────────────────────────────────────────────
	// Names of party members currently highlighted as boss spell targets.
	// When non-empty, these frames are lit instead of the default melee target.
	readonly HashSet<string> _spellTargetNames = new();

	// Minimum time (seconds) a spell-target highlight stays visible after being
	// set. This prevents instant-cast spells (CastTime = 0) from producing a
	// single-frame flicker that the player cannot react to.
	const float SpellHighlightHoldDuration = 1.5f;

	// Counts down from SpellHighlightHoldDuration after spell targets are set.
	// When it reaches zero, the highlight clears automatically.
	float _spellHighlightTimer;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Fill the parent anchor entirely, but never swallow mouse events.
		MouseFilter = MouseFilterEnum.Pass;

		var hbox = new HBoxContainer();
		hbox.MouseFilter = MouseFilterEnum.Pass;
		hbox.AddThemeConstantOverride("separation", 6);
		hbox.SetAnchorsPreset(LayoutPreset.Center);
		hbox.GrowHorizontal = GrowDirection.Both;
		hbox.GrowVertical = GrowDirection.Begin;
		AddChild(hbox);

		for (var i = 0; i < MemberDefs.Length; i++)
		{
			var (name, barColor, maxHp) = MemberDefs[i];
			// Enable the item-effect bar only on the healer frame so that
			// item procs are clearly shown as player-owned effects.
			var showItemEffects = name == GameConstants.HealerName;
			_frames[i] = new PartyFrame(name, barColor, maxHp, showItemEffects);
			// Bottom-align each frame so all health panels stay flush at the same
			// vertical position when effect rows cause individual frames to grow taller.
			_frames[i].SizeFlagsVertical = SizeFlags.ShrinkEnd;
			hbox.AddChild(_frames[i]);
		}

		// ── default-target click wiring ──────────────────────────────────────
		foreach (var frame in _frames)
			frame.OnDefaultTargetClicked = HandleDefaultTargetClicked;

		// ── default targeting state ───────────────────────────────────────────
		// Keep the exact current boss melee target outlined in red between spells.
		UpdateTargetHighlight();

		// ── Templar death tracking ───────────────────────────────────────────
		// Re-evaluate the highlight whenever a party member dies so the UI never
		// keeps a dead frame marked as the boss's target.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.Died),
			Callable.From((Character dead) =>
			{
				if (dead.IsFriendly && dead.CharacterName == _currentBossMeleeTargetName)
				{
					_currentBossMeleeTargetName = null;
					UpdateTargetHighlight();
				}
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.BossMeleeTargetChanged),
			Callable.From((string targetName) =>
			{
				_currentBossMeleeTargetName = string.IsNullOrEmpty(targetName)
					? null
					: targetName;
				UpdateTargetHighlight();
			}));

		// ── boss spell-target highlighting ────────────────────────────────────
		// Bosses emit BossSpellTargetsChanged with the exact character names they
		// are targeting. We highlight only those frames. An empty array clears
		// the override and restores the melee-target outline.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.BossSpellTargetsChanged),
			Callable.From((string[] names) => OnBossSpellTargetsChanged(names)));
	}

	// ── private ───────────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		// Tick down the spell-highlight hold timer for instant-cast spells.
		// When it expires, restore the default melee-target outline.
		if (_spellTargetNames.Count > 0 && _spellHighlightTimer > 0f)
		{
			_spellHighlightTimer -= (float)delta;
			if (_spellHighlightTimer <= 0f)
				ClearSpellTargetHighlight();
		}
	}

	/// <summary>
	/// Called when the boss announces which party members its next spell will
	/// hit. An empty array means the spell has resolved and highlighting should
	/// revert to the default melee-target outline.
	/// </summary>
	void OnBossSpellTargetsChanged(string[] names)
	{
		_spellTargetNames.Clear();

		if (names != null && names.Length > 0)
		{
			foreach (var n in names)
				_spellTargetNames.Add(n);
			// Always reset the hold timer so even a sequence of instant spells
			// never produces a flicker too brief to see.
			_spellHighlightTimer = SpellHighlightHoldDuration;
		}
		else
		{
			// Explicit clear from the boss (channel ended, phase finished, etc.).
			_spellHighlightTimer = 0f;
		}

		UpdateTargetHighlight();
	}

	/// <summary>
	/// Called when the hold timer expires for an instant-cast spell that
	/// doesn't emit an explicit clear signal.
	/// </summary>
	void ClearSpellTargetHighlight()
	{
		_spellTargetNames.Clear();
		_spellHighlightTimer = 0f;
		UpdateTargetHighlight();
	}

	/// <summary>
	/// Recomputes which frames show the boss-targeting outline.
	/// Spell targets take priority: when the boss has announced specific targets
	/// only those frames are lit. Otherwise only the current melee target is lit.
	/// </summary>
	void UpdateTargetHighlight()
	{
		if (_spellTargetNames.Count > 0)
		{
			// Highlight exactly the frames that are spell-targeted.
			for (var i = 0; i < _frames.Length; i++)
			{
				var name = _frames[i].BoundCharacter?.CharacterName;
				_frames[i].SetBossTargeted(name != null && _spellTargetNames.Contains(name));
			}
		}
		else
		{
			// Default: only the current melee target.
			var meleeTargetIndex = GetCurrentMeleeTargetIndex();
			for (var i = 0; i < _frames.Length; i++)
				_frames[i].SetBossTargeted(i == meleeTargetIndex);
		}
	}

	int GetCurrentMeleeTargetIndex()
	{
		for (var i = 0; i < _frames.Length; i++)
			if (_frames[i].BoundCharacter?.IsAlive == true &&
			    _frames[i].BoundCharacter.CharacterName == _currentBossMeleeTargetName)
				return i;

		return -1;
	}

	/// <summary>
	/// Invoked when the player left-clicks a party frame.
	/// Clicking the already-locked frame clears the lock; clicking any other frame
	/// moves the lock to that frame.
	/// </summary>
	void HandleDefaultTargetClicked(Character? clicked)
	{
		// Toggle off when clicking the currently-locked frame.
		var isSameTarget = _defaultTarget != null
		                   && clicked?.CharacterName == _defaultTarget.CharacterName;

		_defaultTarget = isSameTarget ? null : clicked;

		foreach (var frame in _frames)
			frame.SetIsDefaultTarget(
				_defaultTarget != null &&
				frame.BoundCharacter?.CharacterName == _defaultTarget.CharacterName);
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the player's currently locked default healing target, or <c>null</c>
	/// if no frame has been clicked to lock it in.
	/// </summary>
	public Character? GetDefaultTarget()
	{
		return _defaultTarget;
	}

	/// <summary>
	/// Register a <see cref="Character"/> node with a UI slot so that hovering
	/// the slot's panel resolves to the correct game object during targeting.
	/// </summary>
	public void BindCharacter(int slot, Character character)
	{
		if (slot < 0 || slot >= _frames.Length) return;
		_frames[slot].BindCharacter(character);
		UpdateTargetHighlight();
	}

	/// <summary>
	/// Returns the <see cref="Character"/> whose frame the cursor is currently
	/// over, or <c>null</c> if no frame is hovered.
	/// </summary>
	public Character GetHoveredCharacter()
	{
		foreach (var frame in _frames)
			if (frame.IsHovered())
				return frame.BoundCharacter;
		return null;
	}

	/// <summary>
	/// Returns the screen-space rect of the health panel for the party member
	/// named <paramref name="characterName"/>, or <c>Rect2.Zero</c> if not found.
	/// Used by <see cref="healerfantasy.UI.CombatTutorialManager"/> to spotlight
	/// the afflicted frame during the Dispel tutorial.
	/// </summary>
	public Rect2 GetFrameRect(string characterName)
	{
		for (var i = 0; i < MemberDefs.Length; i++)
			if (MemberDefs[i].Name == characterName)
				return _frames[i].GetPanelGlobalRect();
		return new Rect2();
	}
}