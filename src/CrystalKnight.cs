using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Crystal Knight boss enemy.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeAttackInterval"/> seconds, performs a melee attack
///   (Crystal Slash) against the tank — the party member named "Templar".
///   Falls back to any alive party member if the Templar is dead.
/// • Every <see cref="SpellCastInterval"/> seconds, fires Crystal Blast at a
///   randomly chosen alive party member.
///
/// Animations are driven by a padded uniform sprite sheet
/// (crystal_knight_sheet.png, 80×80 frames):
///   Row 0 — "idle"   (4 frames, looping)
///   Row 1 — "attack" (5 frames, one-shot → returns to idle)
///   Row 2 — "spell"  (2 frames, one-shot → returns to idle)
/// </summary>
public partial class CrystalKnight : Character
{
    // ── tuneable exports ──────────────────────────────────────────────────────
    [Export] public float MeleeAttackInterval = 2.0f;
    [Export] public float SpellCastInterval   = 7.0f;
    [Export] public float MeleeDamage         = 20f;
    [Export] public float BlastDamage         = 15f;

    // ── internal state ────────────────────────────────────────────────────────
    float _meleeTimer;
    float _spellTimer;

    BossMeleeAttackSpell  _meleeSpell;
    BossCrystalBlastSpell _blastSpell;
    AnimatedSprite2D      _sprite;

    // Target locked in when a timer fires; damage is dealt once the animation ends.
    Character _pendingTarget;
    bool      _pendingIsMelee;

    // ── constants ─────────────────────────────────────────────────────────────
    const int FrameSize = 80; // uniform cell size in crystal_knight_sheet.png

    // ── lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        base._Ready();

        // Character._Ready() adds every character to "party" — undo for enemies.
        RemoveFromGroup("party");
        AddToGroup("enemies");

        // Stagger first attacks so the player has a moment to react.
        _meleeTimer = MeleeAttackInterval;
        _spellTimer = SpellCastInterval;

        _meleeSpell = new BossMeleeAttackSpell  { DamageAmount = MeleeDamage };
        _blastSpell = new BossCrystalBlastSpell { DamageAmount = BlastDamage };

        // ── sprite setup ──────────────────────────────────────────────────────
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        SetupAnimations();
        _sprite.AnimationFinished += OnAnimationFinished;
        _sprite.Play("idle");
    }

    public override void _Process(double delta)
    {
        // Runs mana regen, effect ticking, and the 0-damage life-loss tick.
        base._Process(delta);

        if (!IsAlive) return;

        _meleeTimer -= (float)delta;
        if (_meleeTimer <= 0f)
        {
            _meleeTimer = MeleeAttackInterval;
            PerformMeleeAttack();
        }

        _spellTimer -= (float)delta;
        if (_spellTimer <= 0f)
        {
            _spellTimer = SpellCastInterval;
            CastCrystalBlast();
        }
    }

    // ── combat actions ────────────────────────────────────────────────────────

    void PerformMeleeAttack()
    {
        var target = FindTank() ?? PickRandomPartyMember();
        if (target == null) return;
        _pendingTarget  = target;
        _pendingIsMelee = true;
        _sprite.Play("attack");
    }

    void CastCrystalBlast()
    {
        var target = PickRandomPartyMember();
        if (target == null) return;
        _pendingTarget  = target;
        _pendingIsMelee = false;
        _sprite.Play("spell");
    }

    // Damage lands on the last frame; then we return to idle.
    void OnAnimationFinished()
    {
        if (_pendingTarget != null && _pendingTarget.IsAlive)
        {
            var spell = _pendingIsMelee ? (healerfantasy.SpellResources.SpellResource)_meleeSpell : _blastSpell;
            SpellPipeline.Cast(spell, this, _pendingTarget);
        }
        _pendingTarget = null;
        _sprite.Play("idle");
    }

    // ── targeting helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the alive party member named "Templar" (the tank),
    /// or null if none is found.
    /// </summary>
    Character FindTank()
    {
        foreach (var node in GetTree().GetNodesInGroup("party"))
            if (node is Character c && c.CharacterName == "Templar" && c.IsAlive)
                return c;
        return null;
    }

    /// <summary>
    /// Picks a uniformly random alive member from the "party" group.
    /// Returns null if the whole party has been wiped.
    /// </summary>
    Character PickRandomPartyMember()
    {
        var alive = new List<Character>();
        foreach (var node in GetTree().GetNodesInGroup("party"))
            if (node is Character c && c.IsAlive)
                alive.Add(c);
        if (alive.Count == 0) return null;
        return alive[(int)(GD.Randi() % (uint)alive.Count)];
    }

    // ── animation setup ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds the SpriteFrames resource from crystal_knight_sheet.png at runtime.
    ///
    /// Sheet layout (each cell is 80×80 px):
    ///   Row 0 — idle   (4 frames)
    ///   Row 1 — attack (5 frames)
    ///   Row 2 — spell  (2 frames)
    /// </summary>
    void SetupAnimations()
    {
        var texture = GD.Load<Texture2D>("res://assets/crystal-knight/crystal_knight_sheet.png");
        var frames  = new SpriteFrames();
        frames.RemoveAnimation("default");

        AddAnim(frames, "idle",   texture, row: 0, count: 4, fps: 8f,  loop: true);
        AddAnim(frames, "attack", texture, row: 1, count: 5, fps: 10f, loop: false);
        AddAnim(frames, "spell",  texture, row: 2, count: 2, fps: 4f,  loop: false);

        _sprite.SpriteFrames = frames;
    }

    static void AddAnim(SpriteFrames frames, string name, Texture2D texture,
                        int row, int count, float fps, bool loop)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, loop);
        frames.SetAnimationSpeed(name, fps);
        for (int i = 0; i < count; i++)
        {
            var atlas = new AtlasTexture
            {
                Atlas  = texture,
                Region = new Rect2(i * FrameSize, row * FrameSize, FrameSize, FrameSize),
            };
            frames.AddFrame(name, atlas);
        }
    }
}
