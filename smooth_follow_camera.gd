extends Camera3D
##
## 平滑跟随相机（第三人称，不随物体翻滚）
##
## 只跟随目标的位置，不继承其旋转，避免眩晕。
## 相机始终保持稳定的俯视角度看向目标。
##

## 要跟随的目标节点
@export var target: Node3D

## 相机相对于目标的偏移（目标局部坐标系）
## 典型值：(0, 3.5, 6) — 正后方 6 米，上方 3.5 米
@export var offset := Vector3(0.0, 3.5, 6.0)

## 位置平滑系数（0=不动，1=瞬间到位，0.1~0.3 比较柔和）
@export var smoothness := 0.2

## 是否始终看向目标中心点
@export var always_look_at_target := true

## 目标看向点的垂直偏移（默认看向目标中心偏上一点）
@export var look_at_height_offset := 0.5


func _physics_process(_delta: float) -> void:
	if target == null:
		return

	# 计算目标在世界坐标系中的理想相机位置
	# 注意：不使用 target.global_transform * offset，因为那会继承目标的旋转
	# 只取目标位置，用固定的偏移方向
	var target_pos := target.global_position
	var desired_pos := target_pos + Vector3(offset.x, offset.y, offset.z)

	# 平滑移动到目标位置
	global_position = global_position.lerp(desired_pos, smoothness)

	if always_look_at_target:
		# 看向目标中心（加一点高度偏移，不盯着脚）
		var look_at_pos := target_pos + Vector3(0.0, look_at_height_offset, 0.0)
		look_at(look_at_pos, Vector3.UP)
