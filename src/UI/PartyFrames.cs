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
		("Templar", new Color(0.88f, 0.30f, 0.50f), 150f), // rose-red
		(GameConstants.HealerName, new Color(0.35f, 0.78f, 0.22f), 80f), // poison-green
		("Assassin", new Color(0.85f, 0.78f, 0.15f), 100f), // golden-yellow
		("Wizard", new Color(0.20f, 0.50f, 0.95f), 70f) // sapphire-blue
	};

	// ── node refs ─────────────────────────────────────────────────────────────
	readonly PartyFrame[] _frames = new PartyFrame[4];

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Fill the parent anchor entirely, but never swallow mouse events.
		MouseFilter = MouseFilterEnum.Pass;

		var hbox = new HBoxContainer();
		hbox.MouseFilter = MouseFilterEnum.Pass;
		hbox.AddThemeConstantOverride("separation", 6);
		hbox.SetAnchorsPreset(LayoutPreset.Center);
		hbox.Position += new Vector2(0, 250); // push down from centre
		hbox.GrowHorizontal = GrowDirection.Both;
		hbox.GrowVertical = GrowDirection.Both;
		AddChild(hbox);

		for (var i = 0; i < MemberDefs.Length; i++)
		{
			var (name, barColor, maxHp) = MemberDefs[i];
			_frames[i] = new PartyFrame(name, barColor, maxHp);
			hbox.AddChild(_frames[i]);
		}
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Register a <see cref="Character"/> node with a UI slot so that hovering
	/// the slot's panel resolves to the correct game object during targeting.
	/// </summary>
	public void BindCharacter(int slot, Character character)
	{
		if (slot < 0 || slot >= _frames.Length) return;
		_frames[slot].BindCharacter(character);
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
}