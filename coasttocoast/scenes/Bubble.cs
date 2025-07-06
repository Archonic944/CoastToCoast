using Godot;
using System;

public partial class Bubble : Node2D
{
	public bool Shake = false;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Shake)
		{
			var sprite = GetNode<Sprite2D>("Sprite2D");
			sprite.Offset = new Vector2(
				GD.Randf() * 4 - 2,
				GD.Randf() * 4 - 2
			);
		}
		else
		{
			var sprite = GetNode<Sprite2D>("Sprite2D");
			sprite.Offset = Vector2.Zero; // Reset position if not shaking
		}
	}

	public void Pop()
	{
		GetNode<Sprite2D>("Sprite2D").Hide();
		QueueFree();
	}
}
