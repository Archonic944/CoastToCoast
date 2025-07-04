using Godot;
using System;

public partial class Kid : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	private Vector2 LastDirection = Vector2.Right;

	public override void _PhysicsProcess(double delta)
	{
		// Get AnimatedSprite2D node
		var animSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Velocity = direction * Speed;
		MoveAndSlide();

		// Animation logic
		if (direction == Vector2.Zero)
		{
			// Idle animations
			if (Mathf.Abs(LastDirection.Y) > Mathf.Abs(LastDirection.X))
			{
				if (LastDirection.Y < 0)
					animSprite.Play("idle back");
				else
					animSprite.Play("idle front");
			}
			else
			{
				animSprite.Play("idle side");
				animSprite.FlipH = LastDirection.X < 0;
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
			LastDirection = direction;
		}
	}
}
