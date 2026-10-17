using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.Runes;
using healerfantasy.SpellResources;

public partial class ThatWhichSwallowedTheStarsMemoryGame : Node2D
{
	[Signal]
	public delegate void CompletedEventHandler();

	public float DamageAmount { get; set; } = 95f;
	public string BossName { get; set; } = GameConstants.SanctumBoss3Name;

	// Tiles are drawn by a shader in the boss's look (void, stolen stars); the sprite's
	// texture only gives it a size.
	static readonly Shader TileShader =
		GD.Load<Shader>("res://src/Characters/Enemies/EnemyMechanics/stolen_memory.gdshader");

	enum TileLook
	{
		Safe = 0,
		DangerPreview = 1,
		DangerReplay = 2
	}

	readonly List<int> _safeQuadrants = new();
	readonly Sprite2D[] _tiles = new Sprite2D[4];
	readonly ShaderMaterial[] _tileMaterials = new ShaderMaterial[4];
	readonly float[] _tileAge = new float[4];
	readonly bool[] _hitThisReplayStep = new bool[4];
	readonly RandomNumberGenerator _rng = new();

	Rect2 _arenaRect;
	State _state;
	float _stateTimer;
	int _stepIndex;

	const float PreviewDuration = 1.0f;
	const float PreviewGapDuration = 1.00f;
	const float RecallDelayDuration = 2.0f;
	const float ReplayDuration = 1.0f;
	const float ReplayGapDuration = 1.00f;

	enum State
	{
		Preview,
		PreviewGap,
		RecallDelay,
		Replay,
		ReplayGap,
		Finished
	}

	bool _isPureRuneActive = false;
	public override void _Ready()
	{
		_isPureRuneActive = RunState.Instance.IsRuneActive(RuneIndex.StarlessNight);
		_rng.Randomize();
		_arenaRect = BuildArenaRect();
		BuildTiles();

		var roundCount = _isPureRuneActive ? 5 : 3;
		_safeQuadrants.Add(_rng.RandiRange(0, 3));
		for (var i = 1; i < roundCount; i++)
		{
			var previousQuadrant = _safeQuadrants[i - 1];
			var adjacentQuadrants = GetAdjacentQuadrants(previousQuadrant);
			_safeQuadrants.Add(adjacentQuadrants[_rng.RandiRange(0, adjacentQuadrants.Count - 1)]);
		}

		StartPreviewStep(0);
	}

	public override void _Process(double delta)
	{
		if (_state == State.Finished)
			return;

		_stateTimer -= (float)delta;

		for (var i = 0; i < _tiles.Length; i++)
		{
			_tileAge[i] += (float)delta;
			_tileMaterials[i].SetShaderParameter("age", _tileAge[i]);
		}

		if (_state == State.Replay)
			CheckReplayDamage();

		if (_stateTimer > 0f)
			return;

		switch (_state)
		{
			case State.Preview:
				HideAllTiles();
				if (_stepIndex >= _safeQuadrants.Count - 1)
				{
					_state = State.RecallDelay;
					_stateTimer = RecallDelayDuration;
				}
				else
				{
					_state = State.PreviewGap;
					_stateTimer = PreviewGapDuration;
				}

				break;

			case State.PreviewGap:
				StartPreviewStep(_stepIndex + 1);
				break;

			case State.RecallDelay:
				StartReplayStep(0);
				break;

			case State.Replay:
				HideAllTiles();
				if (_stepIndex >= _safeQuadrants.Count - 1)
				{
					Finish();
				}
				else
				{
					_state = State.ReplayGap;
					_stateTimer = ReplayGapDuration;
				}

				break;

			case State.ReplayGap:
				StartReplayStep(_stepIndex + 1);
				break;
		}
	}

	Rect2 BuildArenaRect()
	{
		var camera = GetViewport().GetCamera2D();
		if (camera != null)
		{
			var screenCenter = camera.GetScreenCenterPosition();
			var viewportSize = GetViewportRect().Size;
			var worldSize = viewportSize / camera.Zoom;
			return new Rect2(screenCenter - worldSize / 2f, worldSize);
		}

		// Fallback: derive from canvas transform
		var transform = GetCanvasTransform().AffineInverse();
		var vpRect = GetViewportRect();
		var topLeft = transform * vpRect.Position;
		var bottomRight = transform * vpRect.End;
		return new Rect2(topLeft, bottomRight - topLeft);
	}

	void BuildTiles()
	{
		var quadrantSize = _arenaRect.Size / 2f;
		var pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		pixel.Fill(Colors.White);
		var texture = ImageTexture.CreateFromImage(pixel);

		for (var i = 0; i < _tiles.Length; i++)
		{
			var material = new ShaderMaterial { Shader = TileShader };
			material.SetShaderParameter("size", quadrantSize);
			material.SetShaderParameter("seed", _rng.Randf() * 10f);
			_tileMaterials[i] = material;

			var tile = new Sprite2D
			{
				Texture = texture,
				Material = material,
				Centered = true,
				Visible = false,
				ZIndex = 50
			};

			var quadrantRect = GetQuadrantRect(i);
			tile.Position = quadrantRect.GetCenter() - GlobalPosition;
			tile.Scale = quadrantSize;
			AddChild(tile);
			_tiles[i] = tile;
		}
	}

	void StartPreviewStep(int stepIndex)
	{
		_stepIndex = stepIndex;
		_state = State.Preview;
		_stateTimer = PreviewDuration;
		ShowPreviewPattern(_safeQuadrants[stepIndex]);
	}

	void StartReplayStep(int stepIndex)
	{
		_stepIndex = stepIndex;
		_state = State.Replay;
		_stateTimer = ReplayDuration;
		Array.Fill(_hitThisReplayStep, false);
		ShowReplayPattern(_safeQuadrants[stepIndex]);
		CheckReplayDamage();
	}

	void ShowPreviewPattern(int safeQuadrant)
	{
		for (var i = 0; i < _tiles.Length; i++)
			ShowTile(i, i == safeQuadrant ? TileLook.Safe : TileLook.DangerPreview, Vector4.One);
	}

	void ShowReplayPattern(int safeQuadrant)
	{
		for (var i = 0; i < _tiles.Length; i++)
		{
			if (i == safeQuadrant)
				_tiles[i].Visible = false;
			else
				ShowTile(i, TileLook.DangerReplay, RimFacing(i, safeQuadrant));
		}
	}

	void ShowTile(int index, TileLook look, Vector4 rim)
	{
		var material = _tileMaterials[index];
		material.SetShaderParameter("mode", (int)look);
		material.SetShaderParameter("rim", rim);
		material.SetShaderParameter("age", 0f);
		_tileAge[index] = 0f;
		_tiles[index].Visible = true;
	}

	/// <summary>
	/// The edges (left, top, right, bottom) of a danger quadrant that border the safe one,
	/// so the void gets its starlit rim only where it meets safety, not between two danger tiles.
	/// </summary>
	static Vector4 RimFacing(int quadrant, int safeQuadrant)
	{
		int col = quadrant % 2, row = quadrant / 2;
		int safeCol = safeQuadrant % 2, safeRow = safeQuadrant / 2;
		var rim = Vector4.Zero;
		if (row == safeRow)
		{
			if (safeCol < col) rim.X = 1f;
			else rim.Z = 1f;
		}

		if (col == safeCol)
		{
			if (safeRow < row) rim.Y = 1f;
			else rim.W = 1f;
		}

		return rim;
	}

	void HideAllTiles()
	{
		foreach (var tile in _tiles)
			tile.Visible = false;
	}

	void CheckReplayDamage()
	{
		var player = FindPlayerCharacter();
		if (player == null || !player.IsAlive)
			return;

		for (var i = 0; i < _tiles.Length; i++)
		{
			if (!_tiles[i].Visible || _hitThisReplayStep[i])
				continue;

			if (!GetQuadrantRect(i).HasPoint(player.GlobalPosition))
				continue;

			_hitThisReplayStep[i] = true;
			player.TakeDamage(DamageAmount);
			player.RaiseFloatingCombatText(DamageAmount, false, (int)SpellSchool.Generic, false);
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = BossName,
				TargetName = player.CharacterName,
				AbilityName = "Stolen Memory",
				Amount = DamageAmount,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description =
					"A fragment of a stolen memory replays across the arena. Stand in the correct positions to avoid taking damage."
			});
		}
	}

	Character FindPlayerCharacter()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character character
			    && character.IsAlive
			    && character.CharacterName == GameConstants.LightkeeperName)
				return character;

		return null;
	}

	Rect2 GetQuadrantRect(int quadrantIndex)
	{
		var half = _arenaRect.Size / 2f;
		var origin = _arenaRect.Position;
		return quadrantIndex switch
		{
			0 => new Rect2(origin, half),
			1 => new Rect2(origin + new Vector2(half.X, 0f), half),
			2 => new Rect2(origin + new Vector2(0f, half.Y), half),
			_ => new Rect2(origin + half, half)
		};
	}

	List<int> GetAdjacentQuadrants(int quadrantIndex)
	{
		return quadrantIndex switch
		{
			0 => new List<int> { 1, 2 },
			1 => new List<int> { 0, 3 },
			2 => new List<int> { 0, 3 },
			_ => new List<int> { 1, 2 }
		};
	}

	void Finish()
	{
		HideAllTiles();
		_state = State.Finished;
		EmitSignalCompleted();
		QueueFree();
	}
}