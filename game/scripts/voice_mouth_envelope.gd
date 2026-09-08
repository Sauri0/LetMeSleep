class_name VoiceMouthEnvelope
extends RefCounted
## Local listener-side envelope. Its only signal is the level of audio actually
## played for that listener; no oscillator, microphone capture or network state.
var attack_seconds := .045
var release_seconds := .14
var stale_after := .16
var value := 0.0
var target := 0.0
var age := 0.0
var silence_age := 0.0

func configure(attack: float, release: float, stale: float) -> void:
	attack_seconds=clampf(attack,.005,1.0) if is_finite(attack) else .045
	release_seconds=clampf(release,.01,1.0) if is_finite(release) else .14
	stale_after=clampf(stale,.03,2.0) if is_finite(stale) else .16

func set_level(level: float) -> void:
	var next := clampf(level,0.0,1.0) if is_finite(level) else 0.0
	if next>0.0 or target>0.0: silence_age=0.0
	target=next
	age=0.0

func advance(dt: float) -> float:
	if not is_finite(dt) or dt<=0.0: return value
	age+=dt
	if age>=stale_after: target=0.0
	if target==0.0: silence_age+=dt
	else: silence_age=0.0
	var duration := attack_seconds if target>value else release_seconds
	value=lerpf(value,target,1.0-exp(-4.605170186*dt/duration))
	if target==0.0 and (value<.0001 or silence_age>=release_seconds): value=0.0
	value=clampf(value,0.0,1.0)
	return value

func clear() -> void:
	value=0.0
	target=0.0
	age=0.0
	silence_age=0.0

func active() -> bool:
	return value>0.0 or target>0.0
