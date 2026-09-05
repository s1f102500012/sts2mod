extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2 and args.size() != 3:
		push_error("Usage: godot --headless --path <tool_project> -s pack_mod.gd -- <manifest_json> <output_pck> [import_project_root]")
		quit(1)
		return

	var manifest_source := args[0]
	var output_pck := args[1]
	var import_project_root := args[2] if args.size() == 3 else ""
	var asset_root := manifest_source.get_base_dir()
	var manifest_name := manifest_source.get_file()
	var manifest_json := JSON.parse_string(FileAccess.get_file_as_string(manifest_source))
	if typeof(manifest_json) != TYPE_DICTIONARY:
		push_error("manifest json is not a JSON object")
		quit(1)
		return

	var mod_id := String(manifest_json.get("id", ""))
	if mod_id.is_empty():
		push_error("manifest json is missing id")
		quit(1)
		return

	var packer := PCKPacker.new()
	var err := packer.pck_start(output_pck)
	if err != OK:
		push_error("pck_start failed: %s" % err)
		quit(err)
		return

	if import_project_root.is_empty():
		err = _add_asset_tree(packer, asset_root, asset_root, mod_id, manifest_name)
	else:
		err = _add_import_project(packer, import_project_root, mod_id)
	if err != OK:
		quit(err)
		return

	err = packer.flush(true)
	if err != OK:
		push_error("flush failed: %s" % err)
		quit(err)
		return

	print("Created ", output_pck)
	quit()


func _add_asset_tree(packer: PCKPacker, asset_root: String, current_dir: String, mod_id: String, manifest_name: String) -> int:
	var dir := DirAccess.open(current_dir)
	if dir == null:
		push_error("Unable to open asset dir: %s" % current_dir)
		return ERR_CANT_OPEN

	for subdir in dir.get_directories():
		var err := _add_asset_tree(packer, asset_root, current_dir.path_join(subdir), mod_id, manifest_name)
		if err != OK:
			return err

	for file_name in dir.get_files():
		if current_dir == asset_root and file_name == manifest_name:
			continue
		if file_name.begins_with("."):
			continue

		var source_path := current_dir.path_join(file_name)
		var relative_path := source_path.trim_prefix(asset_root + "/")
		var packed_path := "res://%s/%s" % [mod_id, relative_path]
		var err := packer.add_file(packed_path, source_path)
		if err != OK:
			push_error("add_file failed for %s -> %s: %s" % [source_path, packed_path, err])
			return err

	return OK


func _add_import_project(packer: PCKPacker, import_project_root: String, mod_id: String) -> int:
	var mod_root := import_project_root.path_join(mod_id)
	var err := _add_tree_with_prefix(packer, mod_root, mod_root, "res://%s" % mod_id, false)
	if err != OK:
		return err

	var imported_root := import_project_root.path_join(".godot/imported")
	if DirAccess.dir_exists_absolute(imported_root):
		err = _add_tree_with_prefix(packer, imported_root, imported_root, "res://.godot/imported", true)
		if err != OK:
			return err

	return OK


func _add_tree_with_prefix(packer: PCKPacker, root_dir: String, current_dir: String, packed_prefix: String, imported_only: bool) -> int:
	var dir := DirAccess.open(current_dir)
	if dir == null:
		push_error("Unable to open pack dir: %s" % current_dir)
		return ERR_CANT_OPEN

	for subdir in dir.get_directories():
		var err := _add_tree_with_prefix(packer, root_dir, current_dir.path_join(subdir), packed_prefix, imported_only)
		if err != OK:
			return err

	for file_name in dir.get_files():
		if file_name.begins_with("."):
			continue
		if imported_only and not (file_name.ends_with(".ctex") or file_name.ends_with(".stex") or file_name.ends_with(".res") or file_name.ends_with(".sample")):
			continue
		if file_name.ends_with(".md5"):
			continue

		var source_path := current_dir.path_join(file_name)
		var relative_path := source_path.trim_prefix(root_dir + "/")
		var packed_path := packed_prefix.path_join(relative_path)
		var err := packer.add_file(packed_path, source_path)
		if err != OK:
			push_error("add_file failed for %s -> %s: %s" % [source_path, packed_path, err])
			return err

	return OK
