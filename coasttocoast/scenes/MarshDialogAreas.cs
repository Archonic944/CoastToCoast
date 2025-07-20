using Godot;
using System;
using System.Linq;
using DialogueManagerRuntime;

public partial class MarshDialogAreas : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Area2D beforeSticks = GetNode<Area2D>("BeforeSticks");
		beforeSticks.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				DoDialogue(beforeSticks, "res://dialogue/Sticks.dialogue", "before_sticks");
			}
		};
		Area2D afterSticks = GetNode<Area2D>("AfterSticks");
		afterSticks.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				DoDialogue(afterSticks, "res://dialogue/Sticks.dialogue", "after_sticks");
			}
		};
		Area2D disease = GetNode<Area2D>("Disease");
		disease.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				DoDialogue(disease, "res://dialogue/Misc.dialogue", "disease");
			}
		};
		Area2D winRoom = GetNode<Area2D>("WinRoomExit");
		winRoom.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				if(GetTree().GetCurrentScene().GetMeta("win").AsBool()) 
				{DoDialogue(winRoom, "res://dialogue/WinRoom.dialogue", "tryleave");}
			}
		};
		// ugh I could've written a generic method for this
		// but I didn't
		// so here we are
		// and I don't want to refactor it now
		// but I will later
		// well
		// maybe not later
		// but at some point
		// well
		// maybe not at all
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void DoDialogue(Area2D area, string dialoguePath, string dialogueStart)
	{
		DialogueManager.ShowDialogueBalloon(GD.Load(dialoguePath), dialogueStart);
		area.SetDeferred("monitoring", false); // Can't set property directly in signal handler
	}
}
