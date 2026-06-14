extends MeshInstance3D

const MAX_RIPPLES := 48
const RIPPLE_DURATION := 4.0
const WAKE_INTERVAL := 0.15

var _ripples: Array[Vector4] = []
var _material: ShaderMaterial
var _time_offset: float = 0.0
var _floater_prev_pos: Dictionary = {}
var _floater_wake_timer: Dictionary = {}

@onready var area: Area3D = $Area3D


func _ready() -> void:
	_material = material_override
	if _material == null:
		_material = get_surface_override_material(0)
	if _material == null:
		_material = mesh.surface_get_material(0)
	_time_offset = Time.get_ticks_msec() / 1000.0
	area.body_entered.connect(_on_body_entered)


func _on_body_entered(body: Node3D) -> void:
	var speed := 0.0
	if body is RigidBody3D:
		speed = body.linear_velocity.length()
	elif body is CharacterBody3D:
		speed = body.velocity.length()
	var intensity := clampf(speed * 0.12, 0.15, 3.0)
	_add_ripple(body.global_position, intensity)


func _add_ripple(world_pos: Vector3, intensity: float = 1.0) -> void:
	var now := Time.get_ticks_msec() / 1000.0 - _time_offset
	var new_ripple := Vector4(world_pos.x, world_pos.z, now, intensity)

	if _ripples.size() < MAX_RIPPLES:
		_ripples.append(new_ripple)
	else:
		var oldest_idx := 0
		var oldest_time := _ripples[0].z
		for i in range(1, _ripples.size()):
			if _ripples[i].z < oldest_time:
				oldest_time = _ripples[i].z
				oldest_idx = i
		_ripples[oldest_idx] = new_ripple


func _process(_delta: float) -> void:
	var now := Time.get_ticks_msec() / 1000.0 - _time_offset
	var active: Array[Vector4] = []
	var valid: Array[Vector4] = []

	for r in _ripples:
		var elapsed := now - r.z
		if elapsed < 0.0:
			continue
		var t := elapsed / RIPPLE_DURATION
		var intensity: float = r.w * max(0.0, 1.0 - t * t)
		if intensity > 0.005:
			valid.append(r)
			active.append(Vector4(r.x, r.y, elapsed, intensity))

	_ripples = valid

	while active.size() < MAX_RIPPLES:
		active.append(Vector4.ZERO)

	_material.set_shader_parameter("ripple_data", PackedVector4Array(active))

	# --- 漂浮物体移动尾迹 ---
	for body in get_tree().get_nodes_in_group("floating_bodies"):
		var sr: float = body.get_meta("submerged_ratio", 0.0)
		if sr < 0.05:
			_floater_prev_pos.erase(body)
			_floater_wake_timer.erase(body)
			continue

		var vel := Vector3.ZERO
		if body is RigidBody3D:
			vel = body.linear_velocity
		elif body is CharacterBody3D:
			vel = body.velocity

		var horizontal_speed := Vector3(vel.x, 0.0, vel.z).length()
		if horizontal_speed < 0.1:
			_floater_prev_pos[body] = body.global_position
			continue

		var prev: Vector3 = _floater_prev_pos.get(body, body.global_position)
		var dist := prev.distance_to(body.global_position)
		var timer: float = _floater_wake_timer.get(body, 0.0)
		timer += _delta

		if dist > 0.03 and timer >= WAKE_INTERVAL:
			var intensity := clampf(horizontal_speed * 0.08, 0.08, 1.5)
			_add_ripple(body.global_position, intensity)
			_floater_wake_timer[body] = 0.0
		else:
			_floater_wake_timer[body] = timer

		_floater_prev_pos[body] = body.global_position
