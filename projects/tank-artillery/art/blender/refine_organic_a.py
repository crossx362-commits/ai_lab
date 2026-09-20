"""Editable source-matched silhouette corrections for the four organic vehicles.
All coordinates are Blender local (X right, Y back, Z up). Rig anchors untouched.
"""
import math
import bpy

KINDS=('Catapult','CrossBow','Cannon','Carrot')
DECALS={}

def apply(kind,root,api):
    if kind not in KINDS:return root
    objects=list(root.children_recursive)
    def named(prefix):return [o for o in objects if o.name.split('.')[0]==prefix]
    # The original yaw flattened each sclera in front view and elongated its pupil.
    # Retain wrap-around sockets, but expose the rounder source face and inward gaze.
    for eye in named('Eye'):
        side=1 if eye.location.x>0 else -1
        eye.rotation_euler.z=math.radians(side*{'Catapult':25,'CrossBow':18,'Cannon':33,'Carrot':24}[kind])
        eye.scale.z={'Catapult':1.03,'CrossBow':1.20,'Cannon':.94,'Carrot':1.04}[kind]
        eye.scale.x={'Catapult':1.04,'CrossBow':1.28,'Cannon':1.02,'Carrot':1.02}[kind]
        if kind=='CrossBow':eye.location.x=side*.53;eye.location.y=-1.18;eye.location.z=.96
        if kind=='Catapult':eye.location.y=-1.10
        if kind=='Carrot':eye.location.x=side*.64;eye.location.y-=.10
        for child in eye.children_recursive:
            if child.name.startswith('Deep pupil'):
                child.scale.z=.78;child.scale.x=1.02
                child.location.x=-side*.065
            elif child.name.startswith('Eye highlight'):
                child.location.x=-side*.065-.035;child.location.z*=.85
    if kind in ('Catapult','Cannon'):
        for o in named('Wheel'):
            o.scale.x*=1.45 if kind=='Catapult' else 1.75
            if kind=='Cannon':o.location.x*=1.07
    if kind=='Catapult':
        for o in named('Sloping carved oak eyebrow'):
            for v in o.data.vertices:v.co.z=1.56+(v.co.z-1.56)*.90
        for o in named('Faceted loaded boulder'):o.scale.z*=.85
    elif kind=='CrossBow':
        # Folded leaves remain closed volumes. Lengthen forehead to the beak,
        # lift the outside tips, and widen its side fan to match the source tiers.
        for prefix in ('Pointed owl brow leaf','Central owl forehead leaf'):
            for o in named(prefix):
                side=1 if sum(v.co.x for v in o.data.vertices)>0 else -1
                central=prefix.startswith('Central')
                # Slim closed lanceolate leaf: wide near the crest, tapered at the nose.
                for j in range(17):
                    t=j/16
                    cx=0 if central else side*(.055+.86*t)
                    cz=.82+1.03*t
                    cy=-1.44+.36*t
                    profile=max(.002,math.sin(math.pi*t))*(.12+.88*t)
                    for q in range(12):
                        a=q*math.tau/12
                        v=o.data.vertices[j*12+q]
                        offset=(.18 if central else .20)*profile*math.cos(a)
                        v.co=(cx+offset,cy+.045*profile*math.sin(a),cz-(0 if central else side*.78*offset))
        for o in named('Overlapping owl leaf')+named('Leaf midrib'):
            if o.type=='MESH':
                for v in o.data.vertices:v.co.x*=1.13
            elif o.type=='CURVE':o.scale.x*=1.13
        for o in named('Flat laminated recurved oak limb'):
            for v in o.data.vertices:
                v.co.z*=1.45
        for o in named('Bow tip metal cap'):o.scale.z*=1.25
    elif kind=='Cannon':
        for o in named('Thick gold captain eyebrow'):
            side=1 if o.parent.location.x>0 else -1
            # Source inner brow ends descend toward the muzzle on both sides.
            for v in o.data.vertices:v.co.z+=side*.43*v.co.x
            o.location.z-=.03
        for o in named('Round hinged captain lid'):
            o.scale.x=1.07;o.scale.y=1.07;o.location.z+=.24
            o.rotation_euler.x=math.radians(8)
            api['cyl']('Captain hatch seat',(0,1.14,-.28),.56,.25,'Navy',o.parent,seg=48)
    elif kind=='Carrot':
        for side in (-1,1):
            api['box']('Front gold track cheek',(side*1.14,.86,1.24),(.73,.27,.11),'Gold',root,.035)
            for dx in (-.26,.26):
                api['cyl']('Front track cheek fastener',(side*1.14+dx,.88,1.307),.025,.018,'Metal',root,'z',seg=12)
        for o in named('Curved track link'):
            o.scale.x*=1.12
            o.scale.y*=.57
        for o in named('Flat track link'):o.scale.x*=1.12
        for o in named('Wrapped curved fender'):
            # Expose the front end tread and its separate pads below the gold cover.
            o.scale.y*=.85
            o.scale.x*=1.08
        # The concept is a plump root with a rounded low shoulder, not a cone.
        # Deform armor and its inlaid joints identically in their unchanged local frame.
        for prefix in ('Smooth tapered carrot armor','Inset carrot armor joint'):
            for o in named(prefix):
                for v in o.data.vertices:
                    height=v.co.z
                    factor=1+.64*max(0,min(1,(-height+.04)/1.17))
                    v.co.x*=factor
                    v.co.z=max(-1.04,v.co.z)
        for o in named('Orange root eyebrow'):
            side=1 if o.parent.location.x>0 else -1
            for v in o.data.vertices:v.co.z+=side*.46*v.co.x
        # Separate lower crown lobes restore the five-leaf front fan.
        for o in named('Broad folded carrot crown leaf'):
            for v in o.data.vertices:
                if abs(v.co.x)>.40:v.co.x*=1.12
    root['approvedMethodGeometry']='organic-a-v1'
    bpy.context.view_layer.update()
    return root
