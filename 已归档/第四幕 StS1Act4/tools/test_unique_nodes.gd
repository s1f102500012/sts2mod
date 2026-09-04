extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		push_error("Usage: -- <pck> <scene_path> [scene_path...]")
		quit(1)
		return

	ProjectSettings.load_resource_pack(args[0], false)
	for i in range(1, args.size()):
		var scene_path := args[i]
		print("--- ", scene_path, " ---")
		var scene := load(scene_path)
		if scene == null:
			print("load failed")
			continue
		var inst := (scene as PackedScene).instantiate()
		for name in ["%Visuals", "%Bounds", "%IntentPos", "%CenterPos"]:
			var node := inst.get_node_or_null(name)
			print(name, " => ", node)
		inst.queue_free()
	quit()
