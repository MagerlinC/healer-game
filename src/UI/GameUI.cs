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
	ActionBar _actionBar;
	GenericActionBar _genericActionBar;
	CombatMeter _healingMeter;
	CombatMeter _damageMeter;

	public override void _Ready()
	{
		Layer = 10;

		// Full-screen anchor.  Pass filter means blank UI areas never eat mouse
		// events — only explicit interactive children capture input.
		var anchor = new Control();
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		anchor.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(anchor);

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
		var manaBar = new ManaBar();
		manaBar.CustomMinimumSize = new Vector2(280f, 0f);
		manaBar.AnchorLeft = 0.1f;
		manaBar.AnchorRight = 0.1f;
		manaBar.AnchorTop = 0.9f;
		manaBar.AnchorBottom = 0.9f;
		manaBar.GrowHorizontal = Control.GrowDirection.Both;
		manaBar.OffsetLeft = -140f;
		manaBar.OffsetRight = 140f;
		anchor.AddChild(manaBar);

		// ── Boss health bar ───────────────────────────────────────────────────
		_bossHealthBar = new BossHealthBar();
		_bossHealthBar.CustomMinimumSize = new Vector2(400f, 0f);
		_bossHealthBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_bossHealthBar.OffsetTop = 10f;
		_bossHealthBar.OffsetBottom = 90f; // extra room for effect-badge row below the bar
		anchor.AddChild(_bossHealthBar);

		// ── Boss cast bar (shown during telegraphed wind-ups e.g. Structural Crush)
		var bossCastBar = new BossCastBar();
		bossCastBar.CustomMinimumSize = new Vector2(280f, 0f);
		bossCastBar.AnchorLeft  = bossCastBar.AnchorRight = 0.5f;
		bossCastBar.AnchorTop   = bossCastBar.AnchorBottom = 0f;
		bossCastBar.GrowHorizontal = Control.GrowDirection.Both;
		bossCastBar.OffsetLeft  = -140f;
		bossCastBar.OffsetRight =  140f;
		bossCastBar.OffsetTop   = 100f; // just below the boss health bar + effects row
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
		barRow.AnchorLeft  = barRow.AnchorRight  = 0.5f;
		barRow.AnchorTop   = barRow.AnchorBottom = 1f;
		barRow.GrowHorizontal = Control.GrowDirection.Both;
		barRow.GrowVertical   = Control.GrowDirection.Begin;
		barRow.OffsetTop    = -152f;
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
				if (characterName == GameConstants.PlayerName) _actionBar.SetIconShadingBasedOnPlayerMana(current, max);
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

	/// <summary>Returns the Character whose party frame the cursor is over, or null.</summary>
	public Character GetHoveredCharacter()
	{
		if (_bossHealthBar.IsHovered()) return GetTree().GetNodesInGroup(GameConstants.BossGroupName).FirstOrDefault() as Character;
		return _partyFrames.GetHoveredCharacter();
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
}