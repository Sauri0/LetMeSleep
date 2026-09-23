"""Anatomical hand weighting without Blender dependencies.
Smoothing respects finger/joint regions and physical neighbor distance.
v0.3.0 round 5: the hands are 25% larger, so the finger-base neighbourhood
and the palm web reach scale with them (HAND_SCALE).
"""
import math


def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def clamp(x):return max(0.0,min(1.0,x))


HAND_SCALE=1.25


def hand_weights(positions,edges,paths,side):
    chains={}
    for name,a,b,digit in paths:chains.setdefault(digit,[]).append((name,tuple(a),tuple(b)))
    palm='Hand.'+side
    weights=[];regions=[]
    for position in positions:
        candidates=[]
        for digit,chain in chains.items():
            for name,a,b in chain:
                direction=sub(b,a);length2=dot(direction,direction)
                t=clamp(dot(sub(position,a),direction)/length2)
                closest=tuple(x+d*t for x,d in zip(a,direction))
                delta=sub(position,closest)
                candidates.append((dot(delta,delta),digit,name))
        _,digit,name=min(candidates,key=lambda candidate:candidate[0])
        chain=chains[digit];start=chain[0][1];axis=sub(chain[-1][2],start)
        length=math.sqrt(dot(axis,axis));axis=tuple(v/length for v in axis)
        longitudinal=dot(sub(position,start),axis)
        amount=clamp((longitudinal+.007)/.023)
        segments={name:1.0}
        for joint in (1,2):
            joint_at=dot(sub(chain[joint][1],start),axis)
            if abs(longitudinal-joint_at)<.010:
                t=(longitudinal-joint_at+.010)/.020
                segments={chain[joint-1][0]:1-t,chain[joint][0]:t}
                break
        weight={bone:value*amount for bone,value in segments.items() if value*amount>0}
        if amount<1:weight[palm]=1-amount
        allowed=set(segments)
        if amount<1:allowed.add(palm)
        # Only the actual palm/web transition can mix neighboring first
        # phalanges. A thumb vertex cannot borrow a straight finger's chain.
        if amount<.85 and digit!='Thumb':
            for other,other_chain in chains.items():
                if other!='Thumb' and math.dist(start,other_chain[0][1])<.036*HAND_SCALE:
                    allowed.add(other_chain[0][0])
        # The broad thumb web joins the palm before the straight fingers have
        # left it. Preserve that continuous region instead of introducing a
        # hard nearest-chain boundary through its skin.
        region=digit
        palm_edge=max(abs(chain[0][1][0]) for key,chain in chains.items() if key!='Thumb')+.026*HAND_SCALE
        if abs(position[0])<palm_edge:
            allowed={palm}|{chain[0][0] for chain in chains.values()}|{bone[0] for bone in chains['Thumb']}
            region='PalmWeb'
        weights.append(weight);regions.append((region,allowed))

    neighbors=[[] for _ in positions]
    for a,b in edges:
        distance=math.dist(positions[a],positions[b])
        # Decimation can connect a proximal vertex to a distal vertex 47 mm
        # away. Topological adjacency is not a license to transfer that weight.
        if distance<=.010:
            influence=1/(distance*distance+.003*.003)
            neighbors[a].append((b,influence));neighbors[b].append((a,influence))
    for _ in range(3):
        smoothed=[]
        for i,weight in enumerate(weights):
            allowed=regions[i][1]
            local=[(j,influence) for j,influence in neighbors[i]
                   if regions[i][0]==regions[j][0] or palm in allowed and palm in regions[j][1]]
            total=sum(influence for _,influence in local)
            result={bone:value*(.55 if total else 1) for bone,value in weight.items()}
            if total:
                for j,influence in local:
                    for bone,value in weights[j].items():
                        if bone in allowed:result[bone]=result.get(bone,0)+value*.45*influence/total
            strongest=sorted(result.items(),key=lambda item:item[1],reverse=True)[:4]
            norm=sum(value for _,value in strongest)
            assert norm>0 and math.isfinite(norm)
            smoothed.append({bone:value/norm for bone,value in strongest if value>0})
        weights=smoothed
    return weights
