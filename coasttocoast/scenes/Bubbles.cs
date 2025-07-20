using Godot;

public partial class Bubbles : Control
{
	public bool Drowning = true;
	
	[Export] 
	public int BubbleCount { get; set; } = 6;
	// Called when the node enters the scene tree for the first time.
	private Bubble currentBubble;

	[Signal]
	public delegate void DrownedEventHandler();
	public override void _Ready()
	{
		AddUserSignal("drowned");
		currentBubble = GetNode<Bubble>("BubblesList/Bubble");
		for (int i = 0; i < BubbleCount; i++)
		{
			if (currentBubble.Duplicate() is Bubble newBubble)
			{
				newBubble.Position += Vector2.Right*80;
				GetNode("BubblesList").AddChild(newBubble);
				currentBubble = newBubble;
			}
		}

		if (Drowning)
		{
			currentBubble.Shake = true;
			Timer t = GetNode<Timer>("PopTimer");
			t.Start();
			t.Timeout += () =>
			{
				if (!Drowning)
				{
					if (GetNode("BubblesList").GetChildCount() >= BubbleCount)
					{
						FadeOut();
						t.Stop();
					}
					else
					{
						NewBubble();
					}
				}
				else
				{
					currentBubble.Pop();
					GetNode<AudioStreamPlayer>("PopSound").Play();
					Node bList = GetNode("BubblesList");

					if (bList.GetChildCount() <= 1)
					{
						t.Stop();
						EmitSignal(SignalName.Drowned);
						FadeOut();
					}
					else
					{
						currentBubble = bList.GetChild<Bubble>(-2);
						currentBubble.Shake = true;
						t.Start();
					}
				}
			};
		}
	}

	public void FadeIn()
	{
		Show();
		Modulate = Colors.Transparent;
		Tween t = CreateTween();
		t.TweenProperty(this, "modulate", Colors.White, 0.5f);
	}

	public void FadeOut()
	{
		Drowning = false;
		// set bubbles to not shake
		// foreach (Node child in GetNode("BubblesList").GetChildren())
		// {
		// 	if(child is Bubble bubble)
		// 	{
		// 		bubble.Shake = false;
		// 	}
		// }
		// Tween t = CreateTween();
		// t.TweenProperty(this, "modulate", Colors.Transparent, 0.5f);
		// t.Finished += QueueFree;
		QueueFree();
	}

	private void NewBubble()
	{
		if (currentBubble.Duplicate() is Bubble newBubble)
		{
			newBubble.Position += Vector2.Right * 80;
			GetNode("BubblesList").AddChild(newBubble);
			currentBubble = newBubble;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
