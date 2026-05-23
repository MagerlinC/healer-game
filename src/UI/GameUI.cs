#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources.Void;
using healerfantasy.UI;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Root UI layout node.
///
/// Responsibilities
/// ────────────────
/// • Creates and positions every UI component.
/// • Acts as the single entry point that World and Player need: BindCharacter,
///   SetupActionBar, and GetHoveredCharacter are the only public methods.
/// • Everything else — party frame building, signal subscriptions, health/shield
///   updates — lives inside the individual components.
///
/// Pass-through layer
/// ──────────────────
/// The inner anchor Control uses MouseFilter.Pass so that mouse events are never
/// swallowed by blank areas of the UI.  Only interactive leaf nodes (PanelContainers,
/// buttons) use the default Stop filter.  This keeps native tooltips and world-space
/// click events working correctly regardless of the CanvasLayer.
///
/// Mini-enemy health bars
/// ──────────────────────
/// Spawned adds (VinesEnemy, BloodRune, etc.) each get a compact floating health
/// bar that tracks their world-space position.  Optionally a small cast bar can
/// be embedded below the health bar for adds that channel spells (e.g. Blood Rune).
/// Call <see cref="AddMiniEnemyHealthBar"/> for any add that needs a health frame;
/// <see cref="AddVinesHealthBar"/> is a thin wrapper kept for VinesManager
/// compatibility.
/// </summary>
public partial class GameUI : CanvasLayer
{
	PartyFrames _partyFrames;
	BossHealthBar _bossHealthBar;
	BossHealthBar? _secondaryBossHealthBar;
	BossCastBar _bossCastBar = null!;
	ActionBar _actionBar;
	GenericActionBar _genericActionBar;

	/// <summary>
	/// Exposes the generic action bar (Dispel / Deflect slots) so that
	/// <see cref="CombatTutorialManager"/> can read slot screen positions for
	/// tutorial highlight overlays.
	/// </summary>
	public GenericActionBar GenericBar => _genericActionBar;

	/// <summary>
	/// Returns the screen-space rect of the inner health panel for the party
	/// member named <paramref name="characterName"/>, or <c>Rect2.Zero</c> if
	/// the character is not found. Used by <see cref="CombatTutorialManager"/>
	/// to spotlight the afflicted frame in the Dispel tutorial.
	/// </summary>
	public Rect2 GetPartyFrameRect(string characterName)
	{
		return _partyFrames.GetFrameRect(characterName);
	}
	UltimateSlot _ultimateSlot = null!;
	CombatMeter _healingMeter;
	CombatMeter _damageMeter;
	Control _anchor = null!;

	// ── Mini-enemy health bar constants ───────────────────────────────────────
	const float MiniEnemyBarWidth     = 220f;
	const float MiniEnemyBarHeight    = 112f;
	/// <summary>Extra height reserved for the embedded cast bar (health bar + 4px gap + 32px cast bar).</summary>
	const float MiniEnemyBarWithCastHeight = 148f;
	const float MiniEnemyBarTopOffset = 86f;

	/// <summary>Badge shown to the right of the mana orb when Stone of Rebirth is active.</summary>
	ItemEffectIndicator? _stoneOfRebirthBadge;

	/// <summary>
	/// Positioned screen-space wrapper for each active mini-enemy health bar
	/// (VinesEnemy, BloodRune, etc.).
	/// </summary>
	readonly Dictionary<string, Control> _miniEnemyBarAnchors = new();

	/// <summary>
	/// CharacterName → BossHealthBar for each active mini-enemy.
	/// Stored so <see cref="GetHoveredCharacter"/> can check hover state.
	/// </summary>
	readonly Dictionary<string, BossHealthBar> _miniEnemyBars = new();

	/// <summary>Maps CharacterName → Character for each active mini-enemy.</summary>
	readonly Dictionary<string, Character> _miniEnemyCharacters = new();

	/// <summary>
	/// CharacterName → CastBarBase for mini-enemies that have an embedded cast bar.
	/// Only populated when <see cref="AddMiniEnemyHealthBar"/> is called with
	/// <c>hasCastBar = true</c>.
	/// </summary>
	readonly Dictionary<string, CastBarBase> _miniEnemyCastBars = new();

	// Stored so GetHoveredCharacter can return the right Character object and
	// fall back to the alive twin when one is dead.
	Character? _primaryBossCharacter;
	Character? _secondaryBossCharacter;

	public override void _Ready()
	{
		Layer = 10;

		// Full-screen anchor.  Pass filter means blank UI areas never eat mouse
		// events — only explicit interactive children capture input.
		_anchor = new Control();
		_anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_anchor.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(_anchor);
		var anchor = _anchor;

		// ── Cast bar ──────────────────────────────────────────────────────────
		// Anchored at (50%, 87%) — horizontally centred, just below the party
		// frames and above the action bar.  GrowDirection.Both lets the bar
		// expand to its natural content height from that point with no hardcoded
		// pixel offsets for the vertical axis.
		var castBar = new CastBar();
		castBar.CustomMinimumSize = new Vector2(280f, 0f);
		castBar.AnchorLeft = castBar.AnchorRight = 0.5f;
		castBar.AnchorTop = castBar.AnchorBottom = 0.82f;
		castBar.GrowHorizontal = Control.GrowDirection.Both;
		castBar.GrowVertical = Control.GrowDirection.Both;
		castBar.OffsetLeft = -140f;
		castBar.OffsetRight = 140f;
		anchor.AddChild(castBar);

		// ── Mana bar ──────────────────────────────────────────────────────────
		var manaBar = new ManaOrb();
		manaBar.CustomMinimumSize = new Vector2(140f, 140f);
		manaBar.AnchorLeft = 0.2f;
		manaBar.AnchorRight = 0.2f;
		manaBar.AnchorTop = 0.8f;
		manaBar.AnchorBottom = 0.8f;
		manaBar.GrowHorizontal = Control.GrowDirection.Both;
		manaBar.GrowVertical = Control.GrowDirection.Both;
		manaBar.OffsetLeft = -70f;
		manaBar.OffsetRight = 70f;
		manaBar.OffsetTop = -70f;
		manaBar.OffsetBottom = 70f;
		anchor.AddChild(manaBar);

		// ── Stone of Rebirth badge ────────────────────────────────────────────
		// Positioned to the right of the mana orb (orb right-edge = anchor 0.2 + 70px).
		// Subscribes to ItemEffectBus so it appears when the stone is active and
		// disappears when it triggers.  Uses the same 44px ItemEffectIndicator badge
		// style as other item-proc indicators.
		var stoneContainer = new Control();
		stoneContainer.AnchorLeft = stoneContainer.AnchorRight = 0.2f;
		stoneContainer.AnchorTop = stoneContainer.AnchorBottom = 0.8f;
		stoneContainer.GrowHorizontal = Control.GrowDirection.End;
		stoneContainer.GrowVertical = Control.GrowDirection.Both;
		stoneContainer.OffsetLeft = 80f; // 10px gap after orb right edge (70px)
		stoneContainer.OffsetRight = 124f; // 44px wide
		stoneContainer.OffsetTop = -22f; // vertically centred with orb
		stoneContainer.OffsetBottom = 22f;
		stoneContainer.MouseFilter = Control.MouseFilterEnum.Pass;
		anchor.AddChild(stoneContainer);

		ItemEffectBus.ItemEffectActivated += (id, icon, name, desc) =>
		{
			if (id != "stone_of_rebirth") return;
			_stoneOfRebirthBadge?.QueueFree();
			_stoneOfRebirthBadge = new ItemEffectIndicator(id, icon, name, desc, 44);
			_stoneOfRebirthBadge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			stoneContainer.AddChild(_stoneOfRebirthBadge);
		};

		ItemEffectBus.ItemEffectDeactivated += id =>
		{
			if (id != "stone_of_rebirth") return;
			_stoneOfRebirthBadge?.QueueFree();
			_stoneOfRebirthBadge = null;
		};

		// Replay in case stone was purchased before this scene loaded (e.g. returning
		// from camp into a dungeon mid-run).
		ItemEffectBus.ReplayCurrentState((id, icon, name, desc) =>
		{
			if (id != "stone_of_rebirth") return;
			_stoneOfRebirthBadge = new ItemEffectIndicator(id, icon, name, desc, 44);
			_stoneOfRebirthBadge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			stoneContainer.AddChild(_stoneOfRebirthBadge);
		});

		// ── Boss health bar ───────────────────────────────────────────────────
		_bossHealthBar = new BossHealthBar();
		_bossHealthBar.CustomMinimumSize = new Vector2(400f, 0f);
		_bossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_bossHealthBar.OffsetTop = 10f;
		_bossHealthBar.OffsetBottom = 115f; // room for taller bar + effect-badge row below
		anchor.AddChild(_bossHealthBar);

		// ── Boss cast bar (shown during telegraphed wind-ups e.g. Structural Crush)
		_bossCastBar = new BossCastBar();
		var bossCastBar = _bossCastBar;
		bossCastBar.CustomMinimumSize = new Vector2(280f, 0f);
		bossCastBar.AnchorLeft = bossCastBar.AnchorRight = 0.5f;
		bossCastBar.AnchorTop = bossCastBar.AnchorBottom = 0f;
		bossCastBar.GrowHorizontal = Control.GrowDirection.Both;
		bossCastBar.OffsetLeft = -140f;
		bossCastBar.OffsetRight = 140f;
		bossCastBar.OffsetTop = 122f; // just below the boss health bar + effects row
		bossCastBar.OffsetBottom = 162f;
		anchor.AddChild(bossCastBar);

		// ── Party frames ──────────────────────────────────────────────────────
		// Single-point anchor at (50%, 80%) — fully relative so it scales with
		// any resolution.  GrowDirection.Both lets the HBox inside expand from
		// that centre point without needing any hardcoded pixel offsets.
		_partyFrames = new PartyFrames();
		_partyFrames.AnchorLeft = 0.5f;
		_partyFrames.AnchorRight = 0.5f;
		_partyFrames.AnchorTop = 0.75f;
		_partyFrames.AnchorBottom = 0.75f;
		_partyFrames.GrowHorizontal = Control.GrowDirection.Both;
		_partyFrames.GrowVertical = Control.GrowDirection.Both;
		anchor.AddChild(_partyFrames);

		// ── Combat meters ─────────────────────────────────────────────────────
		_healingMeter = new CombatMeter(CombatMeter.MeterType.Healing);
		_healingMeter.AnchorLeft = _healingMeter.AnchorRight = 1f;
		_healingMeter.AnchorTop = _healingMeter.AnchorBottom = 1f;
		_healingMeter.GrowHorizontal = Control.GrowDirection.Begin;
		_healingMeter.GrowVertical = Control.GrowDirection.Begin;
		_healingMeter.OffsetLeft = -510f;
		_healingMeter.OffsetRight = -260f;
		_healingMeter.OffsetTop = -155f;
		_healingMeter.OffsetBottom = -10f;
		anchor.AddChild(_healingMeter);

		_damageMeter = new CombatMeter(CombatMeter.MeterType.Damage);
		_damageMeter.AnchorLeft = _damageMeter.AnchorRight = 1f;
		_damageMeter.AnchorTop = _damageMeter.AnchorBottom = 1f;
		_damageMeter.GrowHorizontal = Control.GrowDirection.Begin;
		_damageMeter.GrowVertical = Control.GrowDirection.Begin;
		_damageMeter.OffsetLeft = -260f;
		_damageMeter.OffsetRight = -10f;
		_damageMeter.OffsetTop = -155f;
		_damageMeter.OffsetBottom = -10f;
		anchor.AddChild(_damageMeter);

		// ── Action bars (regular + generic, side by side) ────────────────────
		// Both bars are children of a shared HBoxContainer so they're naturally
		// laid out next to each other without manual pixel-offset arithmetic.
		var barRow = new HBoxContainer();
		barRow.AddThemeConstantOverride("separation", 14);
		barRow.AnchorLeft = barRow.AnchorRight = 0.5f;
		barRow.AnchorTop = barRow.AnchorBottom = 1f;
		barRow.GrowHorizontal = Control.GrowDirection.Both;
		barRow.GrowVertical = Control.GrowDirection.Begin;
		barRow.OffsetTop = -152f;
		barRow.OffsetBottom = -100f;
		anchor.AddChild(barRow);

		_actionBar = new ActionBar();
		barRow.AddChild(_actionBar);

		// Thin vertical separator between the two bars.
		var sep = new VSeparator();
		sep.AddThemeColorOverride("color", new Color(0.50f, 0.40f, 0.22f, 0.55f));
		barRow.AddChild(sep);

		_genericActionBar = new GenericActionBar();
		barRow.AddChild(_genericActionBar);

		// Thin separator then the ultimate slot to the right of the generic bar.
		var ultimateSep = new VSeparator();
		ultimateSep.AddThemeColorOverride("color", new Color(0.45f, 0.28f, 0.60f, 0.55f));
		barRow.AddChild(ultimateSep);

		_ultimateSlot = new UltimateSlot();
		barRow.AddChild(_ultimateSlot);

		// ── Signal subscriptions owned by GameUI ──────────────────────────────
		// Mana changes shade action-bar icons; all other party signals are
		// handled directly inside PartyFrames and ManaBar.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.ManaChanged),
			Callable.From((string characterName, float current, float max) =>
			{
				if (characterName == GameConstants.HealerName) _actionBar.SetIconShadingBasedOnPlayerMana(current, max);
			}));
	}

	// ── public API (called by World and Player) ───────────────────────────────

	/// <summary>
	/// Associate a Character with a UI slot for hover-targeting and combat meters.
	/// Slot order must match PartyFrames.MemberDefs: 0=Templar, 1=Healer, 2=Assassin, 3=Wizard.
	/// </summary>
	public override void _Process(double delta)
	{
		base._Process(delta);
		UpdateMiniEnemyBarPositions();
	}

	public void BindCharacter(int slot, Character character)
	{
		_partyFrames.BindCharacter(slot, character);
		// Register the runtime name so combat-log source names align with meter rows.
		_healingMeter?.RegisterCharacter(character.CharacterName);
		_damageMeter?.RegisterCharacter(character.CharacterName);
	}

	/// <summary>
	/// Returns the player's currently locked default healing target (set by
	/// left-clicking a party frame), or <c>null</c> if no frame is locked.
	/// </summary>
	public Character? GetDefaultTarget()
	{
		return _partyFrames.GetDefaultTarget();
	}

	/// <summary>Returns the Character whose party frame or boss health bar the cursor is over, or null.</summary>
	public Character? GetHoveredCharacter()
	{
		// World-space Court of Reflections clones take priority over all UI frames.
		// During the mechanic the health bar is hidden, so this is the only targeting path.
		var courtTarget = CourtOfReflectionsRegistry.HoveredTarget;
		if (courtTarget != null && courtTarget.IsAlive) return courtTarget;

		// Secondary bar hover — prefer secondary boss if alive, else fall back to primary.
		if (_secondaryBossHealthBar?.IsHovered() == true)
			return AliveOrFallback(_secondaryBossCharacter, _primaryBossCharacter);

		// Primary bar hover — prefer primary boss if alive, else fall back to secondary.
		if (_bossHealthBar.IsHovered())
			return AliveOrFallback(_primaryBossCharacter, _secondaryBossCharacter);

		// Mini-enemy bars (VinesEnemy, BloodRune, etc.) — return the matching Character.
		foreach (var (charName, bar) in _miniEnemyBars)
		{
			if (bar.IsHovered() && _miniEnemyCharacters.TryGetValue(charName, out var enemy)
			                    && enemy.IsAlive)
				return enemy;
		}

		return _partyFrames.GetHoveredCharacter();
	}

	/// <summary>Returns <paramref name="preferred"/> if alive, otherwise <paramref name="fallback"/> if alive, otherwise null.</summary>
	static Character? AliveOrFallback(Character? preferred, Character? fallback)
	{
		if (preferred?.IsAlive == true) return preferred;
		if (fallback?.IsAlive == true) return fallback;
		return null;
	}


	/// <summary>
	/// Rebuild the action bar from the player's current equipped spells.
	/// Safe to call at startup and whenever the player changes their loadout.
	/// </summary>
	public void RebuildActionBar(SpellResource?[] equipped)
	{
		_actionBar.Rebuild(equipped);
	}

	/// <summary>
	/// Populate the generic action bar with the player's always-available spells.
	/// Must be called once from World after the Player node is resolved.
	/// </summary>
	public void BuildGenericActionBar(Player player)
	{
		_genericActionBar.Build(player);
	}

	/// <summary>
	/// Bind the ultimate slot to <paramref name="player"/> and their equipped ultimate.
	/// Must be called once from World after the Player node is resolved.
	/// The slot handles all further updates autonomously via polling.
	/// </summary>
	public void BindUltimateSlot(Player player)
	{
		_ultimateSlot.Bind(player, player.EquippedUltimate);
	}

	/// <summary>
	/// Register the boss Characters so hover-targeting knows which Character
	/// to return for each health bar.  Call this once from World after the boss
	/// scene is loaded.  For single-boss encounters pass only <paramref name="primary"/>.
	/// </summary>
	public void SetBossCharacters(Character primary, Character? secondary = null)
	{
		_primaryBossCharacter = primary;
		_secondaryBossCharacter = secondary;
	}

	/// <summary>
	/// Add a second boss health bar for <paramref name="secondBoss"/>.
	/// The bar is initialised immediately with the character's current health so
	/// it is visible from the start of the fight rather than waiting for the first
	/// damage event.  Called by World when it detects a multi-boss scene.
	/// </summary>
	public void ShowSecondaryBossBar(Character secondBoss)
	{
		if (_secondaryBossHealthBar != null) return; // already added

		_secondaryBossHealthBar = new BossHealthBar(secondBoss.CharacterName);
		_secondaryBossHealthBar.CustomMinimumSize = new Vector2(400f, 0f);
		_secondaryBossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_secondaryBossHealthBar.OffsetTop = 118f; // below the primary bar + its effects row
		_secondaryBossHealthBar.OffsetBottom = 230f;
		_anchor.AddChild(_secondaryBossHealthBar);

		// Initialise immediately — the character's _Ready() already fired so the
		// initial HealthChanged signal has already been missed.
		_secondaryBossHealthBar.Init(secondBoss.CharacterName, secondBoss.CurrentHealth, secondBoss.MaxHealth);

		// Push the boss cast bar down so it clears both health bars + their effect rows.
		_bossCastBar.OffsetTop = 238f;
		_bossCastBar.OffsetBottom = 278f;
	}

	// ── Mini-enemy health bars (VinesEnemy, BloodRune, etc.) ─────────────────

	/// <summary>
	/// Creates a compact health bar — and optionally an embedded cast bar — for
	/// <paramref name="enemy"/> and adds both to a floating screen-space anchor
	/// that follows the enemy's world position.
	///
	/// Also registers <paramref name="enemy"/> for hover-targeting via
	/// <see cref="GetHoveredCharacter"/> so the player can click/hover the health
	/// frame to target the add with damage spells.
	///
	/// When <paramref name="hasCastBar"/> is <c>true</c> a
	/// <see cref="BloodRuneCastBar"/> is embedded below the health bar and
	/// returned; the caller is responsible for calling
	/// <see cref="CastBarBase.StartCast"/> and <see cref="CastBarBase.StopCast"/>
	/// on it at the appropriate times.
	/// Returns <c>null</c> when <paramref name="hasCastBar"/> is <c>false</c>.
	/// </summary>
	public CastBarBase? AddMiniEnemyHealthBar(Character enemy, string displayName, bool hasCastBar = false)
	{
		var anchorHeight = hasCastBar ? MiniEnemyBarWithCastHeight : MiniEnemyBarHeight;

		// Free-positioned wrapper so the bar can follow the enemy's world position.
		var anchor = new Control();
		anchor.CustomMinimumSize = new Vector2(MiniEnemyBarWidth, anchorHeight);
		anchor.Size              = new Vector2(MiniEnemyBarWidth, anchorHeight);
		anchor.MouseFilter       = Control.MouseFilterEnum.Pass;
		_anchor.AddChild(anchor);

		// Vertical stack: health bar on top, optional cast bar below.
		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.MouseFilter = Control.MouseFilterEnum.Pass;
		anchor.AddChild(vbox);

		// Compact BossHealthBar — expands to fill available height in the VBox.
		var bar = new BossHealthBar(enemy.CharacterName, 6);
		bar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bar.MouseFilter       = Control.MouseFilterEnum.Pass;
		vbox.AddChild(bar);

		// Initialise immediately so the bar is visible from the first frame.
		bar.Init(displayName, enemy.CurrentHealth, enemy.MaxHealth);

		_miniEnemyBarAnchors[enemy.CharacterName] = anchor;
		_miniEnemyBars[enemy.CharacterName]       = bar;
		_miniEnemyCharacters[enemy.CharacterName] = enemy;
		UpdateSingleMiniEnemyBarPosition(enemy.CharacterName, enemy);

		if (!hasCastBar) return null;

		// Embedded cast bar — driven externally by the add enemy.
		var castBar = new BloodRuneCastBar();
		castBar.CustomMinimumSize    = new Vector2(0f, 32f);
		castBar.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
		vbox.AddChild(castBar);
		_miniEnemyCastBars[enemy.CharacterName] = castBar;
		return castBar;
	}

	/// <summary>
	/// Creates a compact health bar for <paramref name="vines"/> and adds it to
	/// a floating screen-space anchor above the attached target.
	/// Wrapper around <see cref="AddMiniEnemyHealthBar"/> kept for
	/// <see cref="VinesManager"/> compatibility.
	/// </summary>
	public void AddVinesHealthBar(VinesEnemy vines)
	{
		AddMiniEnemyHealthBar(vines, vines.DisplayName, hasCastBar: false);
	}

	/// <summary>
	/// Removes the mini-enemy health bar (and optional embedded cast bar) for
	/// the character named <paramref name="characterName"/>.
	/// Called when the add dies or despawns.
	/// </summary>
	public void RemoveMiniEnemyHealthBar(string characterName)
	{
		_miniEnemyCharacters.Remove(characterName);
		_miniEnemyBars.Remove(characterName);
		_miniEnemyCastBars.Remove(characterName);

		if (_miniEnemyBarAnchors.TryGetValue(characterName, out var anchor))
		{
			_miniEnemyBarAnchors.Remove(characterName);
			anchor.QueueFree(); // frees the VBox, BossHealthBar, and cast bar as children
		}
	}

	/// <summary>
	/// Removes the vines health bar for the character named
	/// <paramref name="vinesCharacterName"/>.
	/// Wrapper around <see cref="RemoveMiniEnemyHealthBar"/> kept for
	/// <see cref="VinesManager"/> compatibility.
	/// </summary>
	public void RemoveVinesHealthBar(string vinesCharacterName)
		=> RemoveMiniEnemyHealthBar(vinesCharacterName);

	void UpdateMiniEnemyBarPositions()
	{
		if (_miniEnemyCharacters.Count == 0) return;

		foreach (var (characterName, enemy) in _miniEnemyCharacters)
			UpdateSingleMiniEnemyBarPosition(characterName, enemy);
	}

	void UpdateSingleMiniEnemyBarPosition(string characterName, Character enemyCharacter)
	{
		if (!_miniEnemyBarAnchors.TryGetValue(characterName, out var anchor)
		    || !IsInstanceValid(anchor)
		    || !IsInstanceValid(enemyCharacter))
			return;

		// Convert the enemy's world position into screen-space pixels so the
		// bar follows the same on-screen point as the animated sprite.
		var canvasTransform = enemyCharacter.GetViewport().GetCanvasTransform();
		var screenPos = canvasTransform * enemyCharacter.GlobalPosition;
		anchor.Position = new Vector2(
			screenPos.X - MiniEnemyBarWidth / 2f,
			screenPos.Y - MiniEnemyBarTopOffset);
	}
}
