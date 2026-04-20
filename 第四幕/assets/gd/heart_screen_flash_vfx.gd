extends Control

@export var is_buff_flash: bool = false

var _elapsed: float = 0.0
var _duration: float = 0.5
var _border_texture: Texture2D = null
var _logged_first_frame: bool = false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	z_index = 3200
	_duration = 0.58 if is_buff_flash else 0.38
	_border_texture = _load_texture("res://StS1Act4/extracted/sts1_vfx/borderGlow2.png")
	print("[StS1Act4.HeartVfxGD] screen flash ready buff=%s size=%s" % [str(is_buff_flash), str(size)])
	set_process(true)
	queue_redraw()


func _process(delta: float) -> void:
	if not _logged_first_frame:
		_logged_first_frame = true
		print("[StS1Act4.HeartVfxGD] screen flash first frame buff=%s" % str(is_buff_flash))

	_elapsed += delta
	queue_redraw()
	if _elapsed >= _duration:
		queue_free()


func _exit_tree() -> void:
	print("[StS1Act4.HeartVfxGD] screen flash exit buff=%s" % str(is_buff_flash))


func _draw() -> void:
	var rect_size := size
	if rect_size.length_squared() <= 0.001:
		rect_size = get_viewport_rect().size

	var t: float = clamp(_elapsed / _duration, 0.0, 1.0)
	var fade: float = t / 0.14 if t < 0.14 else clamp(1.0 - (t - 0.14) / 0.86, 0.0, 1.0)
	var border_alpha: float = fade * (0.76 if is_buff_flash else 0.48)
	var fill_alpha: float = fade * (0.18 if is_buff_flash else 0.10)

	draw_rect(Rect2(Vector2.ZERO, rect_size), Color(0.34, 0.01, 0.05, fill_alpha), true)
	if _border_texture != null:
		var pulse_scale: float = 1.0 + (1.0 - t) * (0.06 if is_buff_flash else 0.03)
		var scaled_size: Vector2 = rect_size * pulse_scale
		var offset: Vector2 = (rect_size - scaled_size) * 0.5
		draw_texture_rect(_border_texture, Rect2(offset, scaled_size), false, Color(1.0, 0.08, 0.12, border_alpha))
		draw_texture_rect(_border_texture, Rect2(offset, scaled_size), false, Color(1.0, 0.42, 0.46, border_alpha * 0.38))


func _load_texture(path: String) -> Texture2D:
	var bytes := FileAccess.get_file_as_bytes(path)
	if bytes.is_empty():
		push_warning("Failed to read texture bytes: %s" % path)
		return null

	var image := Image.new()
	var err := image.load_png_from_buffer(bytes)
	if err != OK:
		push_warning("Failed to decode texture: %s (%s)" % [path, err])
		return null

	return ImageTexture.create_from_image(image)
