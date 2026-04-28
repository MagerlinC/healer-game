using Godot;
using healerfantasy;

/// <summary>
/// A reflection of The Countess spawned during the Court of Reflections mechanic.
///
/// One instance is the real boss (<see cref="IsRealBoss"/> = true); the remainder
/// are decoys. Neither attacks — their only role is to be distinguished from each other.
///
/// Interaction
/// ───────────
/// The player can interact with a clone/boss in two ways:
///   1. Walk into it  — detected via an <see cref="Area2D"/> body-entered signal.
///   2. Dispel it     — detected via the overridden <see cref="RemoveHarmfulEffects"/>.
///
/// When the real boss is interacted with, the mechanic ends and TheCountess reappears.
/// When a decoy is interacted with, only that decoy is removed.
///
/// Hover glow
/// ──────────
/// Each frame the clone checks whether the mouse is over its world-space sprite rect.
/// When hovered, the sprite modulate is brightened to a golden tint and the clone
/// registers itself with <see cref="CourtOfReflectionsRegistry"/> so that
/// <see cref="UI.GameUI.GetHoveredCharacter"/> can route Dispel at it correctly.
///
/// Immunity
/// ────────
/// All incoming damage is ignored — <see cref="TakeDamage"/> is a no-op.
/// </summary>
public partial class CountessClone : Character
{
	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>True for the one clone that actually ends the mechanic when found.</summary>
	public bool IsRealBoss { get; private set; }

	TheCountess _countess;

	/// <param name="countess">The Countess managing this mechanic phase.</param>
	/// <param name="isRealBoss">
	/// True if interacting with this clone should resolve the mechanic.
	/// False for decoys that just disappear when found.
	/// </param>
	public CountessClone(TheCountess countess, bool isRealBoss)
	{
		_countess = countess;
		IsRealBoss = isRealBoss;
	}

	// ── Visuals ───────────────────────────────────────────────────────────────

	const string AssetBase = "res://assets/enemies/the-countess/";

	/// <summary>Modulate applied when the mouse is over the sprite.</summary>
	static readonly Color HoverModulate = new(2.0f, 1.8f, 0.5f);

	AnimatedSprite2D _sprite;
	bool _isHovered;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();

		// Clones must not appear in the party group or the boss health bar.
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Give a name for debugging; the real boss uses the Countess's actual name
		// so EffectApplied signal routing would still point at her health bar.
		CharacterName = IsRealBoss
			? GameConstants.CastleBoss2Name + "_Clone_Real"
			: "Reflection_" + GetInstanceId();

		// ── Sprite ────────────────────────────────────────────────────────────
		_sprite = new AnimatedSprite2D();
		_sprite.Scale = new Vector2(0.4f, 0.4f);
		AddChild(_sprite);
		SetupAnimations();
		_sprite.Play("idle");

		// ── Body-entered detector (player walking into clone) ─────────────────
		var bodyDetector = new Area2D();
		bodyDetector.CollisionLayer = 0;   // doesn't occupy any layer itself
		bodyDetector.CollisionMask  = 1;   // detects bodies on layer 1 (player/party layer)

		var shape = new CollisionShape2D();
		var circle = new CircleShape2D { Radius = 28f };
		shape.Shape = circle;
		bodyDetector.AddChild(shape);
		AddChild(bodyDetector);

		bodyDetector.BodyEntered += OnBodyEntered;

		// Tag so any future system can enumerate active reflections.
		AddToGroup("court_of_reflections");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		// ── Hover glow ────────────────────────────────────────────────────────
		// Use world-space mouse position so there is no camera-transform mismatch.
		var worldMouse  = GetGlobalMousePosition();
		var worldCenter = GlobalPosition;

		// Approximate world-space hit area for the sprite (tweak if scale differs).
		// At scale 0.4f on a ~64 px-wide texture this is roughly 25 world units half-width.
		const float HalfW = 26f;
		const float HalfH = 34f;
		var rect = new Rect2(worldCenter.X - HalfW, worldCenter.Y - HalfH, HalfW * 2f, HalfH * 2f);

		var nowHovered = rect.HasPoint(worldMouse);
		if (nowHovered != _isHovered)
		{
			_isHovered = nowHovered;
			_sprite.Modulate = _isHovered ? HoverModulate : Colors.White;
		}

		CourtOfReflectionsRegistry.SetHovered(this, _isHovered);
	}

	public override void _ExitTree()
	{
		// Ensure the registry doesn't hold a reference to this freed node.
		CourtOfReflectionsRegistry.SetHovered(this, false);
		base._ExitTree();
	}

	// ── Immunity & interaction ────────────────────────────────────────────────

	/// <summary>Clones cannot take damage — they are purely interactive objects.</summary>
	public override void TakeDamage(float amount) { }

	/// <summary>
	/// Intercepts the Dispel spell (which calls <see cref="Character.RemoveHarmfulEffects"/>
	/// on its target). Instead of cleaning debuffs, this notifies TheCountess that
	/// this reflection was dispelled, resolving or progressing the mechanic.
	/// </summary>
	public override void RemoveHarmfulEffects()
	{
		if (!IsInstanceValid(_countess) || _countess.IsBeingRemoved) return;
		_countess.OnCloneInteracted(this);
	}

	void OnBodyEntered(Node body)
	{
		if (body is Player)
		{
			if (!IsInstanceValid(_countess) || _countess.IsBeingRemoved) return;
			_countess.OnCloneInteracted(this);
		}
	}

	// ── Animation setup ───────────────────────────────────────────────────────

	/// <summary>Mirrors TheCountess's idle animation so clones look identical.</summary>
	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", 3f);
		for (var i = 1; i <= 2; i++)
		{
			var tex = GD.Load<Texture2D>(AssetBase + $"idle{i}.png");
			frames.AddFrame("idle", tex);
		}

		_sprite.SpriteFrames = frames;
	}
}
