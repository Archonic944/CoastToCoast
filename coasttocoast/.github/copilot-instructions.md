This is a Godot project using C#, featuring a top-down layout and rpg elements.

When asked to help write a script for a scene or node, read the scene file first. If unsure what the scene file is, you may list files in the res://scenes directory. Take note of all related nodes, such as child nodes and nested scenes, which you may also have to read from disk. The key here is to be as informed as possible not just about the C# code, but about the user's Godot project as well.

When instantiating a node from a packed scene, configure its instance variables before adding it to the scene tree.

Instead of editing Godot scenes directly, pause your activity and ask the user to make edits from the Godot user interface. The only exception to this rule is if you are substituting a particular value within the scene, such as a number, for a similar value. The rule does not apply to positions, which the user must change themself.