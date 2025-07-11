using System;
using Godot;

public partial class Kid : CharacterBody2D
{
	[Export] private NodePath FunctionalTileMapLayer;
    // This is the main character of the game, the kid.
    private const float Speed = 300.0f;
	private const float CharacterHeight = 30.2f;
	private const float MudSinkingSecs = 2f;
	public float MudSinkingProgress = 0.0f;

	private Bubbles _b;

	private TileMapLayer _ftml;
	// increment per second
	private Vector2 _lastDirection = Vector2.Right;

	private AudioStreamPlayer _footstepsSound;

	public override void _Ready()
	{
		_footstepsSound = GetNode<AudioStreamPlayer>("Footsteps");
		_ftml = GetNode<TileMapLayer>(FunctionalTileMapLayer);
		if (_ftml == null)
		{
			GD.PrintErr("FunctionalTileMapLayer not set or invalid. Please set it in the inspector.");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var animSprite = GetNode<AnimatedSprite2D>("ColorRect/AnimatedSprite2D");
		var inMud = false;
		float moveSpeedMultiplier = 1.0f;
		if (_ftml != null)
		{
			Vector2 globalPos = GlobalPosition;
			Vector2I tileCoords = _ftml.LocalToMap(_ftml.ToLocal(globalPos));
			var tileData = _ftml.GetCellTileData(tileCoords);
			if (tileData != null)
			{
				var moveSpeedObj = tileData.GetCustomData("MoveSpeed");
				if (moveSpeedObj.VariantType == Variant.Type.Float)
				{
					float msFloat = moveSpeedObj.AsSingle();
					if (msFloat != 0f) moveSpeedMultiplier = msFloat;
				}
					
				var terrain = tileData.GetTerrain();
				if (terrain != -1)
				{
					if (terrain == TerrainType.Mud)
					{
						inMud = true;
						MudSinkingProgress += (float)delta / MudSinkingSecs;
						if (MudSinkingProgress > 1f)
						{
							MudSinkingProgress = 1.0f;
							if (_b == null)
							{
								var bubblesScene = GD.Load<PackedScene>("res://scenes/bubbles.tscn");
								_b = bubblesScene.Instantiate<Bubbles>();
								_b.FadeIn();
								GetTree().GetCurrentScene().GetNode<CanvasLayer>("UI").AddChild(_b);
							}
						}
					}

					if (!_footstepsSound.Name.ToString().EndsWith(terrain.ToString()))
					{
						AudioStreamPlayer potentialFootsteps =
							GetNodeOrNull<AudioStreamPlayer>("Footsteps" + terrain);
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
			animSprite.Offset = new Vector2(0, MudSinkingProgress * CharacterHeight); //TODO offset overwritten every frame, could cause state issues
		else
		{
			if (_b != null)
			{
				if (IsInstanceValid(_b)) _b.FadeOut();
				_b = null;
			}
			animSprite.Offset = Vector2.Zero;
			MudSinkingProgress = 0.0f; // Reset sinking progress when not in mud
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
