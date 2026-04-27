#nullable enable
using System.Linq;
using Godot;
using healerfantasy;
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
/// </summary>
public partial class GameUI : CanvasLayer
{
	PartyFrames _partyFrames;
	BossHealthBar _bossHealthBar;
	BossHealthBar? _secondaryBossHealthBar;
	BossCastBar _bossCastBar = null!;
	ActionBar _actionBar;
	GenericActionBar _genericActionBar;
	CombatMeter _healingMeter;
	CombatMeter _damageMeter;
	Control _anchor = null!;

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
		var castBar = new CastBar();
		castBar.CustomMinimumSize = new Vector2(280f, 0f);
		castBar.SetAnchorsPreset(Control.LayoutPreset.Center);
		castBar.GrowHorizontal = Control.GrowDirection.Both;
		castBar.OffsetLeft = -140f;
		castBar.OffsetRight = 140f;
		castBar.OffsetTop = 300f;
		castBar.OffsetBottom = 60f;
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

		// ── Boss health bar ───────────────────────────────────────────────────
		_bossHealthBar = new BossHealthBar();
		_bossHealthBar.CustomMinimumSize = new Vector2(400f, 0f);
		_bossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_bossHealthBar.OffsetTop = 10f;
		_bossHealthBar.OffsetBottom = 90f; // extra room for effect-badge row below the bar
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
		bossCastBar.OffsetTop = 100f; // just below the boss health bar + effects row
		bossCastBar.OffsetBottom = 140f;
		anchor.AddChild(bossCastBar);

		// ── Party frames ──────────────────────────────────────────────────────
		_partyFrames = new PartyFrames();
		_partyFrames.AnchorTop = 0.6f;
		_partyFrames.AnchorBottom = 0.4f;
		_partyFrames.AnchorLeft = 0.5f;
		_partyFrames.AnchorRight = 0.5f;
		_partyFrames.GrowHorizontal = Control.GrowDirection.Both;
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
	public void BindCharacter(int slot, Character character)
	{
		_partyFrames.BindCharacter(slot, character);
		// Register the runtime name so combat-log source names align with meter rows.
		_healingMeter?.RegisterCharacter(character.CharacterName);
		_damageMeter?.RegisterCharacter(character.CharacterName);
	}

	/// <summary>Returns the Character whose party frame or boss health bar the cursor is over, or null.</summary>
	public Character? GetHoveredCharacter()
	{
		// Secondary bar hover — prefer secondary boss if alive, else fall back to primary.
		if (_secondaryBossHealthBar?.IsHovered() == true)
			return AliveOrFallback(_secondaryBossCharacter, _primaryBossCharacter);

		// Primary bar hover — prefer primary boss if alive, else fall back to secondary.
		if (_bossHealthBar.IsHovered())
			return AliveOrFallback(_primaryBossCharacter, _secondaryBossCharacter);

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
		_secondaryBossHealthBar.OffsetTop = 80f; // below the primary bar
		_secondaryBossHealthBar.OffsetBottom = 160f;
		_anchor.AddChild(_secondaryBossHealthBar);

		// Initialise immediately — the character's _Ready() already fired so the
		// initial HealthChanged signal has already been missed.
		_secondaryBossHealthBar.Init(secondBoss.CharacterName, secondBoss.CurrentHealth, secondBoss.MaxHealth);

		// Push the boss cast bar down so it clears both health bars.
		_bossCastBar.OffsetTop = 170f;
		_bossCastBar.OffsetBottom = 210f;
	}
}