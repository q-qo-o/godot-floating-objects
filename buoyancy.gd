extends RigidBody3D
##
## 基于体素采样的浮力脚本
## ============================================================
## 算法原理：
##   1. 读取本节点上的 CollisionShape3D（仅支持 BoxShape3D）。
##   2. 将 Box 划分为 grid_resolution³ 个均匀小立方体（"体素"）。
##   3. 对每个体素，计算其在水面以下的部分高度，进而得到
##      该体素的浸没体积。
##   4. 在每个浸没体素的中心处施加 F = ρ·g·V 的向上浮力。
##   5. 浮力累加后近似等于物体所受的总浮力与力矩。
##
## 局限性：
##   - 仅支持 BoxShape3D。
##   - 离散化采样会在物体倾斜部分浸没时产生力矩跳变
##     （只剩少量体素时浮心位置突变）。如需更稳定的物理表现，
##     使用基于网格几何的 buoyancy_mesh.gd。
##
## 使用要求：
##   - 节点本身必须是 RigidBody3D。
##   - 必须有一个 CollisionShape3D 子节点且形状为 BoxShape3D。
##

# ============================================================
# 导出参数
# ============================================================

## 水面节点。其全局 Y 坐标作为水面高度。
## 若未指定，会在 _ready 时尝试查找名为 "WaterSurface" 的节点；
## 若仍找不到，水面高度回退到 _FALLBACK_WATER_LEVEL。
@export var water_surface_node: Node3D

## 体素网格分辨率。每条边被分为 grid_resolution 段，
## 总体素数 = grid_resolution³。值越大越精确但越慢。
## 默认 6 即 216 个体素。
@export var grid_resolution := 6

## 流体密度，水为 1000 kg/m³
@export var fluid_density := 1000.0

## 基础线性阻尼（按浸没比例缩放）
@export var water_drag := 0.6

## 基础角阻尼（按浸没比例缩放）
@export var water_angular_drag := 0.6


# ============================================================
# 内部常量与状态
# ============================================================

## 找不到水面节点时的回退高度（一般不会用到）
const _FALLBACK_WATER_LEVEL := 0.0

## 重力加速度（从 ProjectSettings 读取）
var _gravity: float

## 当前浸没比例 [0, 1]，供外部脚本读取
var submerged_ratio: float = 0.0

## 当前垂直速度，供外部脚本读取
var vertical_velocity: float = 0.0


# ============================================================
# 初始化
# ============================================================

func _ready() -> void:
	# 加入 floating_bodies 组，便于 water_surface.gd 检索做尾迹
	add_to_group("floating_bodies")
	_gravity = ProjectSettings.get_setting("physics/3d/default_gravity")

	# 若未指定水面节点，从当前场景查找名为 "WaterSurface" 的节点
	if water_surface_node == null:
		water_surface_node = get_tree().current_scene.get_node_or_null("WaterSurface")
	if water_surface_node == null:
		push_warning("buoyancy.gd: 未找到水面节点，将使用回退水面高度 %f" % _FALLBACK_WATER_LEVEL)


# ============================================================
# 物理积分（每物理帧调用）
# ============================================================

func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	var water_height := _get_water_height()

	# 找到碰撞形状节点（先按常用名查找，否则遍历子节点）
	var shape_node := get_node_or_null("CollisionShape3D") as CollisionShape3D
	if shape_node == null:
		shape_node = _find_collision_shape()
	if shape_node == null:
		return

	# 当前实现仅支持 Box 形状
	var shape := shape_node.shape
	if not (shape is BoxShape3D):
		return

	var box := shape as BoxShape3D
	var size := box.size               # Box 的完整边长
	var half := size * 0.5             # 半边长，用于计算体素中心
	var voxel := size / float(grid_resolution)  # 单个体素的边长
	var voxel_area := voxel.x * voxel.z         # 体素水平截面积

	var total_volume := size.x * size.y * size.z  # Box 总体积
	var submerged_volume := 0.0                   # 累加浸没体积

	# 三重循环遍历所有体素
	for ix in range(grid_resolution):
		for iy in range(grid_resolution):
			for iz in range(grid_resolution):
				# 计算体素中心的局部坐标
				# (float(ix) + 0.5) * voxel.x - half.x 把第 ix 个体素中心
				# 放在 Box 内部对应位置
				var local_center := Vector3(
					(float(ix) + 0.5) * voxel.x - half.x,
					(float(iy) + 0.5) * voxel.y - half.y,
					(float(iz) + 0.5) * voxel.z - half.z
				)

				# 转换到世界坐标，判断是否在水面以下
				var world_center := to_global(local_center)
				var voxel_bottom := world_center.y - voxel.y * 0.5
				var submerged_height := water_height - voxel_bottom

				# 体素完全在水面上：跳过
				if submerged_height <= 0.0:
					continue

				# 限制最大浸没高度为体素高度（即完全浸没）
				submerged_height = min(submerged_height, voxel.y)
				# 这个体素的浸没体积 = 水平截面积 × 浸没高度
				var vol := voxel_area * submerged_height
				submerged_volume += vol

				# 在体素中心施加向上的浮力
				# F = ρ · g · V_浸没
				var force := Vector3.UP * vol * fluid_density * _gravity
				state.apply_force(force, local_center)

	# 阻尼与状态更新
	if submerged_volume > 0.0:
		submerged_ratio = submerged_volume / total_volume
		# 简化阻尼模型：按浸没比例直接缩放速度
		state.linear_velocity *= 1.0 - water_drag * state.step * submerged_ratio
		state.angular_velocity *= 1.0 - water_angular_drag * state.step * submerged_ratio
	else:
		submerged_ratio = 0.0

	# 更新公开状态供外部脚本读取
	vertical_velocity = state.linear_velocity.y
	set_meta("submerged_ratio", submerged_ratio)
	set_meta("vertical_velocity", vertical_velocity)


# ============================================================
# 工具函数
# ============================================================

## 获取当前水面高度（优先使用 water_surface_node，否则回退）
func _get_water_height() -> float:
	if water_surface_node != null:
		return water_surface_node.global_position.y
	return _FALLBACK_WATER_LEVEL


## 若直接 get_node_or_null("CollisionShape3D") 失败，
## 退而求其次：遍历直接子节点查找第一个 CollisionShape3D
func _find_collision_shape() -> CollisionShape3D:
	for child in get_children():
		if child is CollisionShape3D:
			return child
	return null
