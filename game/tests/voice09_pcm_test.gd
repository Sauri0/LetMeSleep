extends SceneTree
const PCM=preload("res://scripts/voice_pcm.gd")
const Capture=preload("res://scripts/voice_capture.gd")
var checks:=0
var failures: Array[String]=[]
func check(value: bool, label: String) -> void:
	checks+=1
	if not value: failures.append(label)
func converted(rate: int, chunk_size: int) -> PackedFloat32Array:
	var converter:=PCM.new();var result:=PackedFloat32Array()
	for offset: int in range(0,rate,chunk_size):
		var chunk:=PackedFloat32Array();chunk.resize(mini(chunk_size,rate-offset))
		for index: int in range(chunk.size()): chunk[index]=.25*sin(TAU*440*float(offset+index)/rate)
		check(converter.append_mono(chunk,rate),"append bounded source callback %d/%d"%[rate,chunk_size])
		while true:
			var frame: PackedFloat32Array=converter.pop_frame()
			if frame.is_empty(): break
			check(frame.size()==960,"Opus frame is exactly 20ms")
			result.append_array(frame)
	return result
func _initialize() -> void:
	for rate: int in [16000,44100,48000]:
		var reference:=converted(rate,127)
		check(reference.size()==49*960,"one second retains sample duration at "+str(rate))
		for chunk: int in [37,512,1024]:
			var candidate:=converted(rate,chunk)
			var delta:=0.0
			if candidate.size()==reference.size():
				for index: int in range(candidate.size()): delta=maxf(delta,absf(candidate[index]-reference[index]))
			check(candidate.size()==reference.size() and delta<.00001,"callback partition invariance %d/%d"%[rate,chunk])
	var converter:=PCM.new()
	check(not converter.append_mono(PackedFloat32Array([NAN]),48000),"reject nonfinite input")
	converter.reset();check(converter.append_mono(PackedFloat32Array([.1,.2]),48000),"valid input")
	check(not converter.append_mono(PackedFloat32Array([.1,.2]),44100),"device rate switch requires reset")
	converter.reset()
	var dc:=PackedFloat32Array();dc.resize(960);dc.fill(.4)
	var tail:=PackedFloat32Array()
	for index: int in range(50):
		check(converter.append_mono(dc,48000),"bounded DC input")
		var frame: PackedFloat32Array=converter.pop_frame()
		if not frame.is_empty(): tail=frame
	check(PCM.rms(tail)<.00001,"DC settles to silence")
	check(PCM.mouth_level(tail)==0.0,"actual silence keeps mouth closed")
	converter.reset()
	var marker:=PackedFloat32Array();marker.resize(480)
	for index: int in range(marker.size()): marker[index]=.4*sin(TAU*880*float(index)/48000)
	check(converter.append_mono(marker,48000),"append final half-frame marker")
	var ending: Array[PackedFloat32Array]=converter.finish_frames()
	check(ending.size()==1 and ending[0].size()==960,"release pads final codec frame")
	if ending.size()==1:
		check(PCM.rms(ending[0].slice(0,480))>.2,"final speech remains audible before padding")
		check(PCM.rms(ending[0].slice(480))==0.0,"only missing frame samples are zero padding")
	check(converter.finish_frames().is_empty(),"finishing twice cannot repeat final speech")
	check(converter.append_mono(marker,48000),"append speech before hard reset")
	converter.reset();check(converter.finish_frames().is_empty(),"hard reset discards pending microphone speech")
	var capture:=Capture.new();root.add_child(capture)
	check(not capture.start() and not capture.active and capture.source==null,"hardware guard rejects capture before creating any microphone")
	capture.free()
	for failure: String in failures: print("FAIL "+failure)
	print("voice09_pcm checks=%d failures=%d"%[checks,failures.size()])
	quit(0 if failures.is_empty() else 1)
