using Godot;
using System;

public partial class Footsteps : AudioStreamPlayer
{
	public override void _Ready()
	{
		SetVolumeDb(-4); // Why can't I do this in the editor...? Well, I probably can, this is just easier.
		Finished += () => Play();
	}
}
