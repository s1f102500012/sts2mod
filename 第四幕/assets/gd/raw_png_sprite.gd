extends Sprite2D

@export var texture_path: String = ""


func _ready() -> void:
	if texture_path.is_empty():
		return

	var bytes := FileAccess.get_file_as_bytes(texture_path)
	if bytes.is_empty():
		push_warning("Failed to read texture bytes: %s" % texture_path)
		return

	var image := Image.new()
	var err := OK
	if texture_path.to_lower().ends_with(".jpg") or texture_path.to_lower().ends_with(".jpeg"):
		err = image.load_jpg_from_buffer(bytes)
	else:
		err = image.load_png_from_buffer(bytes)

	if err != OK:
		push_warning("Failed to decode texture: %s (%s)" % [texture_path, err])
		return

	texture = ImageTexture.create_from_image(image)
