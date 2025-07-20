using System;
using Godot;
using DialogueManagerRuntime;


public partial class Kid : CharacterBody2D
{
	[Export] private NodePath _functionalTileMapLayer;
	// This is the main character of the game, the kid.
	private const float Speed = 300.0f;
	private const float CharacterHeight = 30.2f;
	private const float MudSinkingSecs = 2f;
	public float MudSinkingProgress = 0.0f;

	private Bubbles _b;

	private TileMapLayer _ftml;
	// increment per second
	private Vector2 _lastDirection = Vector2.Right;
	private Vector2 _inputDirection = Vector2.Zero;

	private AudioStreamPlayer _footstepsSound;
	private AnimatedSprite2D _animatedSprite;
		
	// Interaction nodes
	private Area2D _interactUp;
	private Area2D _interactDown;
	private Area2D _interactLeft;
	private Area2D _interactRight;

	public bool IsBeingHugged = false;
	public sbyte ChestPieces = 0;

	public byte iframes = 0;

	public override void _Ready()
	{
		_footstepsSound = GetNode<AudioStreamPlayer>("Footsteps");
		_animatedSprite = GetNode<AnimatedSprite2D>("ColorRect/AnimatedSprite2D");
		_ftml = GetNode<TileMapLayer>(_functionalTileMapLayer);
		if (_ftml == null)
		{
			GD.PrintErr("FunctionalTileMapLayer not set or invalid. Please set it in the inspector.");
		}
			
		// Get the interaction areas
		_interactUp = GetNode<Area2D>("Interacts/Up");
		_interactDown = GetNode<Area2D>("Interacts/Down");
		_interactLeft = GetNode<Area2D>("Interacts/Left");
		_interactRight = GetNode<Area2D>("Interacts/Right");

		DialogueManager.DialogueStarted += OnDialogueStarted;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Handle movement input
		_inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
			
		// If we detect any movement input, update last direction only if we're actually moving
		if (_inputDirection != Vector2.Zero)
		{
			_lastDirection = _inputDirection;
		}
		if (@event.IsActionPressed("ui_accept"))
		{
			TryInteract();
		}
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			GD.Print("Detected left mouse button click");
			if (ChestPieces > 0)
			{
				GD.Print("Throwing chest piece");
				ThrowChestPiece(GetGlobalMousePosition());
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if(iframes > 0) iframes--;
		var animSprite = _animatedSprite; // replaced local GetNode
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
							TryGetBubbles().Drowning = true;
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

		if (inMud)
		{
			// Offset sprite down out of ColorRect, clips children
			animSprite.Offset = new Vector2(0, MudSinkingProgress * CharacterHeight); //TODO Offset overwritten every frame, could cause state issues
		}else
		{
			MudSinkingProgress = 0.0f;
			animSprite.Offset = Vector2.Zero;
		}
		
		if (!IsBeingHugged && MudSinkingProgress < 1.0f)
		{
			DiscardBubbles();
		}
			
		// Apply movement based on input direction
		Velocity = _inputDirection * Speed * moveSpeedMultiplier;
		MoveAndSlide();

		// Animation logic
		if (_inputDirection == Vector2.Zero)
		{
			// Idle animations
			if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
			{
				if (_lastDirection.Y < 0)
					animSprite.Play("idle back");
				else
					animSprite.Play("idle front");
				animSprite.FlipH = false;
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
			if (Mathf.Abs(_inputDirection.Y) > Mathf.Abs(_inputDirection.X))
			{
				if (_inputDirection.Y < 0)
					animSprite.Play("walk back");
				else
					animSprite.Play("walk front");
				animSprite.FlipH = false;
			}
			else
			{
				animSprite.Play("walk side");
				animSprite.FlipH = _inputDirection.X < 0;
			}
			if(!_footstepsSound.Playing)
			{
				_footstepsSound.Play();
			}
		}
	}

	private void CreateBubbles()
	{
		var bubblesScene = GD.Load<PackedScene>("res://scenes/bubbles.tscn");
		_b = bubblesScene.Instantiate<Bubbles>();
		_b.FadeIn();
		// attach to drowned signal with game over
		_b.Drowned += () =>
		{
			CreateTween().TweenProperty(GetTree().GetCurrentScene(), "modulate", Colors.Transparent, 1f).Finished +=
				() => {
					DialogueManager.ShowDialogueBalloon(GD.Load("res://dialogue/Misc.dialogue"), "drowned");
				};
		};
		var ui = GetTree().GetCurrentScene().GetNode<CanvasLayer>("UI");
		ui?.AddChild(_b);
	}
		
	private void TryInteract()
	{
		// Get the appropriate interaction area based on facing direction
		Area2D activeArea = GetActiveInteractArea();
			
		// Check for overlapping areas
		foreach (var area in activeArea.GetOverlappingAreas())
		{
			var parent = area.GetParent();
			if (parent is Interactable interactable)
			{
				interactable.Interact(this);
				return;
			}
		}
			
		foreach (var body in activeArea.GetOverlappingBodies())
		{
			// Try the body itself first
			if (body is Interactable interactable)
			{
				interactable.Interact(this);
				return;
			}
				
			// Try its parent
			var parent = body.GetParent();
			if (parent is Interactable parentInteractable)
			{
				parentInteractable.Interact(this);
				return;
			}
		}
	}
	
	private Bubbles TryGetBubbles()
	{
		// Lazy initialization with validity check
		if (_b == null || !IsInstanceValid(_b))
		{
			CreateBubbles();
			return _b;
		}
		else
		{
			return _b;
		}
	}

	private void DiscardBubbles()
	{
		if (_b != null)
		{
			if (IsInstanceValid(_b))
			{
				TryGetBubbles().Drowning = false;
			}
		}
	}
		
	private Area2D GetActiveInteractArea()
	{
		// Determine active area based on the last direction
		if (Mathf.Abs(_lastDirection.Y) > Mathf.Abs(_lastDirection.X))
		{
			return _lastDirection.Y < 0 ? _interactUp : _interactDown;
		}
		else
		{
			return _lastDirection.X < 0 ? _interactLeft : _interactRight;
		}
	}

	private void OnDialogueStarted(Resource dialogueResource)
	{
		// stop movement
		_inputDirection = Vector2.Zero;
		_footstepsSound.Stop();
		_animatedSprite.Stop();
	}

	public override void _ExitTree()
	{
		DialogueManager.DialogueStarted -= OnDialogueStarted;
	}

	// Method to spawn and throw a chest piece
	private void ThrowChestPiece(Vector2 targetPosition)
	{
		ChestPieces--;
		GetTree().GetCurrentScene().GetNode<RichTextLabel>("UI/ChestPieceHUD/RichTextLabel")?.SetText(ChestPieces + "/4");
		var chestScene = GD.Load<PackedScene>("res://scenes/chest_piece.tscn");
		var chest = chestScene.Instantiate<ChestPiece>();
		GetTree().GetCurrentScene().AddChild(chest);
		chest.GetNode<Sprite2D>("Sprite2D")?.SetFrame(ChestPieces);
		chest.GlobalPosition = GlobalPosition;
		chest.StartThrow(targetPosition);
	}

	// Called by Hugger when it latches
	public void StartHugging()
	{
		if (!IsBeingHugged)
		{
			IsBeingHugged = true;
			TryGetBubbles().Drowning = true;
		}
	}

	// Called by Hugger when it detaches
	public void StopHugging()
	{
		if (IsBeingHugged)
		{
			IsBeingHugged = false;
			DiscardBubbles();
		}
	}
}