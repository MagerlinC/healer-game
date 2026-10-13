using Godot;

namespace healerfantasy.LastLight.Hud;

/// <summary>
/// Party-frame health fill: light vs. darkness with a jagged edge, plus an
/// ember trail that shows recent damage before catching up.
/// </summary>
public partial class LightHealthFill : ColorRect
{
	static Shader _shader;

	ShaderMaterial _mat = null!;
	float _target = 1f, _shown = 1f, _ghost = 1f, _ghostHold;
	bool _dead;

	public override void _Ready()
	{
		_shader ??= GD.Load<Shader>("res://src/LastLight/Hud/hud_health.gdshader");
		_mat = new ShaderMaterial { Shader = _shader };
		_mat.SetShaderParameter("seed", GD.Randf() * 50f);
		Material = _mat;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetRatio(float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		if (ratio < _target)
		{
			_ghost = Mathf.Max(_ghost, _shown);
			_ghostHold = 0.45f;
		}
		_target = ratio;
		_dead = ratio <= 0f;
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_shown = Mathf.MoveToward(_shown, _target, dt * 2.5f);

		if (_ghostHold > 0f) _ghostHold -= dt;
		else _ghost = Mathf.MoveToward(_ghost, _shown, dt * 0.55f);
		if (_ghost < _shown) _ghost = _shown;

		_mat.SetShaderParameter("ratio", _shown);
		_mat.SetShaderParameter("ghost", _ghost);
		_mat.SetShaderParameter("dead", _dead ? 1f : 0f);
		_mat.SetShaderParameter("size", Size);
	}
}

/// <summary>Mana orb fill: black glass holding molten light.</summary>
public partial class LightOrbFill : ColorRect
{
	static Shader _shader;

	ShaderMaterial _mat = null!;
	float _target = 1f, _shown = 1f;

	public override void _Ready()
	{
		_shader ??= GD.Load<Shader>("res://src/LastLight/Hud/hud_orb.gdshader");
		_mat = new ShaderMaterial { Shader = _shader };
		Material = _mat;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetRatio(float ratio) => _target = Mathf.Clamp(ratio, 0f, 1f);

	public override void _Process(double delta)
	{
		_shown = Mathf.MoveToward(_shown, _target, (float)delta * 1.5f);
		_mat.SetShaderParameter("ratio", _shown);
	}
}
