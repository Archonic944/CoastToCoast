using System;
using Godot;

public partial class Kid : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float CharacterHeight = 34.5f;
	public const float MudSinkingSecs = 2f;
	private float _mudSinkingProgress = 0.0f;

	private Bubbles _b;
	// increment per second
	private Vector2 _lastDirection = Vector2.Right;

	private AudioStreamPlayer _footstepsSound;

	public override void _Ready()
	{
		_footstepsSound = GetNode<AudioStreamPlayer>("Footsteps");
	}

	public override void _PhysicsProcess(double delta)
	{
		var animSprite = GetNode<AnimatedSprite2D>("ColorRect/AnimatedSprite2D");
		var mudTiles = GetParent().GetNodeOrNull<TileMapLayer>("MudTiles");
		var inMud = false;
		float moveSpeedMultiplier = 1.0f;
		if (mudTiles != null)
		{
			Vector2 globalPos = GlobalPosition;
			Vector2I tileCoords = mudTiles.LocalToMap(mudTiles.ToLocal(globalPos));
			var tileData = mudTiles.GetCellTileData(tileCoords);
			if (tileData != null)
			{
				var moveSpeedObj = tileData.GetCustomData("MoveSpeed");
				if (moveSpeedObj.VariantType == Variant.Type.Float)
					moveSpeedMultiplier = moveSpeedObj.AsSingle();
				var terrain = tileData.GetCustomData("Terrain");
				if (terrain.VariantType == Variant.Type.String)
				{
					string terrainType = terrain.AsString();
					if (terrainType == "mud")
					{
						inMud = true;
						_mudSinkingProgress += (float)delta / MudSinkingSecs;
						if (_mudSinkingProgress > 1f)
						{
							_mudSinkingProgress = 1.0f;
							if (_b == null)
							{
								var bubblesScene = GD.Load<PackedScene>("res://scenes/bubbles.tscn");
								_b = bubblesScene.Instantiate<Bubbles>();
								_b.FadeIn();
								GetTree().GetRoot().AddChild(_b);
							}
						}
					}

					if (!_footstepsSound.Name.ToString().Contains(terrainType))
					{
						AudioStreamPlayer potentialFootsteps =
							GetNodeOrNull<AudioStreamPlayer>(terrainType + "Footsteps");
						if (potentialFootsteps != null)
						{
							if (_footstepsSound is { Playing: true })
							{
								_footstepsSound.Stop();
							}
							_footstepsSound = potentialFootsteps;
						}
					}
				}
			}
			else
			{
				if(!(_footstepsSound != null && _footstepsSound.Name.ToString() == "Footsteps"))
				{
					if (_footstepsSound is { Playing: true }) _footstepsSound.Stop();
					_footstepsSound = GetNode<AudioStreamPlayer>("Footsteps");
				}
			}
		}
		if (inMud) // Offset sprite down
			animSprite.Offset = new Vector2(0, _mudSinkingProgress * CharacterHeight); //TODO offset overwritten every frame, could cause state issues
		else
		{
			if (_b != null)
			{
				if (_b.IsInsideTree()) _b.FadeOut();
				_b = null;
			}
			animSprite.Offset = Vector2.Zero;
			_mudSinkingProgress = 0.0f; // Reset sinking progress when not in mud
		}
		
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Velocity = direction * Speed * moveSpeedMultiplier;
		MoveAndSlide();

		// Animation logic
		if (direction == Vector2.Zero)
		{
			// Idle animations
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
			{
				if (_lastDirection.Y < 0)
					animSprite.Play("idle back");
				else
					animSprite.Play("idle front");
			}
			else
			{
				animSprite.Play("idle side");
				animSprite.FlipH = _lastDirection.X < 0;
			}
			if (_footstepsSound.Playing)
			{
				_footstepsSound.Stop();
			}
		}
		else
		{
			// Walk animations
			if (Mathf.Abs(direction.Y) > Mathf.Abs(direction.X))
			{
				if (direction.Y < 0)
					animSprite.Play("walk back");
				else
					animSprite.Play("walk front");
			}
			else
			{
				animSprite.Play("walk side");
				animSprite.FlipH = direction.X < 0;
			}
			_lastDirection = direction;
			if(!_footstepsSound.Playing)
			{
				_footstepsSound.Play();
			}
		}
	}
}
