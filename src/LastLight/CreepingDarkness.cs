using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>A light that holds the darkness back. UV is in screen space; radius in screen heights.</summary>
public sealed class LightPocket
{
	public Vector2 Uv;
	public float Radius;

	/// <summary>0 = a ragged, smouldering pocket; 1 = a faint round glow with no edge.</summary>
	public float Soft;
}

/// <summary>
/// Full-screen overlay: darkness pressing in on every light that is left.
/// By default it follows the healer (<see cref="LastLightStyle.LightFeetUv"/>);
/// <see cref="LastLightStyle.Pockets"/> (e.g. a bonfire) and <see cref="Extra"/>
/// (e.g. lit places on the world map) hold back the dark too. Never blocks the mouse.
/// </summary>
public partial class CreepingDarkness : ColorRect
{
	const int MaxLights = 12;

	/// <summary>Radius of the healer's lit area, in screen heights.</summary>
	public float Radius = 0.32f;

	/// <summary>How opaque the darkness gets; below 1 lets distant shapes show faintly.</summary>
	public float MaxDark = 0.95f;

	/// <summary>How flat each pocket lies on the floor (&gt;1 = wider than tall).</summary>
	public float Squash = 1.5f;

	/// <summary>False for scenes without a healer (the world map).</summary>
	public bool FollowHealer = true;

	/// <summary>Lights owned by this overlay's scene.</summary>
	public readonly List<LightPocket> Extra = new();

	ShaderMaterial _mat = null!;
	readonly Vector2[] _uvs = new Vector2[MaxLights];
	readonly float[] _radii = new float[MaxLights];
	readonly float[] _soft = new float[MaxLights];

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

		var count = 0;
		if (FollowHealer)
		{
			// Centre the pocket on the floor just under the flame; it shrinks when the flame dips.
			_uvs[count] = LastLightStyle.LightFeetUv.Lerp(LastLightStyle.LightUv, 0.35f);
			_radii[count] = Radius * Mathf.Lerp(0.9f, 1f, Mathf.Clamp(LastLightStyle.LightStrength, 0f, 1.2f));
			_soft[count] = 0f;
			count++;
		}
		foreach (var pocket in LastLightStyle.Pockets)
			if (count < MaxLights) Add(pocket, ref count);
		foreach (var pocket in Extra)
			if (count < MaxLights) Add(pocket, ref count);

		var size = Size;
		_mat.SetShaderParameter("light_count", count);
		_mat.SetShaderParameter("light_uvs", _uvs);
		_mat.SetShaderParameter("light_radii", _radii);
		_mat.SetShaderParameter("light_soft", _soft);
		_mat.SetShaderParameter("aspect", size.Y > 0f ? size.X / size.Y : 1.7778f);
		_mat.SetShaderParameter("max_dark", MaxDark);
		_mat.SetShaderParameter("squash", Squash);
	}

	void Add(LightPocket pocket, ref int count)
	{
		_uvs[count] = pocket.Uv;
		_radii[count] = pocket.Radius;
		_soft[count] = pocket.Soft;
		count++;
	}
}
