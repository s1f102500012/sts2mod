extends Node2D

@export var center_position: Vector2 = Vector2.ZERO

const DURATION := 1.15

var _elapsed: float = 0.0
var _logged_first_frame: bool = false
var _spawned_follow_up_wave: bool = false
var _spark_texture: Texture2D = null
var _sparks: Array = []


func _ready() -> void:
	top_level = true
	z_index = 2600
	position = Vector2.ZERO
	_spark_texture = _load_texture("res://StS1Act4/extracted/sts1_vfx/glowSpark2.png")
	_spawn_wave(true, 0.0)
	print("[StS1Act4.HeartVfxGD] buff vfx ready center=%s" % str(center_position))
	set_process(true)
	queue_redraw()


func _process(delta: float) -> void:
	if not _logged_first_frame:
		_logged_first_frame = true
		print("[StS1Act4.HeartVfxGD] buff vfx first frame center=%s" % str(center_position))

	_elapsed += delta
	if not _spawned_follow_up_wave and _elapsed >= 0.12:
		_spawned_follow_up_wave = true
		_spawn_wave(false, _elapsed)

	queue_redraw()
	if _elapsed >= DURATION:
		queue_free()


func _exit_tree() -> void:
	print("[StS1Act4.HeartVfxGD] buff vfx exit center=%s" % str(center_position))


func _draw() -> void:
	var t: float = clamp(_elapsed / DURATION, 0.0, 1.0)
	var pulse_alpha: float = t / 0.12 if t < 0.12 else clamp(1.0 - (t - 0.12) / 0.88, 0.0, 1.0)

	draw_circle(center_position, lerp(42.0, 170.0, t), Color(0.45, 0.0, 0.03, pulse_alpha * 0.18))
	draw_circle(center_position, lerp(16.0, 92.0, t), Color(1.0, 0.08, 0.12, pulse_alpha * 0.54))
	draw_circle(center_position, lerp(8.0, 48.0, t), Color(1.0, 0.42, 0.48, pulse_alpha * 0.28))

	if _spark_texture != null:
		_draw_glow(center_position, lerp(110.0, 430.0, t), Color(1.0, 0.08, 0.12, pulse_alpha * 0.72), _elapsed * 0.5)
		_draw_glow(center_position, lerp(80.0, 300.0, t), Color(1.0, 0.38, 0.42, pulse_alpha * 0.42), -_elapsed * 0.35)

	var spike_alpha := clamp(1.0 - t * 0.9, 0.0, 1.0)
	for i in range(10):
		var angle: float = TAU * float(i) / 10.0 + _elapsed * 0.8
		var direction: Vector2 = Vector2.from_angle(angle)
		var start: Vector2 = center_position + direction * lerp(16.0, 40.0, t)
		var finish: Vector2 = center_position + direction * lerp(110.0, 240.0, t)
		draw_line(start, finish, Color(1.0, 0.18, 0.22, spike_alpha * 0.24), 7.0)

	for spark in _sparks:
		var wave_time: float = spark["wave_time"]
		var duration: float = spark["duration"]
		var local_t: float = (_elapsed - wave_time) / duration
		if local_t < 0.0 or local_t > 1.0:
			continue

		var start_pos: Vector2 = spark["start"]
		var control_pos: Vector2 = spark["control"]
		var finish_pos: Vector2 = spark["finish"]
		var position_now: Vector2 = _quadratic_bezier(start_pos, control_pos, finish_pos, local_t)
		var previous_t: float = max(local_t - 0.08, 0.0)
		var previous_position: Vector2 = _quadratic_bezier(start_pos, control_pos, finish_pos, previous_t)
		var alpha: float = local_t / 0.12 if local_t < 0.12 else clamp(1.0 - (local_t - 0.12) / 0.88, 0.0, 1.0)
		var size: float = lerp(float(spark["start_size"]), float(spark["end_size"]), local_t)
		var rotation: float = spark["rotation"]
		var rotation_speed: float = spark["rotation_speed"]

		draw_line(previous_position, position_now, Color(1.0, 0.14, 0.18, alpha * 0.62), size * 1.3)
		draw_circle(position_now, size * 1.5, Color(0.45, 0.0, 0.02, alpha * 0.28))
		draw_circle(position_now, size, Color(1.0, 0.04, 0.11, alpha * 0.96))
		if _spark_texture != null:
			_draw_glow(position_now, size * 11.0, Color(1.0, 0.35, 0.40, alpha * 0.48), rotation + local_t * rotation_speed)


func _spawn_wave(primary: bool, wave_time: float) -> void:
	var count := 22 if primary else 16
	for _i in range(count):
		var angle: float = randf() * TAU
		var radial: Vector2 = Vector2.from_angle(angle)
		var perpendicular: Vector2 = Vector2(-radial.y, radial.x)
		var start_radius: float = randf_range(150.0, 280.0) if primary else randf_range(90.0, 180.0)
		var end_radius: float = randf_range(6.0, 22.0)
		var start: Vector2 = center_position + radial * start_radius + Vector2(randf_range(-18.0, 18.0), randf_range(-14.0, 14.0))
		var finish: Vector2 = center_position + radial * end_radius
		var control: Vector2 = start.lerp(finish, 0.34) + perpendicular * randf_range(-130.0, 130.0)
		_sparks.append({
			"wave_time": wave_time,
			"start": start,
			"control": control,
			"finish": finish,
			"duration": randf_range(0.36, 0.72) if primary else randf_range(0.26, 0.52),
			"start_size": randf_range(8.0, 13.0) if primary else randf_range(6.0, 10.0),
			"end_size": randf_range(2.0, 4.0),
			"rotation": randf() * TAU,
			"rotation_speed": randf_range(-5.0, 5.0)
		})


func _draw_glow(draw_position: Vector2, pixel_size: float, color: Color, rotation: float) -> void:
	var rect_size := Vector2.ONE * pixel_size
	draw_set_transform(draw_position, rotation, Vector2.ONE)
	draw_texture_rect(_spark_texture, Rect2(-rect_size * 0.5, rect_size), false, color)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


func _quadratic_bezier(start: Vector2, control: Vector2, finish: Vector2, t: float) -> Vector2:
	var p0 := start.lerp(control, t)
	var p1 := control.lerp(finish, t)
	return p0.lerp(p1, t)


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
