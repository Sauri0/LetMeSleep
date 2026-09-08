extends Node3D
## Pitch changes tone at fixed sample rate. Mouth follows wet PCM's output clock.
const PCM=preload("res://scripts/voice_pcm.gd")
const RATE:=48000
var output: AudioStreamPlayer3D
var dry: AudioStreamPlayer
var wet_capture: AudioEffectCapture
var filter: AudioEffectLowPassFilter
var _output_buffer: AudioStreamGeneratorPlayback
var _dry_buffer: AudioStreamGeneratorPlayback
var _capacity:=0
var _dry_capacity:=0
var _last_clock:=-INF
var _input_ended:=false
var _tail_remaining:=0
var _dry_end_clock:=INF
var _dsp_drained:=false
var _drain_output_end:=0.0
var _bus_names: Array[StringName]=[]
var _envelopes: Array[Dictionary]=[]
var _permission: Dictionary={}
var _wet_pending:=PackedVector2Array()
var _output_latency:=0.0
var _gain:=0.0
var _active:=false
var _configured:=false
var error:=""
var last_level:=0.0
var pushed_samples:=0
var dry_samples:=0
var max_queue_samples:=0

func _bus(suffix: String, muted: bool=false) -> int:
	var name:=StringName("Voice_"+str(get_instance_id())+suffix)
	AudioServer.add_bus();var index:=AudioServer.bus_count-1
	AudioServer.set_bus_name(index,name);AudioServer.set_bus_send(index,&"Master")
	AudioServer.set_bus_mute(index,muted);_bus_names.append(name)
	return index

func _stream() -> AudioStreamGenerator:
	var stream:=AudioStreamGenerator.new();stream.mix_rate=RATE;stream.buffer_length=.16
	return stream

func configure(permission: Dictionary) -> void:
	_permission=permission.duplicate()
	if _configured: return
	_configured=true
	error="";_last_clock=-INF;_input_ended=false;_tail_remaining=0;_dry_end_clock=INF;_dsp_drained=false;_drain_output_end=0.0
	if AudioServer.get_bus_index(&"VoiceMaster")<0:
		AudioServer.add_bus();var master:=AudioServer.bus_count-1
		AudioServer.set_bus_name(master,&"VoiceMaster");AudioServer.set_bus_send(master,&"Master")
		var limiter:=AudioEffectHardLimiter.new();limiter.ceiling_db=-1.0
		AudioServer.add_bus_effect(master,limiter)
	var index:=_bus("_Output")
	filter=AudioEffectLowPassFilter.new();filter.cutoff_hz=float(permission.get("cutoff",12000.0))
	AudioServer.add_bus_effect(index,filter)
	AudioServer.set_bus_send(index,&"VoiceMaster")
	output=AudioStreamPlayer3D.new();output.stream=_stream();output.bus=_bus_names.back()
	output.attenuation_model=AudioStreamPlayer3D.ATTENUATION_DISABLED
	output.max_distance=0.0;output.pitch_scale=1.0;output.doppler_tracking=AudioStreamPlayer3D.DOPPLER_TRACKING_DISABLED
	output.volume_db=-80.0;add_child(output);output.play()
	if float(permission.get("pitch",1.0))>1.0:
		index=_bus("_Pitch",true)
		var pitch_effect:=AudioEffectPitchShift.new()
		pitch_effect.pitch_scale=1.55;pitch_effect.fft_size=AudioEffectPitchShift.FFT_SIZE_2048;pitch_effect.oversampling=4
		AudioServer.add_bus_effect(index,pitch_effect)
		wet_capture=AudioEffectCapture.new();wet_capture.buffer_length=.24
		AudioServer.add_bus_effect(index,wet_capture)
		dry=AudioStreamPlayer.new();dry.stream=_stream();dry.bus=_bus_names.back();dry.pitch_scale=1.0
		add_child(dry);dry.play();_dry_buffer=dry.get_stream_playback();_dry_capacity=_dry_buffer.get_frames_available()
	# Player3D begins on its physics tick. Do not request playback before then.
	await get_tree().physics_frame
	if not is_instance_valid(output) or not _configured: return
	_output_buffer=output.get_stream_playback();_capacity=_output_buffer.get_frames_available()
	_output_latency=AudioServer.get_output_latency()
	_active=true

func set_permission(permission: Dictionary) -> void: _permission=permission.duplicate()

func push(samples: PackedFloat32Array) -> bool:
	if samples.size()!=PCM.FRAME or not _configured or _input_ended: return false
	var stereo:=PackedVector2Array();stereo.resize(samples.size())
	for index: int in range(samples.size()):
		if not is_finite(samples[index]): error="La voz recibida no es válida.";return false
		var value:=clampf(samples[index],-.98,.98);stereo[index]=Vector2(value,value)
	dry_samples+=samples.size()
	if is_instance_valid(dry):
		if _dry_buffer==null or not _dry_buffer.can_push_buffer(stereo.size()): error="La reproducción de voz se demoró.";return false
		return _dry_buffer.push_buffer(stereo)
	_wet_pending.append_array(stereo)
	return _wet_pending.size()<=PCM.MAX_QUEUED

func advance(dt: float) -> float:
	if not _active: return 0.0
	_gain=lerpf(_gain,float(_permission.get("gain",0.0)),1.0-exp(-dt/.06))
	output.volume_db=linear_to_db(maxf(.0001,_gain))
	filter.cutoff_hz=lerpf(filter.cutoff_hz,float(_permission.get("cutoff",12000.0)),1.0-exp(-dt/.06))
	if wet_capture!=null and not _dsp_drained:
		if wet_capture.get_discarded_frames()>0: error="La reproducción de voz se demoró.";return 0.0
		if _input_ended and _tail_remaining>0:
			var count:=mini(_tail_remaining,_dry_buffer.get_frames_available())
			if count>0:
				var silence:=PackedVector2Array();silence.resize(count)
				AudioServer.lock()
				var queued:=_dry_capacity-_dry_buffer.get_frames_available()
				var end:=dry.get_playback_position()+float(queued+count)/RATE
				var pushed:=_dry_buffer.push_buffer(silence)
				AudioServer.unlock()
				if not pushed: error="No se pudo cerrar la frase de voz.";return 0.0
				_tail_remaining-=count
				if _tail_remaining==0: _dry_end_clock=end
		# Capture and generator cursor refer to the same completed mixer block.
		AudioServer.lock()
		var available:=wet_capture.get_frames_available()
		var wet:=wet_capture.get_buffer(available) if available>0 else PackedVector2Array()
		var dry_clock:=dry.get_playback_position()
		AudioServer.unlock()
		_wet_pending.append_array(wet)
		if _input_ended and _tail_remaining==0 and dry_clock>=_dry_end_clock:
			_dsp_drained=true;dry.stop()
	if _wet_pending.size()>PCM.MAX_QUEUED: error="La cola de voz se demoró.";return 0.0
	if not _wet_pending.is_empty():
		var levels: Array[Dictionary]=[]
		for offset: int in range(0,_wet_pending.size(),PCM.FRAME):
			var count:=mini(PCM.FRAME,_wet_pending.size()-offset)
			var mono:=PackedFloat32Array();mono.resize(count)
			for index: int in range(count): mono[index]=(_wet_pending[offset+index].x+_wet_pending[offset+index].y)*.5
			levels.append({"offset":offset,"count":count,"level":PCM.mouth_level(mono)})
		AudioServer.lock()
		var can_push:=_output_buffer.can_push_buffer(_wet_pending.size())
		var queued:=_capacity-_output_buffer.get_frames_available()
		var start:=output.get_playback_position()+float(queued)/RATE
		var pushed:=_output_buffer.push_buffer(_wet_pending) if can_push else false
		AudioServer.unlock()
		if not pushed: error="La cola de salida de voz está llena.";return 0.0
		max_queue_samples=maxi(max_queue_samples,queued+_wet_pending.size())
		for entry: Dictionary in levels:
			_envelopes.append({"start":start+float(entry.offset)/RATE,"end":start+float(int(entry.offset)+int(entry.count))/RATE,"level":entry.level})
		_drain_output_end=start+float(_wet_pending.size())/RATE
		pushed_samples+=_wet_pending.size();_wet_pending.clear()
	var clock:=maxf(_last_clock,output.get_playback_position()+AudioServer.get_time_since_last_mix()-_output_latency)
	_last_clock=clock
	last_level=0.0
	while not _envelopes.is_empty() and float(_envelopes[0].end)<=clock: _envelopes.pop_front()
	if not _envelopes.is_empty() and float(_envelopes[0].start)<=clock: last_level=float(_envelopes[0].level)
	if _envelopes.size()>48: error="El reloj de voz se demoró.";return 0.0
	return last_level

func finish_input() -> void:
	if _input_ended: return
	_input_ended=true
	# Explicit zero padding flushes FFT history after the final decoded sample.
	# The drain waits for real sample consumption, not a fixed release timeout.
	if is_instance_valid(dry): _tail_remaining=3*2048
	else: _dsp_drained=true

func finished() -> bool:
	return _active and _input_ended and _dsp_drained and _wet_pending.is_empty() and _last_clock>=_drain_output_end

func shutdown() -> void:
	_active=false;_configured=false;last_level=0.0;_wet_pending.clear();_envelopes.clear()
	if is_instance_valid(output): output.stop();output.stream=null;output.queue_free()
	if is_instance_valid(dry): dry.stop();dry.stream=null;dry.queue_free()
	output=null;dry=null;wet_capture=null;_output_buffer=null;_dry_buffer=null
	for name: StringName in _bus_names:
		var index:=AudioServer.get_bus_index(name)
		if index>0: AudioServer.remove_bus(index)
	_bus_names.clear()

func _exit_tree() -> void: shutdown()
