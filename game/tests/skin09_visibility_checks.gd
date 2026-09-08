extends SceneTree
## Compare actual imported selection with the previous unconditional setter.
class CountedSkin:
	extends "res://assets/art/characters/shared/character_skin.gd"
	var visibility_passes := 0
	func _update_visibility() -> void:
		visibility_passes += 1
		super._update_visibility()

class ReferenceSkin:
	extends CountedSkin
	func set_first_person(value: bool) -> void:
		first_person = value
		_update_visibility()

var checks := 0
var failures: Array[String] = []
var records: Array[Dictionary] = []
var output := ""

func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--output="): output=arg.trim_prefix("--output=")
	_run.call_deferred()

func _check(condition: bool, label: String) -> void:
	checks += 1
	if not condition: failures.append(label)

func _visible(skin: CountedSkin) -> Dictionary:
	var result: Dictionary={}
	for mesh: MeshInstance3D in skin.meshes: result[str(mesh.name)]=mesh.visible
	return result

func _run() -> void:
	for role: String in ["human","mosquito"]:
		var current:=CountedSkin.new()
		var reference:=ReferenceSkin.new()
		root.add_child(current)
		root.add_child(reference)
		# Also exercise selecting view mode before setup: initial appearance must
		# establish visibility even when the first setter call was a no-op.
		current.set_first_person(false)
		reference.set_first_person(false)
		current.setup(role)
		reference.setup(role)
		_check(not current.meshes.is_empty(),role+" imported meshes")
		_check(_visible(current)==_visible(reference),role+" initial selection")
		for accessory: int in range(4):
			for hair: int in range(3):
				var appearance: Dictionary={"accessory":accessory,"hair":hair,"outfit":accessory%3,"eyes":hair,"brows":(hair+1)%3,"mouth":(hair+2)%3,"mustache":accessory%3,"beard":hair,"footwear":hair}
				for view: bool in [false,true,true,false]:
					current.set_first_person(view)
					current.set_appearance(appearance)
					reference.set_first_person(view)
					reference.set_appearance(appearance)
					var before: int=current.visibility_passes
					for repeat: int in range(8):
						current.set_first_person(view)
						reference.set_first_person(view)
						_check(_visible(current)==_visible(reference),"%s A%d H%d view%s repeat%d"%[role,accessory,hair,view,repeat])
					_check(current.visibility_passes==before,role+" repeated view avoids selection pass")
					if role=="human" and view:
						for mesh: MeshInstance3D in current.meshes:
							var category: String=str(mesh.name).split("_")[1]
							if category in ["head","eyes","brows","mouth","hair","accessory","mustache","beard"]:
								_check(not mesh.visible,"first person hides "+str(mesh.name))
		_check(current.visibility_passes<reference.visibility_passes,role+" fewer visibility passes")
		records.append({"role":role,"meshes":current.meshes.size(),"current_passes":current.visibility_passes,"reference_passes":reference.visibility_passes})
		current.queue_free()
		reference.queue_free()
		await process_frame
	var report: Dictionary={"checks":checks,"failures":failures,"records":records,"scope":"Imported mesh selection equivalence and redundant setter work; no gameplay FPS or skin deformation claim","skin_sha256":FileAccess.get_sha256("res://assets/art/characters/shared/character_skin.gd")}
	if not output.is_empty():
		var file:=FileAccess.open(output,FileAccess.WRITE)
		file.store_string(JSON.stringify(report,"\t"))
		file.close()
	print("SKIN09_VISIBILITY "+JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)
