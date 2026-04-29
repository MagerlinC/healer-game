using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Wizard — a ranged spellcaster dealing damage from the back line.
/// Casts Arcane Blast every <see cref="CastInterval"/> seconds.
/// Slowest cadence of the three but highest damage per hit.
/// </summary>
public partial class Wizard : PartyMember
{
    /// <summary>Seconds between each Arcane Blast.</summary>
    [Export] public float CastInterval = 3.0f;

    float _castTimer;
    WizardArcaneBlastSpell _arcaneBlast;
    AnimatedSprite2D _sprite = null!;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite.Play("idle");
        _sprite.AnimationFinished += OnAnimationFinished;
        // Stagger first cast so it doesn't overlap with the melee members.
        _castTimer = CastInterval * 0.6f;
        _arcaneBlast = new WizardArcaneBlastSpell();
    }

    void OnAnimationFinished()
    {
        if (_sprite.Animation == "attack")
            _sprite.Play("idle");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!IsAlive) return;

        _castTimer -= (float)delta;
        if (_castTimer <= 0f)
        {
            _castTimer = GetHasteAdjustedAttackInterval(CastInterval);
            CastArcaneBlast();
        }
    }

    void CastArcaneBlast()
    {
        var boss = FindPreferredBoss();
        if (boss == null) return;
        _sprite.Play("attack");
        SpellPipeline.Cast(_arcaneBlast, this, boss);
    }

    protected override void ApplyDeathVisuals()
    {
        _sprite.Stop();
        _sprite.Rotation = Mathf.Pi / 2f;

        var shader = new Shader();
        shader.Code = """
            shader_type canvas_item;
            void fragment() {
                vec4 col = texture(TEXTURE, UV);
                float grey = dot(col.rgb, vec3(0.299, 0.587, 0.114));
                COLOR = vec4(grey, grey, grey, col.a);
            }
            """;
        var mat = new ShaderMaterial();
        mat.Shader = shader;
        _sprite.Material = mat;
    }
}
