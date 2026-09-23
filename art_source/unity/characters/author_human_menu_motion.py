"""Seated and sleeping presentation clips on the existing 65-bone rig, no combat edits.
v0.3.0 adds MenuSleep / MenuSleepSwat (UI-06 screen 1): authored upright in actor space; the Unity menu lays the actor
on Prop_BedSleeper through the contract in human_menu_contract.SLEEP_CONTRACT."""
import math
import bpy
from mathutils import Vector,Matrix
from author_motion import Pose,sampled
from human_menu_contract import CLIPS,CONTRACT,FPS,SLEEP_CLIPS,SLEEP_CONTRACT,parameters,sleep_parameters,source_point
from author_human_joints import align_menu_grip


def author(c):
    p=Pose(c)
    assert len(p.rig.pose.bones)==65,'Menu must use the existing human rig'
    hips=Vector(source_point(CONTRACT['hips_actor_m']))
    feet={}
    for side,target in CONTRACT['sole_actor_m'].items():
        # The requested anchor is the sole reference, not the ankle pivot.
        offset=p.rest['Socket.Foot.'+side].translation-p.rest['Foot.'+side].translation
        feet[side]=Vector(source_point(target))-offset

    def chain(upper,lower,target,pole,end=None,rotation=None):
        p.update();bone=p.rig.pose.bones[upper];other=p.rig.pose.bones[lower]
        reach=(Vector(target)-bone.head).length
        assert abs(bone.bone.length-other.bone.length)+.001<reach<bone.bone.length+other.bone.length-.001,(upper,'Unreachable menu target',reach)
        p.chain(upper,lower,target,pole,end,rotation)

    def pose(clip,t):
        state=parameters(clip,t);p.reset()
        p.translate('Hips',hips-p.rest['Hips'].translation+Vector((state['weight_shift'],0,0)))
        p.rotate('Spine',(state['spine_pitch'],0,state['sway']))
        p.rotate('Chest',(state['lean'],state['chest_yaw'],0))
        for side in ['L','R']:p.translate('Shoulder.'+side,(0,0,state['shoulder_lift']))
        p.update()
        # Presentation owns continuous target tracking after graph evaluation.
        # Bake only small tired offsets; no fixed mosquito target in these clips.
        p.rotate('Neck',(state['head_pitch']*.35,state['head_yaw']*.35,0))
        p.rotate('Head',(state['head_pitch']*.65,state['head_yaw']*.65,0))
        p.update()
        for side,sign in [('L',1),('R',-1)]:
            chain('UpperLeg.'+side,'LowerLeg.'+side,feet[side],(sign*.16+state['knee_settle'],-.95,.45),'Foot.'+side,p.rest['Foot.'+side])
        left=Vector(source_point(CONTRACT['left_wrist_actor_m']))
        # Palm faces the cushion, fingers toward the back of the seat. This
        # avoids forcing a half-turn of pronation into a short wrist segment.
        y=Vector((0,1,0));z=Vector((0,0,-1));x=y.cross(z)
        left_rotation=Matrix((x,y,z)).transposed().to_4x4()
        chain('UpperArm.L','LowerArm.L',left,(.32,-.60,.82),'Hand.L',left_rotation)
        right=Vector(source_point(state['wrist']))
        chain('UpperArm.R','LowerArm.R',right,(-.25,-.22,.77))
        align_menu_grip(p,'R',state['tilt'])
        p.fingers('L',.08);p.fingers('R',1)
        return p.snapshot()

    # Sign of the bone-local Z roll that tilts the head toward the actor's left (+X): measured, not assumed.
    p.reset();p.rotate('Neck',(0,0,.2));p.update()
    roll_sign=1 if (p.rig.pose.bones['Head'].head.x>0) else -1
    p.reset()

    # sleep_parameters() is written for the canonical "right side down" pose (top = left). The contract lies the
    # sleeper on SLEEP_CONTRACT['lying_side']; the left side mirrors every lateral value (x, roll, yaw) and swaps limbs.
    mirror=-1 if SLEEP_CONTRACT['lying_side']=='left' else 1
    top,bottom=('R','L') if mirror<0 else ('L','R')

    def mx(v):return Vector((mirror*v[0],v[1],v[2]))

    def sleep_pose(clip,t):
        state=sleep_parameters(clip,t);p.reset();b=state['breath']
        p.rotate('Spine',(state['spine']+.012*b,0,0))
        p.rotate('Chest',(state['chest']+.024*b,0,0))
        # The top shoulder rises toward the ceiling with every breath; both shoulders protract toward the folded
        # arms, narrowing the side-lying silhouette under the quilt.
        p.translate('Shoulder.'+top,tuple(mx((-.07+.007*b,-.05,-.015+.003*b))))
        p.translate('Shoulder.'+bottom,tuple(mx((.07,-.05,-.015))))
        p.update()
        p.rotate('Neck',(state['head_pitch']*.4,mirror*state['neck_yaw']*.5,mirror*roll_sign*state['neck_roll']))
        p.rotate('Head',(state['head_pitch']*.6,mirror*state['neck_yaw']*.5,mirror*roll_sign*state['head_roll']))
        p.rotate('Jaw',(state['jaw'],0,0))
        p.update()
        # Knees drawn up (bottom leg straighter), the top leg resting over the bottom one.
        for side,x,hip_deg,knee_deg in [(bottom,mirror*-.10,35,50),(top,.0,60,85-50*state['top_knee_extend'])]:
            hip=p.rig.pose.bones['UpperLeg.'+side].head.copy()
            a=math.radians(hip_deg);s_=math.radians(hip_deg-knee_deg)
            upper=p.rig.pose.bones['UpperLeg.'+side].bone.length;lower=p.rig.pose.bones['LowerLeg.'+side].bone.length
            knee=hip+Vector((0,-math.sin(a),-math.cos(a)))*upper
            ankle=knee+Vector((0,-math.sin(s_),-math.cos(s_)))*lower
            ankle.x=x
            chain('UpperLeg.'+side,'LowerLeg.'+side,ankle,(x,-.95,.55),'Foot.'+side,p.rest['Foot.'+side])
        chain('UpperArm.'+bottom,'LowerArm.'+bottom,mx(state['bottom_hand']),tuple(mx((-.36,-.18,.92))))
        chain('UpperArm.'+top,'LowerArm.'+top,mx(state['top_hand']),tuple(mx((.44,-.10,1.02))))
        p.fingers(top,state['top_fingers']);p.fingers(bottom,state['bottom_fingers'])
        return p.snapshot()

    for name,seconds in CLIPS.items():
        fn=sleep_pose if name in SLEEP_CLIPS else pose
        sampled(c,name,round(seconds*FPS)+1,lambda t,name=name,fn=fn:fn(name,t))
        action=bpy.data.actions['Human_'+name]
        action.name=name
        c.clips[-1].update(name=name,loop=name in ('MenuSeatedIdle','MenuSleep'),presentation_only=True)
    return {'contract':CONTRACT,'sleep_roll_sign':roll_sign,'fixed_gaze_baked':False,'clips':c.clips,'minimum_chain_reach_margin_m':p.minimum_reach_margin,
            'tool_grip_validated':False,'seat_skin_contact_validated':False,'visual_approval':False}
