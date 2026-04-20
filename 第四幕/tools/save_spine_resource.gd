extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Usage: -- <scene_path> <output_tres>")
		quit(1)
		return

	var scene_path := args[0]
	var output_path := args[1]
	var scene := load(scene_path)
	if scene == null or not (scene is PackedScene):
		push_error("Failed to load scene: %s" % scene_path)
		quit(2)
		return

	var root := (scene as PackedScene).instantiate()
	var visuals := root.get_node_or_null("Visuals")
	if visuals == null:
		push_error("Scene has no Visuals node: %s" % scene_path)
		quit(3)
		return

	var skeleton_data = visuals.get("skeleton_data_res")
	if skeleton_data == null:
		push_error("Visuals has no skeleton_data_res: %s" % scene_path)
		quit(4)
		return

	var err := ResourceSaver.save(skeleton_data, output_path)
	if err != OK:
		push_error("ResourceSaver.save failed: %s" % err)
		quit(err)
		return

	print("saved ", output_path)
	root.queue_free()
	quit()
