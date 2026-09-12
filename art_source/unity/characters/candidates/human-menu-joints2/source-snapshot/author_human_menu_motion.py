"""Four seated presentation clips on the existing65-bone rig, no combat edits."""
import math
import bpy
from mathutils import Vector,Matrix
from author_motion import Pose,sampled
from human_menu_contract import CLIPS,CONTRACT,FPS,parameters,source_point
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

    for name,seconds in CLIPS.items():
        sampled(c,name,round(seconds*FPS)+1,lambda t,name=name:pose(name,t))
        action=bpy.data.actions['Human_'+name]
        action.name=name
        c.clips[-1].update(name=name,loop=name=='MenuSeatedIdle',presentation_only=True)
    return {'contract':CONTRACT,'fixed_gaze_baked':False,'clips':c.clips,'minimum_chain_reach_margin_m':p.minimum_reach_margin,
            'tool_grip_validated':False,'seat_skin_contact_validated':False,'visual_approval':False}
