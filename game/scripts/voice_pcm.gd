extends RefCounted
## Streaming capture conversion. Sample phase survives callback boundaries.
const RATE:=48000
const FRAME:=960
const MAX_QUEUED:=FRAME*12
var _source:=PackedFloat32Array()
var _output:=PackedFloat32Array()
var _cursor:=0.0
var _rate:=0
var _dc_x:=0.0
var _dc_y:=0.0
var error:=""

func reset() -> void:
	_source.clear();_output.clear();_cursor=0.0;_rate=0;_dc_x=0.0;_dc_y=0.0;error=""

func append_stereo(samples: PackedVector2Array, source_rate: int) -> bool:
	var mono:=PackedFloat32Array();mono.resize(samples.size())
	for index: int in range(samples.size()): mono[index]=(samples[index].x+samples[index].y)*.5
	return append_mono(mono,source_rate)

func append_mono(samples: PackedFloat32Array, source_rate: int) -> bool:
	if not error.is_empty(): return false
	if source_rate<8000 or source_rate>192000 or (_rate!=0 and _rate!=source_rate):
		error="La frecuencia de entrada cambió. Volvé a presionar para hablar.";return false
	if samples.size()>source_rate/4 or _source.size()+samples.size()>source_rate/3:
		error="La entrada de voz se demoró demasiado. Volvé a intentar.";return false
	for sample: float in samples:
		if not is_finite(sample): error="La entrada de audio no es válida.";return false
	_rate=source_rate;_source.append_array(samples)
	var step:=float(source_rate)/RATE
	while _cursor+1<_source.size():
		var index:=int(_cursor)
		var value:=lerpf(_source[index],_source[index+1],_cursor-index)
		# 48 kHz DC blocker, independent of the input device's sample rate.
		var filtered:=value-_dc_x+.995*_dc_y
		_dc_x=value;_dc_y=filtered
		_output.append(clampf(filtered,-.98,.98))
		_cursor+=step
		if _output.size()>MAX_QUEUED:
			error="La cola de captura está llena. Volvé a intentar.";_output.clear();return false
	var consumed:=mini(int(_cursor),maxi(0,_source.size()-1))
	_source=_source.slice(consumed);_cursor-=consumed
	return true

func pop_frame() -> PackedFloat32Array:
	if _output.size()<FRAME: return PackedFloat32Array()
	var frame:=_output.slice(0,FRAME);_output=_output.slice(FRAME)
	return frame

func finish_frames() -> Array[PackedFloat32Array]:
	var result: Array[PackedFloat32Array]=[]
	# Hold the final source sample for interpolation, then pad only the last
	# 20 ms codec frame. A normal release must retain sub-frame speech.
	if not _source.is_empty() and error.is_empty():
		append_mono(PackedFloat32Array([_source[-1]]),_rate)
	if error.is_empty() and not _output.is_empty():
		var padded: int=ceili(float(_output.size())/FRAME)*FRAME
		_output.resize(padded)
		while not _output.is_empty(): result.append(pop_frame())
	reset()
	return result

static func rms(samples: PackedFloat32Array) -> float:
	if samples.is_empty(): return 0.0
	var square:=0.0
	for sample: float in samples: square+=sample*sample
	return sqrt(square/samples.size())

static func mouth_level(samples: PackedFloat32Array) -> float:
	return clampf((linear_to_db(maxf(.000001,rms(samples)))+48.0)/36.0,0.0,1.0)
