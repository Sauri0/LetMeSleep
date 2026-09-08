extends SceneTree
const WorldScene=preload("res://scripts/world.gd")
const Prefs=preload("res://scripts/preferences.gd")
var checks:=0
var failures:=0
func _initialize()->void:run.call_deferred()
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:failures+=1;printerr("HOUSE_LIGHT_FAIL "+label)
func run()->void:
	var saved_quality:int=Prefs.video_shadows
	Prefs.video_shadows=2
	var world:Node3D=WorldScene.new();root.add_child(world);world.build();world.load_map("house")
	var lights:Array[Light3D]=[];var rooms:Dictionary={};var halls:=0;var stairs:=0
	for node:Node in world.map_root.find_children("*","Light3D",true,false):
		var light:=node as Light3D
		if light is DirectionalLight3D or light.light_energy<=0:continue
		lights.append(light)
		check(light is SpotLight3D,"all local house light cones remain bounded")
		check(not light.distance_fade_enabled,"camera movement never removes light or occlusion")
		check(light.shadow_enabled and bool(light.get_meta("authored_shadows",false)),"shadows are part of every authored local source")
		check(is_equal_approx(light.shadow_bias,.20) and is_equal_approx(light.shadow_normal_bias,2.0),"tested acne/contact bias")
		if light.has_meta("house_room"):
			rooms[str(light.get_meta("house_room"))]=true
			check(is_equal_approx(light.light_energy,.55),"room exposure matches selected skin/pajama comparison")
		if light.has_meta("house_hall"):halls+=1
		if light.has_meta("house_stair"):stairs+=1
	check(rooms.size()==world.map_data.rooms.size() and rooms.size()==16,"all sixteen room keys represented")
	check(halls==6 and stairs==1,"hallways and both-floor stair route retain local sources")
	check(lights.size()<=int(ProjectSettings.get_setting("rendering/limits/opengl/max_renderable_lights",32)),"entire scene stays within positional cap even before frustum culling")
	check(int(ProjectSettings.get_setting("rendering/limits/opengl/max_lights_per_object",8))==8,"per-object limit was not raised to mask geometry selection problems")
	for quality:int in [0,1,2]:
		Prefs.video_shadows=quality;world.apply_video_settings()
		check(root.positional_shadow_atlas_size==[0,1024,2048][quality],"atlas follows user's quality choice")
		check(not root.positional_shadow_atlas_16_bits,"32-bit depth preserved at all atlas qualities")
		for light:Light3D in lights:check(light.shadow_enabled==(quality>0),"quality toggles occlusion and restores authored shadows")
	Prefs.video_shadows=saved_quality
	world.queue_free();await process_frame;await process_frame
	print("HOUSE_LIGHT_TEST checks=",checks," failures=",failures)
	quit(0 if failures==0 else 1)
