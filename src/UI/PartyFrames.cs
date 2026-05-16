using Godot;
using healerfantasy;
using healerfantasy.SpellResources;

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
		(GameConstants.TemplarName, new Color(0.88f, 0.30f, 0.50f), 150f), // rose-red
		(GameConstants.HealerName, new Color(0.35f, 0.78f, 0.22f), 100f), // poison-green
		(GameConstants.AssassinName, new Color(0.85f, 0.78f, 0.15f), 80f), // golden-yellow
		(GameConstants.WizardName, new Color(0.20f, 0.50f, 0.95f), 80f) // sapphire-blue
	};

	// Slot indices (must match MemberDefs order)
	const int TemplarSlot = 0;

	// ── node refs ─────────────────────────────────────────────────────────────
	readonly PartyFrame[] _frames = new PartyFrame[4];

	// True while a boss cast windup is in progress (suppresses tank-only highlight).
	bool _specialCastActive;

	// The party member the player has locked as their default healing target.
	// Null = no lock; spells fall back to the caster when nothing is hovered.
	Character? _defaultTarget;

	// The exact party member currently selected by the boss for melee attacks.
	string? _currentBossMeleeTargetName = GameConstants.TemplarName;

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
		// Keep the exact current boss melee target outlined in red between
		// special boss casts.
		UpdateMeleeHighlight();

		// ── Templar death tracking ───────────────────────────────────────────
		// Re-evaluate the default melee highlight whenever a party member dies so
		// the UI never keeps a dead frame marked as the boss's target.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.Died),
			Callable.From((Character dead) =>
			{
				if (dead.IsFriendly && dead.CharacterName == _currentBossMeleeTargetName)
				{
					_currentBossMeleeTargetName = null;
					UpdateMeleeHighlight();
				}
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.BossMeleeTargetChanged),
			Callable.From((string targetName) =>
			{
				_currentBossMeleeTargetName = string.IsNullOrEmpty(targetName)
					? null
					: targetName;
				UpdateMeleeHighlight();
			}));

		// ── boss cast-windup targeting ────────────────────────────────────────
		// Re-use the existing CastWindupStarted / CastWindupEnded signal pair that
		// every boss already emits.  When a windup begins we highlight the whole
		// party (the target is often random or AoE); when it ends we restore the
		// tank-only default.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupStarted),
			Callable.From((string _n, Texture2D _t, float _d) => OnCastWindupStarted()));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupEnded),
			Callable.From(OnCastWindupEnded));
	}

	// ── private ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Recomputes which frame should show the default melee-target outline.
	/// Suppressed during special-cast windups because those temporarily light up
	/// every party member instead.
	/// </summary>
	void UpdateMeleeHighlight()
	{
		if (_specialCastActive)
			return;

		var meleeTargetIndex = GetCurrentMeleeTargetIndex();
		for (var i = 0; i < _frames.Length; i++)
			_frames[i].SetBossTargeted(i == meleeTargetIndex);
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

	/// <summary>
	/// Called when any boss begins a telegraphed cast wind-up.
	/// Highlights every party frame to warn the player that a special ability
	/// is incoming and the target is not yet known (or is the whole party).
	/// </summary>
	void OnCastWindupStarted()
	{
		_specialCastActive = true;
		foreach (var frame in _frames)
			frame.SetBossTargeted(true);
	}

	/// <summary>
	/// Called when the boss cast wind-up ends (ability fires or is cancelled).
	/// Restores the default melee-target highlight.
	/// </summary>
	void OnCastWindupEnded()
	{
		_specialCastActive = false;
		// Restore the tank-only default — but only if the Templar is still alive.
		// If the tank has died, the boss auto-attacks random members and there is no
		// reliable single frame to keep highlighted.
		UpdateMeleeHighlight();
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
		UpdateMeleeHighlight();
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