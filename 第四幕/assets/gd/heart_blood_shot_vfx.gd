extends Node2D

@export var source_position: Vector2 = Vector2.ZERO
@export var target_position: Vector2 = Vector2.ZERO
@export var projectile_count: int = 15
@export var first_hit_delay: float = 0.23
@export var hit_interval: float = 0.035
@export var flight_duration: float = 0.11
@export var grow_duration: float = 0.055
@export var hold_duration: float = 0.07

var _elapsed: float = 0.0
var _max_end_time: float = 0.0
var _logged_first_frame: bool = false
var _spark_texture: Texture2D = null
var _projectiles: Array = []
var _impacts: Array = []


func _ready() -> void:
	top_level = true
	z_index = 2800
	position = Vector2.ZERO
	_spark_texture = _load_texture("res://StS1Act4/extracted/sts1_vfx/glowSpark2.png")
	_build_projectiles()
	print(
		"[StS1Act4.HeartVfxGD] blood volley ready source=%s target=%s count=%s"
		% [str(source_position), str(target_position), str(projectile_count)]
	)
	set_process(true)
	queue_redraw()


func _process(delta: float) -> void:
	if not _logged_first_frame:
		_logged_first_frame = true
		print("[StS1Act4.HeartVfxGD] blood volley first frame source=%s target=%s" % [str(source_position), str(target_position)])

	_elapsed += delta
	for projectile in _projectiles:
		var hit_time: float = projectile["hit_time"]
		if _elapsed >= hit_time and not bool(projectile["hit_spawned"]):
			projectile["hit_spawned"] = true
			_impacts.append({
				"time": hit_time,
				"position": projectile["finish"],
				"radius": float(projectile["size"]) * 2.4
			})

	queue_redraw()
	if _elapsed >= _max_end_time + 0.35:
		queue_free()


func _exit_tree() -> void:
	print("[StS1Act4.HeartVfxGD] blood volley exit source=%s target=%s" % [str(source_position), str(target_position)])


func _draw() -> void:
	for projectile in _projectiles:
		var appear_time: float = projectile["appear_time"]
		var grow_end_time: float = projectile["grow_end_time"]
		var move_start_time: float = projectile["move_start_time"]
		var hit_time: float = projectile["hit_time"]
		var start_pos: Vector2 = projectile["start"]
		var control_pos: Vector2 = projectile["control"]
		var finish_pos: Vector2 = projectile["finish"]
		var base_size: float = projectile["size"]
		var rotation: float = projectile["rotation"]
		var rotation_speed: float = projectile["rotation_speed"]

		if _elapsed < appear_time or _elapsed > hit_time:
			continue

		var pos: Vector2
		var prev_1: Vector2
		var prev_2: Vector2
		var alpha: float = 1.0
		var size: float = base_size
		var phase_rotation_t: float = 0.0

		if _elapsed < grow_end_time:
			var grow_t: float = clamp((_elapsed - appear_time) / max(grow_duration, 0.001), 0.0, 1.0)
			pos = start_pos
			prev_1 = start_pos
			prev_2 = start_pos
			alpha = grow_t
			size = lerp(base_size * 0.18, base_size, 1.0 - pow(1.0 - grow_t, 2.0))
			phase_rotation_t = grow_t
		elif _elapsed < move_start_time:
			var hold_t: float = clamp((_elapsed - grow_end_time) / max(hold_duration, 0.001), 0.0, 1.0)
			var pulse: float = 0.92 + 0.08 * sin(hold_t * PI * 2.0)
			pos = start_pos
			prev_1 = start_pos
			prev_2 = start_pos
			alpha = 1.0
			size = base_size * pulse
			phase_rotation_t = 1.0 + hold_t
		else:
			var move_t: float = clamp((_elapsed - move_start_time) / max(flight_duration, 0.001), 0.0, 1.0)
			pos = _quadratic_bezier(start_pos, control_pos, finish_pos, move_t)
			prev_1 = _quadratic_bezier(start_pos, control_pos, finish_pos, max(move_t - 0.12, 0.0))
			prev_2 = _quadratic_bezier(start_pos, control_pos, finish_pos, max(move_t - 0.24, 0.0))
			alpha = move_t / 0.08 if move_t < 0.08 else clamp(1.0 - (move_t - 0.08) / 0.92, 0.0, 1.0)
			size = base_size * lerp(1.0, 0.72, move_t)
			phase_rotation_t = 2.0 + move_t

		draw_line(prev_2, prev_1, Color(0.45, 0.0, 0.03, alpha * 0.24), size * 2.2)
		draw_line(prev_1, pos, Color(1.0, 0.12, 0.16, alpha * 0.72), size * 1.35)
		draw_circle(pos, size * 1.75, Color(0.45, 0.0, 0.02, alpha * 0.26))
		draw_circle(pos, size, Color(1.0, 0.03, 0.12, alpha * 0.98))
		if _spark_texture != null:
			_draw_glow(pos, size * 9.0, Color(1.0, 0.36, 0.42, alpha * 0.42), rotation + phase_rotation_t * rotation_speed)

	for impact in _impacts:
		var impact_time: float = impact["time"]
		var impact_t: float = (_elapsed - impact_time) / 0.18
		if impact_t < 0.0 or impact_t > 1.0:
			continue

		var radius: float = lerp(float(impact["radius"]) * 0.55, float(impact["radius"]) * 2.4, impact_t)
		var alpha: float = 1.0 - impact_t
		var impact_position: Vector2 = impact["position"]
		draw_circle(impact_position, radius * 1.12, Color(0.42, 0.0, 0.02, alpha * 0.24))
		draw_circle(impact_position, radius * 0.68, Color(1.0, 0.08, 0.14, alpha * 0.56))
		if _spark_texture != null:
			_draw_glow(impact_position, radius * 3.3, Color(1.0, 0.42, 0.48, alpha * 0.58), impact_t * 2.0)


func _build_projectiles() -> void:
	for i in range(projectile_count):
		var hit_time: float = first_hit_delay + hit_interval * float(i)
		var appear_time: float = hit_time - (grow_duration + hold_duration + flight_duration)
		var grow_end_time: float = appear_time + grow_duration
		var move_start_time: float = grow_end_time + hold_duration
		var start: Vector2 = source_position + Vector2(randf_range(-48.0, 48.0), randf_range(-38.0, 34.0))
		var finish: Vector2 = target_position + Vector2(randf_range(-16.0, 16.0), randf_range(-20.0, 20.0))
		var direction: Vector2 = (finish - start).normalized()
		var perpendicular: Vector2 = Vector2(-direction.y, direction.x)
		var duration: float = flight_duration + randf_range(-0.01, 0.01)
		var control: Vector2 = start.lerp(finish, 0.46) + perpendicular * randf_range(-65.0, 65.0) + direction * randf_range(-10.0, 26.0)
		var projectile := {
			"appear_time": appear_time,
			"grow_end_time": grow_end_time,
			"move_start_time": move_start_time,
			"hit_time": hit_time,
			"duration": duration,
			"start": start,
			"control": control,
			"finish": finish,
			"size": randf_range(7.0, 11.0),
			"rotation": randf() * TAU,
			"rotation_speed": randf_range(-6.0, 6.0),
			"hit_spawned": false
		}
		_projectiles.append(projectile)
		_max_end_time = max(_max_end_time, hit_time)


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
