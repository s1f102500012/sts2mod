extends SceneTree


func _init() -> void:
	for spine_class in ["SpineAtlasResource", "SpineSkeletonFileResource", "SpineSkeletonDataResource", "SpineSprite"]:
		print("--- ", spine_class, " ---")
		var obj = ClassDB.instantiate(spine_class)
		if obj == null:
			print("instantiate failed")
			continue
		for prop in obj.get_property_list():
			var name := String(prop.get("name", ""))
			if name.begins_with("_"):
				continue
			var usage := int(prop.get("usage", 0))
			if usage == 0:
				continue
			var value = obj.get(name)
			if value is Object:
				print(name, " = <", value.get_class(), ">")
			else:
				print(name, " = ", value)
		print("methods:")
		for m in obj.get_method_list():
			var n := String(m.get("name", ""))
			if not n.begins_with("_"):
				print("  ", n)
				if n in ["load_from_atlas_file", "load_from_file", "update_skeleton_data"]:
					print("    info=", m)
	quit()
