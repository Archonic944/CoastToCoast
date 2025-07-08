using Godot;
using System;

public partial class RainSpawner : Node2D
{
	[Export] public NodePath TargetPath { get; set; }
	[Export] public double SpawnInterval { get; set; } = 10.0;
	[Export] public double CloudRadius { get; set; } = 300.0;
	[Export] public int RaindropCount { get; set; } = 25;
	[Export] public double SpawnCenterRadius { get; set; } = 40;
	[Export] public float RaindropSpeed { get; set; } = 2500.0f;
	[Export] public float RainAngle { get; set; } = 2 * Mathf.Pi / 3.0f; // Changed to 2π/3 (120 degrees) to fall down and to the left
	[Export] public float RainHeight { get; set; } = 4000f; // How far above the target to spawn raindrops

	private const float DropletHeightSeparation = 150.0f; // Random height separation between raindrops

	private PackedScene RainScene;

	private Node2D _target;
	private Timer _timer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_target = GetNode<Node2D>(TargetPath);
		_timer = GetNode<Timer>("Timer");
		//set the timer initially
		_timer.WaitTime = SpawnInterval + GD.RandRange(-3.0, 3.0);
		_timer.Timeout += OnTimerTimeout;
		_timer.Start();
		RainScene = GD.Load<PackedScene>("res://scenes/raindrop.tscn");
	}

	private void OnTimerTimeout()
	{
		MakeItRain();
		// Reset timer with slight randomization
		_timer.WaitTime = SpawnInterval + GD.RandRange(-3.0, 3.0);
		_timer.Start();
	}

	public void MakeItRain()
	{
		// Calculate a random offset for the cloud center within SpawnCenterRadius
		Vector2 centerOffset = new Vector2(
			(float)GD.RandRange(-SpawnCenterRadius, SpawnCenterRadius),
			(float)GD.RandRange(-SpawnCenterRadius, SpawnCenterRadius)
		);
		
		// Determine the cloud center position relative to the target
		Vector2 cloudCenter = _target.GlobalPosition + centerOffset;
		
		// Calculate the direction vector for rain (angled at RainAngle)
		Vector2 rainDirection = new Vector2(Mathf.Cos(RainAngle), Mathf.Sin(RainAngle)).Normalized();
		
		// Spawn the raindrops
		for (int i = 0; i < RaindropCount; i++)
		{
			// Create a random point within CloudRadius of the cloud center
			float angle = (float)GD.RandRange(0, Mathf.Tau);
			float distance = (float)GD.RandRange(0, CloudRadius);
			Vector2 raindropOffset = new Vector2(
				distance * Mathf.Cos(angle),
				distance * Mathf.Sin(angle)
			);
			
			// Calculate spawn position (offset upward and rightward based on the rain angle)
			Vector2 targetPos = cloudCenter + raindropOffset;
			
			// Add slight random vertical separation between raindrops
			float heightVariation = (float)GD.RandRange(-DropletHeightSeparation, DropletHeightSeparation);
			Vector2 spawnPos = targetPos - rainDirection * (RainHeight + heightVariation);
			
			// Instantiate the raindrop
			Raindrop raindrop = RainScene.Instantiate<Raindrop>();
			raindrop.GlobalPosition = spawnPos;
			raindrop.GlobalTarget = targetPos;
			raindrop.Speed = RaindropSpeed;
			GetTree().Root.AddChild(raindrop);
			// Go, my child, go! (or rather, fall) (the raindrop handles its own movement)
		}
	}
}
