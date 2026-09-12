"""Presentation-only seated motion targets; plain Python, actor-local metres.
Actor Unity axes follow the existing corrected model: source(x,y,z)->(-x,z,-y).
The declared contact is a target, never evidence that skin/cloth supports it.
"""
import math

FPS=30
CLIPS={'MenuSeatedIdle':8.0,'MenuLook':4.0,'MenuSwat':1.4,'MenuReturn':1.8}
CONTRACT={
    'version':'human-menu-seated-source-1','root_world_unity_m':[3.3,0,5.35],
    'root_yaw_degrees':180,'scale':1,'root_motion':False,
    'seat_surface_actor_m':[0,.575,.345],'seat_front_actor_z_m':.40,
    'seat_flat_front_actor_z_m':.355,'seat_back_actor_z_m':-.32,
    'seat_half_width_m':.91,'back_support_actor_m':[0,.93,-.265],
    'back_support_required':False,'posture':'Seated near the front edge; back support is informational',
    'sole_actor_m':{'L':[-.16,0,.70],'R':[.16,0,.70]},
    'hips_actor_m':[0,.651,.345],
    'hips_height_note':'Raised7mm after first native menu showed cloth5.6mm below seat; remeasurement/visual review required',
    'left_wrist_actor_m':[-.245,.615,.28],
    'tool_socket_bone':'Socket.Grip.R','tool_runtime_anchor':'ToolSocket_R',
    'tool_mount':'Align ToolView.Grip to ToolSocket_R with production position/rotation correction',
    'tool_grip_to_impact_m':.365,'tool_grip_section_m':[.034,.024],
    'swat_contact_normalized':.5,'swat_contact_seconds':.7,
    'mosquito_pass_actor_m':[.69625,1.50,.62875],
    'mosquito_pass_note':'Elementos/Presentation agreed flight pass; measure separation from the real tool, no actual hit',
    'reduced_motion':'Hold MenuSeatedIdle at time0; no gesture',
    'transitions':'Idle full cycles -> Look -> Swat -> Return -> Idle0; .18s fade only at sequence entry/exit',
    'combat_ids_modified':False,'artistic_approval':False}


def source_point(point):
    x,y,z=point
    return (-x,-z,y)


def smooth(t):
    t=max(0,min(1,t));return t*t*t*(t*(t*6-15)+10)


REST={'wrist':[.24,.83,.50],'tilt':0.0,'head_yaw':0.0,'head_pitch':0.0,
      'chest_yaw':0.0,'lean':.02,'sway':0.0,'blink':0.0,'look_weight':0.0}
ATTENTION={**REST,'wrist':[.24,.845,.50],'chest_yaw':-.15,'look_weight':1.0}
WINDUP={**ATTENTION,'wrist':[.26,1.06,.45],'tilt':math.radians(-20)}
CONTACT={**ATTENTION,'wrist':[.40,1.10,.49],'tilt':math.radians(10),'lean':.045}
FOLLOW={**ATTENTION,'wrist':[.40,.91,.55],'tilt':math.radians(30),'lean':.035}


def blend(a,b,t):
    u=smooth(t)
    return {key:[x+(y-x)*u for x,y in zip(a[key],b[key])] if isinstance(a[key],list)
            else a[key]+(b[key]-a[key])*u for key in a}


def parameters(clip,t):
    assert clip in CLIPS and 0<=t<=1
    if clip=='MenuSeatedIdle':
        return {**REST,'lean':REST['lean']+.006*math.sin(2*math.pi*t)**2,
                'sway':.007*math.sin(2*math.pi*t)**3}
    if clip=='MenuLook':
        result=blend(REST,ATTENTION,t)
        result['head_yaw']-=.035*math.sin(math.pi*t)**2
        return result
    if clip=='MenuSwat':
        knots=[(0,ATTENTION),(.28,WINDUP),(.5,CONTACT),(1,FOLLOW)]
        for (a,left),(b,right) in zip(knots,knots[1:]):
            if t<=b:return blend(left,right,(t-a)/(b-a))
    return blend(FOLLOW,REST,t)
