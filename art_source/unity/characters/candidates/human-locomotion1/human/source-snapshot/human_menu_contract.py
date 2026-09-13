"""Presentation-only seated motion targets; plain Python, actor-local metres.
Actor Unity axes follow the existing corrected model: source(x,y,z)->(-x,z,-y).
The declared contact is a target, never evidence that skin/cloth supports it.
"""
import math

FPS=30
CLIPS={'MenuSeatedIdle':8.0,'MenuLook':4.0,'MenuSwat':1.4,'MenuReturn':1.8}
CONTRACT={
    'version':'human-menu-seated-source-2-joints','root_world_unity_m':[3.3,0,5.35],
    'root_yaw_degrees':180,'scale':1,'root_motion':False,
    'seat_surface_actor_m':[0,.575,.345],'seat_front_actor_z_m':.40,
    'seat_flat_front_actor_z_m':.355,'seat_back_actor_z_m':-.32,
    'seat_half_width_m':.91,'back_support_actor_m':[0,.93,-.265],
    'back_support_required':False,'posture':'Seated near the front edge; back support is informational',
    'sole_actor_m':{'L':[-.16,0,.70],'R':[.16,0,.70]},
    'hips_actor_m':[0,.651,.345],
    'hips_height_note':'Raised7mm after first native menu showed cloth5.6mm below seat; remeasurement/visual review required',
    'left_wrist_actor_m':[-.245,.599,.28],
    'left_palm_note':'Fingers point toward the back of the seat; avoid forced forearm pronation and measure palm support',
    'tool_socket_bone':'Socket.Grip.R','tool_runtime_anchor':'ToolSocket_R',
    'tool_mount':'Align ToolView.Grip to ToolSocket_R with production position/rotation correction',
    'grip_pose_note':'Palm follows the forearm with authored wrist tilt; skin and cuff share lower-arm/hand weights',
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


REST={'wrist':[.38,.83,.53],'tilt':0.0,'head_yaw':0.0,'head_pitch':0.0,
      'chest_yaw':0.0,'lean':.05,'sway':0.0,'spine_pitch':0.0,'shoulder_lift':0.0,
      'weight_shift':0.0,'knee_settle':0.0,'blink':0.0,'look_weight':0.0}
ATTENTION={**REST,'wrist':[.38,.855,.53],'chest_yaw':-.13,'lean':.075,'look_weight':1.0,'shoulder_lift':.003}
WINDUP={**ATTENTION,'wrist':[.41,1.10,.42],'tilt':math.radians(-24),'chest_yaw':-.23,'lean':.02,'shoulder_lift':.007}
CONTACT={**ATTENTION,'wrist':[.54,1.10,.58],'tilt':math.radians(6),'chest_yaw':.04,'lean':.13,'spine_pitch':.018}
FOLLOW={**ATTENTION,'wrist':[.47,.86,.63],'tilt':math.radians(32),'chest_yaw':.10,'lean':.105,'shoulder_lift':-.003}


def blend(a,b,t):
    u=smooth(t)
    return {key:[x+(y-x)*u for x,y in zip(a[key],b[key])] if isinstance(a[key],list)
            else a[key]+(b[key]-a[key])*u for key in a}


def strike_curve(t):
    # Pause at anticipation; carry velocity through the strike instead of
    # stopping at the mosquito pass, then decelerate into the tired follow.
    knots=[(0,ATTENTION),(.28,WINDUP),(.5,CONTACT),(1,FOLLOW)]
    for i,((a,left),(b,right)) in enumerate(zip(knots,knots[1:])):
        if t>b:continue
        u=(t-a)/(b-a);h00=2*u**3-3*u*u+1;h10=u**3-2*u*u+u
        h01=-2*u**3+3*u*u;h11=u**3-u*u
        def value(key,x,y,index=None):
            pick=lambda state:state[key][index] if index is not None else state[key]
            speed=(pick(FOLLOW)-pick(WINDUP))/.72*.65
            tangent_a=speed if i==2 else 0
            tangent_b=speed if i==1 else 0
            return h00*x+h10*(b-a)*tangent_a+h01*y+h11*(b-a)*tangent_b
        return {key:[value(key,x,y,j) for j,(x,y) in enumerate(zip(left[key],right[key]))]
                if isinstance(left[key],list) else value(key,left[key],right[key]) for key in left}
    return FOLLOW.copy()


def parameters(clip,t):
    assert clip in CLIPS and 0<=t<=1
    if clip=='MenuSeatedIdle':
        breath=math.sin(2*math.pi*t)**2;settle=math.sin(2*math.pi*t)**3
        return {**REST,'lean':REST['lean']+.035*breath,'spine_pitch':-.010*breath,
                'sway':.024*settle,'shoulder_lift':.004*breath,
                'weight_shift':.003*settle,'knee_settle':.014*settle,
                'head_pitch':.020*breath,'head_yaw':.024*settle,
                'wrist':[REST['wrist'][0]+.004*settle,REST['wrist'][1]+.006*breath,REST['wrist'][2]]}
    if clip=='MenuLook':
        result=blend(REST,ATTENTION,t)
        result['head_yaw']-=.035*math.sin(math.pi*t)**2
        return result
    if clip=='MenuSwat':
        return strike_curve(t)
    result=blend(FOLLOW,REST,t);fatigue=math.sin(math.pi*t)**2
    result['lean']+=.035*fatigue;result['head_pitch']+=.06*fatigue
    result['shoulder_lift']-=.004*fatigue
    return result
