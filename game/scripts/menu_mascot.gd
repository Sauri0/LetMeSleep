extends Control
## The shipped mosquito in an isolated, non-interactive home vignette.
## AvatarPreview's editor camera and source remain untouched.
const Preview = preload("res://scripts/avatar_preview.gd")
var preview: SubViewportContainer
var appearance: Dictionary = {}
var active := true

func _ready() -> void:
	custom_minimum_size = Vector2(280,230)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	preview = Preview.new()
	preview.set_avatar("mosquito",appearance)
	preview.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(preview)
	preview.custom_minimum_size = Vector2.ZERO
	preview.mouse_filter = Control.MOUSE_FILTER_IGNORE
	preview.viewport.transparent_bg = true
	preview.focus_mode = Control.FOCUS_NONE
	preview.set_meta("original_focus_mode",Control.FOCUS_NONE)
	preview.reset_view()
	# Full silhouette; this preset belongs only to the home instance.
	preview.focus_distance = 2.8
	preview.focus_target = Vector3(0,0.92,0)
	preview.orbit_yaw = -0.52
	preview._update_camera()
	for child: Node in preview.studio.get_children():
		if child is WorldEnvironment:
			child.environment.background_mode = Environment.BG_CLEAR_COLOR
		elif child is MeshInstance3D and child.mesh is CylinderMesh:
			child.hide()
	set_active(active)

func set_appearance(value: Dictionary) -> void:
	if appearance==value: return
	appearance = value.duplicate(true)
	if is_instance_valid(preview): preview.set_avatar("mosquito",appearance)

func set_active(value: bool) -> void:
	active = value
	if not is_instance_valid(preview): return
	preview.set_process(active)
	preview.viewport.render_target_update_mode = SubViewport.UPDATE_WHEN_VISIBLE if active else SubViewport.UPDATE_DISABLED
