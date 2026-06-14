extends Node
##
## 摄像机切换器
## 把所有需要切换的 Camera3D 加入 cameras 列表，
## 运行时按数字键 1/2/3... 或 Tab 在它们之间切换。
##

## 要切换的相机列表（在编辑器中按顺序拖入）
@export var cameras: Array[Camera3D] = []

## 是否启用 Tab 键循环切换
@export var enable_tab_cycle := true

var _current_index := 0


func _ready() -> void:
	# 启动时切到列表中的第一个相机
	if cameras.size() > 0:
		_switch_to(0)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		var key_event := event as InputEventKey

		# 数字键 1-9 直接切换到对应索引的相机
		if key_event.keycode >= KEY_1 and key_event.keycode <= KEY_9:
			var idx := key_event.keycode - KEY_1
			if idx < cameras.size():
				_switch_to(idx)

		# Tab 循环切换
		elif enable_tab_cycle and key_event.keycode == KEY_TAB:
			_switch_to((_current_index + 1) % cameras.size())


func _switch_to(index: int) -> void:
	if index < 0 or index >= cameras.size():
		return
	var cam := cameras[index]
	if cam == null:
		return
	cam.make_current()
	_current_index = index
	print("切换到相机 %d: %s" % [index + 1, cam.name])
