extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 4:
		push_error("Usage: -- <pck> <atlas_path> <json_path> <png_path>")
		quit(1)
		return

	ProjectSettings.load_resource_pack(args[0], false)

	var bytes := FileAccess.get_file_as_bytes(args[3])
	var image := Image.new()
	var err := image.load_png_from_buffer(bytes)
	if err != OK:
		push_error("png decode failed: %s" % err)
		quit(err)
		return
	var texture := ImageTexture.create_from_image(image)

	var atlas := ClassDB.instantiate("SpineAtlasResource")
	var atlas_err = atlas.call("load_from_atlas_file", args[1])
	atlas.set("textures", [texture])

	var skeleton_file := ClassDB.instantiate("SpineSkeletonFileResource")
	var skel_err = skeleton_file.call("load_from_file", args[2])

	var data_res := ClassDB.instantiate("SpineSkeletonDataResource")
	data_res.set("atlas_res", atlas)
	data_res.set("skeleton_file_res", skeleton_file)
	data_res.set("default_mix", 0.05)
	data_res.set("animation_mixes", [])
	data_res.call("update_skeleton_data")
	print("atlas_err=", atlas_err, " skel_err=", skel_err)

	var sprite := ClassDB.instantiate("SpineSprite")
	sprite.set("skeleton_data_res", data_res)
	get_root().add_child(sprite)
	await process_frame

	var skeleton = sprite.call("get_skeleton")
	print("skeleton=", skeleton)
	if skeleton == null:
		quit()
		return
	var data = skeleton.call("get_data")
	print("data=", data)
	if data == null:
		quit()
		return
	var animations = data.call("get_animations")
	print("animations=", animations)
	if animations == null:
		quit()
		return
	print("animations_size=", animations.size())
	for anim in animations:
		print(anim.call("get_name"))
	quit()
