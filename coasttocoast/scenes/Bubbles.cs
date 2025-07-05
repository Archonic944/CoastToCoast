using Godot;
using System;

public partial class Bubbles : Control
{
	[Export] public int BubbleCount { get; set; } = 6;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var bubble = GetNode<Bubble>("Bubble");
		for (int i = 0; i < BubbleCount; i++)
		{
			if (bubble.Duplicate() is Bubble newBubble)
			{
				newBubble.Position += Vector2.Right*40;
				AddChild(newBubble);
				bubble = newBubble;
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
