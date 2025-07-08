using Godot;
using System;

public partial class Raindrop : Node2D
{
    public Vector2 GlobalTarget { get; set; } = Vector2.Inf;
    public double Speed = 50.0;
	
    private AnimationPlayer _animationPlayer;
    private bool _reachedTarget = false;
	
    public override void _Ready()
    {
        if (GlobalTarget == Vector2.Inf)
        {
            GD.PrintErr("GlobalTarget not set for Raindrop. Please set it before using the raindrop.");
            QueueFree();
            return;
        }
		
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        _animationPlayer.AnimationFinished += OnAnimationFinished;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
        if (_reachedTarget)
        {
            return; // Stop moving if target is reached
        }
		
        // Calculate direction to target
        Vector2 direction = (GlobalTarget - GlobalPosition).Normalized();
		
        // Calculate distance to move this frame
        float distanceToMove = (float)(Speed * delta);
		
        // Calculate distance to target
        float distanceToTarget = GlobalPosition.DistanceTo(GlobalTarget);
		
        if (distanceToMove >= distanceToTarget)
        {
            // We've reached the target
            GlobalPosition = GlobalTarget;
            _reachedTarget = true;
            PlaySplashAnimation();
        }
        else
        {
            // Move towards target
            GlobalPosition += direction * distanceToMove;
        }
    }
	
    private void PlaySplashAnimation()
    {
        _animationPlayer.Play("raindrop_splash");
    }
	
    private void OnAnimationFinished(StringName animName)
    {
        if (animName == "raindrop_splash")
        {
            QueueFree(); // Remove the raindrop when animation finishes
        }
    }
}