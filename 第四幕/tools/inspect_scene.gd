extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("Usage: -- <scene_or_resource_path> [more_paths]")
		quit(1)
		return

	var paths: Array[String] = []
	for arg in args:
		if arg.begins_with("--pack="):
			var pack_path := arg.trim_prefix("--pack=")
			var ok := ProjectSettings.load_resource_pack(pack_path, false)
			print("load_resource_pack(", pack_path, ") = ", ok)
			continue
		paths.append(arg)

	for path in paths:
		print("--- ", path, " ---")
		if path.ends_with(".png") or path.ends_with(".jpg") or path.ends_with(".jpeg"):
			var bytes := FileAccess.get_file_as_bytes(path)
			print("raw_bytes=", bytes.size())
		var res := ResourceLoader.load(path)
		if res == null:
			print("LOAD FAILED")
			continue

		print("resource_class=", res.get_class())
		if res is PackedScene:
			var root := (res as PackedScene).instantiate()
			_dump_node(root, 0)
			root.queue_free()
		else:
			for prop in res.get_property_list():
				var name := String(prop.get("name", ""))
				if name.is_empty():
					continue
				var value := res.get(name)
				if value is Object:
					print("  ", name, " = <", value.get_class(), ">")
				else:
					print("  ", name, " = ", value)

	quit()


func _dump_node(node: Node, depth: int) -> void:
	var indent := "  ".repeat(depth)
	print(indent, node.name, " :: ", node.get_class())
	for name in ["position", "size", "scale", "global_position"]:
		if _has_property(node, name):
			print(indent, "  ", name, " = ", node.get(name))
	for prop in node.get_property_list():
		var name := String(prop.get("name", ""))
		if name in ["script", "material", "texture", "skeleton_data_res"]:
			var value := node.get(name)
			if value is Object:
				print(indent, "  ", name, " = <", value.get_class(), ">")
				if name == "skeleton_data_res":
					_dump_object(value, depth + 2)
			else:
				print(indent, "  ", name, " = ", value)

	for child in node.get_children():
		_dump_node(child, depth + 1)


func _has_property(obj: Object, prop_name: String) -> bool:
	for prop in obj.get_property_list():
		if String(prop.get("name", "")) == prop_name:
			return true
	return false


func _dump_object(obj: Object, depth: int) -> void:
	var indent := "  ".repeat(depth)
	for prop in obj.get_property_list():
		var name := String(prop.get("name", ""))
		if name.begins_with("_"):
			continue
		var usage := int(prop.get("usage", 0))
		if usage == 0:
			continue
		var value = obj.get(name)
		if value is Callable:
			continue
		if value is Object:
			print(indent, name, " = <", value.get_class(), ">")
			if name in ["atlas_res", "skeleton_file_res"]:
				_dump_object(value, depth + 1)
		else:
			print(indent, name, " = ", value)
