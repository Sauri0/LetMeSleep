extends SceneTree
const Preview=preload("res://assets/art/samples07/sample_preview.gd")
func _initialize()->void:_run.call_deferred()
func _run()->void:
	root.size=Vector2i(1600,900)
	var row:=HBoxContainer.new();row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);root.add_child(row)
	var previews:Array[SubViewportContainer]=[]
	for option:int in range(3):
		var cell:=VBoxContainer.new();cell.size_flags_horizontal=Control.SIZE_EXPAND_FILL;row.add_child(cell)
		var label:=Label.new();label.text=["Shadow disabled","Default spot shadow","Bias .15 / normal 2.0"][option];cell.add_child(label)
		var preview:=Preview.new();preview.size_flags_vertical=Control.SIZE_EXPAND_FILL;cell.add_child(preview);preview.set_sample("A","human",1);preview.set_face_focus();preview.set_expression("neutral");previews.append(preview)
		for node:Node in preview.studio.get_children():
			if node is SpotLight3D:
				if option==0:node.shadow_enabled=false
				if option==2:node.shadow_bias=.15;node.shadow_normal_bias=2.0
	for frame:int in range(6):
		for preview:SubViewportContainer in previews:preview.step(1.0/30.0)
		await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://../work/samples07-shadow-diagnostic.png"))
	row.queue_free();await process_frame;await process_frame;quit()
