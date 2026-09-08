extends Node
## No input player exists until the player explicitly holds PTT/test.
const PCM=preload("res://scripts/voice_pcm.gd")
var hardware_allowed:=false
var active:=false
var error:=""
var source: AudioStreamPlayer
var capture: AudioEffectCapture
var converter:=PCM.new()
var input_device:="Default"
var _bus_name: StringName
var _last_frames:=0
var _quiet_capture_age:=0.0

func start() -> bool:
	if active: return true
	error=""
	if not hardware_allowed:
		error="El micrófono está desactivado en esta prueba.";return false
	if not bool(ProjectSettings.get_setting("audio/driver/enable_input",false)):
		error="La entrada de voz no está disponible en esta versión.";return false
	var devices:=AudioServer.get_input_device_list()
	if devices.is_empty() or (input_device!="Default" and not devices.has(input_device)):
		error="No se encontró el micrófono seleccionado.";return false
	AudioServer.input_device=input_device
	_bus_name=StringName("VoiceCapture_"+str(get_instance_id()))
	AudioServer.add_bus()
	var index:=AudioServer.bus_count-1
	AudioServer.set_bus_name(index,_bus_name)
	AudioServer.set_bus_send(index,&"Master")
	capture=AudioEffectCapture.new();capture.buffer_length=.24
	AudioServer.add_bus_effect(index,capture)
	# Mute is applied after capture; local microphone never feeds speakers.
	AudioServer.set_bus_mute(index,true)
	source=AudioStreamPlayer.new();source.bus=_bus_name
	source.stream=AudioStreamMicrophone.new();add_child(source)
	converter.reset();_quiet_capture_age=0.0;_last_frames=0
	source.play()
	active=source.playing
	if not active: error="No se pudo abrir el micrófono. Revisá el dispositivo y sus permisos.";stop()
	return active

func read_frames(dt: float) -> Array[PackedFloat32Array]:
	var frames: Array[PackedFloat32Array]=[]
	if not active: return frames
	if capture.get_discarded_frames()>0:
		error="La captura se demoró demasiado. Volvé a presionar para hablar.";stop();return frames
	var count:=capture.get_frames_available()
	if count>0:
		_quiet_capture_age=0.0
		if not converter.append_stereo(capture.get_buffer(count),int(AudioServer.get_mix_rate())):
			error=converter.error;stop();return frames
	else: _quiet_capture_age+=dt
	if _quiet_capture_age>1.0:
		error="El micrófono no entregó audio. Revisá el dispositivo y sus permisos.";stop();return frames
	for index: int in range(12):
		var frame: PackedFloat32Array=converter.pop_frame()
		if frame.is_empty(): break
		frames.append(frame)
	return frames

func finish_frames() -> Array[PackedFloat32Array]:
	var frames: Array[PackedFloat32Array]=read_frames(0.0)
	if active: frames.append_array(converter.finish_frames())
	return frames

func stop() -> void:
	active=false
	if is_instance_valid(source): source.stop();source.stream=null;source.queue_free()
	source=null;capture=null;converter.reset()
	var index:=AudioServer.get_bus_index(_bus_name)
	if index>0: AudioServer.remove_bus(index)
	_bus_name=&""

func _exit_tree() -> void: stop()
