"""Kinematic proposal only: two-link reach and stance drift, not authored animation."""
import json
import math
from pathlib import Path

A,B=.34,.32
CASES=[
    dict(name='walk_1mps',speed=1.,contacts=2.4,duty=.60,hip=.695,rise=.025,lift=.09,ramp=.22),
    dict(name='walk_1_55mps',speed=1.55,contacts=3.2,duty=.56,hip=.685,rise=.035,lift=.10,ramp=.22),
    dict(name='fast_trot_3_1mps',speed=3.1,contacts=4.,duty=.32,hip=.69,compression=.030,lift=.15,ramp=.18),
    dict(name='run_5mps',speed=5.,contacts=4.6,duty=.24,hip=.68,compression=.035,lift=.20,ramp=.16),
]


def smooth(t):
    t=max(0,min(1,t))
    return t*t*t*(10+t*(-15+6*t))


def hip_height(phase,c):
    if c['duty']>=.5:
        return c['hip']+c['rise']*math.sin(2*math.pi*phase)**2
    duration=2/c['contacts'];local=phase%.5
    stance=c['duty']*duration;flight=(.5-c['duty'])*duration
    if local<=c['duty']:
        w=local/c['duty'];a=9.81*flight*stance/(2*math.pi)
        return c['hip']-a*math.sin(math.pi*w)-(c['compression']-a)*math.sin(math.pi*w)**2
    t=(local-c['duty'])*duration
    return c['hip']+.5*9.81*t*(flight-t)


def foot(phase,c):
    q=phase%1;duration=2/c['contacts'];distance=c['speed']*duration
    span=distance*c['duty']
    if q<c['duty']:
        return span/2-distance*q,.12,True
    u=(q-c['duty'])/(1-c['duty']);k=distance*(1-c['duty'])
    # Quintic Hermite: endpoint horizontal velocity matches planted -speed,
    # endpoint acceleration is zero. Includes retraction, so reach checks
    # must examine the full swing rather than only the stance span.
    forward=-span/2-k*u+distance*smooth(u)
    height=.12+c['lift']*smooth(u/c['ramp'])*smooth((1-u)/c['ramp'])
    return forward,height,False


def inspect(c):
    rows=[];hip_values=[];max_drift=0.;forward=[]
    steps=2400;duration=2/c['contacts']
    for i in range(steps+1):
        phase=i/steps;hip=hip_height(phase,c);hip_values.append(hip)
        for offset in [0,.5]:
            x,y,stance=foot(phase+offset,c);forward.append(x)
            reach=math.hypot(x,hip-y);rows.append(reach)
            if i<steps:
                nx,ny,nstance=foot(phase+offset+1/steps,c)
                if stance and nstance and nx<x:
                    max_drift=max(max_drift,abs(c['speed']*duration/steps+nx-x))
    maximum=max(rows);minimum=min(rows)
    return dict(c,duration=duration,distance_per_cycle=c['speed']*duration,
        stance_span=c['speed']*duration*c['duty'],contact_phases=[0,.5],
        hip_min=min(hip_values),hip_max=max(hip_values),ankle_forward_min=min(forward),ankle_forward_max=max(forward),
        leg_reach_min=minimum,leg_reach_max=maximum,minimum_extension_margin=A+B-maximum,
        maximum_knee_flex_degrees=180-math.degrees(math.acos(max(-1,min(1,(A*A+B*B-minimum*minimum)/(2*A*B))))),
        maximum_stance_world_drift_m=max_drift,samples=steps*2+2,
        reach_with_20mm_margin=maximum<=A+B-.02,
        scope='Ideal point ankle contacts and sagittal two-link reach only. No source clips, mesh/skin/collider/terrain/torque/perceptual validation.')


if __name__=='__main__':
    result=[inspect(c) for c in CASES]
    Path(__file__).with_name('gait-feasibility.json').write_text(json.dumps(result,indent=2),encoding='utf8',newline='\n')
    for c in result:print(json.dumps(c))
