using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// Full-screen overlay: darkness pressing in on the healer's light.
/// Follows <see cref="LastLightStyle.LightFeetUv"/>, so it tracks whichever
/// healer rig is active. Never blocks the mouse.
/// </summary>
public partial class CreepingDarkness : ColorRect
{
	/// <summary>Radius of the lit area, in screen heights.</summary>
	public float Radius = 0.32f;

	/// <summary>How opaque the darkness gets; below 1 lets distant shapes show faintly.</summary>
	public float MaxDark = 0.95f;

	ShaderMaterial _mat = null!;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/LastLight/creeping_dark.gdshader") };
		Material = _mat;
	}

	public override void _Process(double delta)
	{
		// Anchors don't resolve under a CanvasLayer parent, so follow the viewport by hand.
		var viewport = GetViewportRect().Size;
		if (GetParent() is not Control && Size != viewport)
		{
			Position = Vector2.Zero;
			Size = viewport;
		}

		var size = Size;
		// Centre the dark on the floor just under the flame.
		var centre = LastLightStyle.LightFeetUv.Lerp(LastLightStyle.LightUv, 0.35f);
		_mat.SetShaderParameter("light_uv", centre);
		_mat.SetShaderParameter("aspect", size.Y > 0f ? size.X / size.Y : 1.7778f);
		_mat.SetShaderParameter("radius", Radius);
		_mat.SetShaderParameter("strength", LastLightStyle.LightStrength);
		_mat.SetShaderParameter("max_dark", MaxDark);
	}
}
