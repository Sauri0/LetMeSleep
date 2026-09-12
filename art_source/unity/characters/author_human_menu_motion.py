"""Four seated presentation clips on the existing65-bone rig, no combat edits."""
import math
import bpy
from mathutils import Vector,Matrix
from author_motion import Pose,sampled
from human_menu_contract import CLIPS,CONTRACT,FPS,parameters,source_point


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
        p.translate('Hips',hips-p.rest['Hips'].translation)
        p.rotate('Spine',(0,0,state['sway']))
        p.rotate('Chest',(state['lean'],state['chest_yaw'],0))
        p.update()
        # Aim toward the agreed room pass in the Chest frame, not a canned
        # forward glance. Split the turn across neck/head, leaving eye geometry.
        neck=p.rig.pose.bones['Neck']
        direction=neck.parent.matrix.to_3x3().inverted()@(Vector(source_point(CONTRACT['mosquito_pass_actor_m']))-p.rig.pose.bones['Socket.Eye'].head)
        yaw=max(-math.radians(75),min(math.radians(75),math.atan2(direction.x,direction.z)))*state['look_weight']
        pitch=max(-.3,min(.3,-math.atan2(direction.y,math.hypot(direction.x,direction.z))))*state['look_weight']
        yaw+=state['head_yaw'];pitch+=state['head_pitch']
        p.rotate('Neck',(pitch*.35,yaw*.35,0));p.rotate('Head',(pitch*.65,yaw*.65,0))
        p.update()
        # Correct the residual world-space gaze error caused by the eye offset
        # and the chest/neck turns; keep the Head pivot and its child hierarchy.
        for _ in range(2):
            forward=(Vector(source_point(CONTRACT['mosquito_pass_actor_m']))-p.rig.pose.bones['Socket.Eye'].head).normalized()
            up=Vector((0,0,1));up=(up-forward*up.dot(forward)).normalized();right=up.cross(forward)
            desired=Matrix((right,up,forward)).transposed().to_quaternion()
            head=p.rig.pose.bones['Head'];matrix=head.matrix.to_quaternion().slerp(desired,state['look_weight']).to_matrix().to_4x4()
            matrix.translation=head.head;head.matrix=matrix;p.update()
        for side,sign in [('L',1),('R',-1)]:
            chain('UpperLeg.'+side,'LowerLeg.'+side,feet[side],(sign*.16,-.95,.45),'Foot.'+side,p.rest['Foot.'+side])
        left=Vector(source_point(CONTRACT['left_wrist_actor_m']))
        # Open left palm beside the seated pelvis, oriented toward the cushion.
        y=Vector((0,-1,0));z=Vector((0,0,-1));x=y.cross(z)
        left_rotation=Matrix((x,y,z)).transposed().to_4x4()
        chain('UpperArm.L','LowerArm.L',left,(.50,-.30,.82),'Hand.L',left_rotation)
        right=Vector(source_point(state['wrist']))
        right_rotation=Matrix.Rotation(state['tilt'],4,'X')@p.rest['Hand.R']
        chain('UpperArm.R','LowerArm.R',right,(-.50,-.35,.85),'Hand.R',right_rotation)
        p.fingers('L',.08);p.fingers('R',1)
        return p.snapshot()

    for name,seconds in CLIPS.items():
        sampled(c,name,round(seconds*FPS)+1,lambda t,name=name:pose(name,t))
        action=bpy.data.actions['Human_'+name]
        action.name=name
        c.clips[-1].update(name=name,loop=name=='MenuSeatedIdle',presentation_only=True)
    return {'contract':CONTRACT,'clips':c.clips,'minimum_chain_reach_margin_m':p.minimum_reach_margin,
            'tool_grip_validated':False,'seat_skin_contact_validated':False,'visual_approval':False}
