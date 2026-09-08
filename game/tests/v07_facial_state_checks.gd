extends SceneTree
const Facial=preload("res://assets/art/characters/shared/facial_expression.gd")
var checks:=0
var failures:=0
func check(ok:bool,label:String)->void:
	checks+=1
	if not ok:
		failures+=1
		print("FACIAL_STATE07 FAIL ",label)
func _initialize()->void:
	for species:String in ["human","mosquito"]:
		for face:int in range(3):
			for context:String in ["sleepy","alert","effort","impact"]:
				var control:=Facial.new(.37)
				var result:Dictionary={}
				for frame:int in range(90):
					result=control.advance({"state":"human" if species=="human" else "flying","preview_only":true,"facial_preview":context},species,1.0/60.0,face)
				for channel:String in Facial.CHANNELS:
					check(is_finite(float(result[channel])) and absf(float(result[channel]))<=1.0,species+str(face)+context+channel+" finite range")
				if context=="impact":check(result.MouthOpen>.4 and result.BrowUp>.35,species+str(face)+" visible mouth and brow reaction")
	var live:=Facial.new(.2)
	var no_override:Dictionary={}
	for frame:int in range(120):no_override=live.advance({"state":"human","facial_preview":"impact"},"human",1.0/60.0)
	check(no_override.MouthOpen<.1,"preview override ignored in gameplay")
	var reaction:=Facial.new(.2)
	var peak:=0.0
	var after:Dictionary={}
	for frame:int in range(180):
		after=reaction.advance({"state":"human","bitten":frame>=20},"human",1.0/60.0)
		peak=maxf(peak,float(after.MouthOpen))
	check(peak>.3 and after.MouthOpen<.2,"observed bite reacts once then settles")
	var a:=Facial.new(.04)
	var b:=Facial.new(.73)
	var differing_blinks:=0
	for frame:int in range(600):
		var va:Dictionary=a.advance({"state":"flying"},"mosquito",1.0/60.0)
		var vb:Dictionary=b.advance({"state":"flying"},"mosquito",1.0/60.0)
		if absf(float(va.BlinkL)-float(vb.BlinkL))>.35:differing_blinks+=1
	check(differing_blinks>15,"actors do not blink in lockstep")
	print("FACIAL_STATE07_RESULT checks=%d failures=%d" % [checks,failures])
	quit(failures)
