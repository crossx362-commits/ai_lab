"""Editable source-matched silhouette corrections for the four organic vehicles.
All coordinates are Blender local (X right, Y back, Z up). Rig anchors untouched.
"""
import math
import bpy
from mathutils import Vector

KINDS=('Catapult','CrossBow','Cannon','Carrot')
DECALS={}

def apply(kind,root,api):
    if kind not in KINDS:return root
    objects=list(root.children_recursive)
    def named(prefix):return [o for o in objects if o.name.split('.')[0]==prefix]
    def traced_leaf(obj, outline, depth_fn, thickness=.055):
        # Closed volume from an explicitly traced front outline. The front center
        # is raised above the edge to create a folded surface, never an art plane.
        cx=sum(p[0] for p in outline)/len(outline);cz=sum(p[1] for p in outline)/len(outline)
        n=len(outline);verts=[]
        for back in (False,True):
            for x,z in outline:verts.append((x,depth_fn(x,z)+(thickness if back else -thickness),z))
        verts.extend([(cx,depth_fn(cx,cz)-thickness-.075,cz),(cx,depth_fn(cx,cz)+thickness,cz)])
        faces=[]
        for i in range(n):
            j=(i+1)%n;faces.extend([(2*n,i,j),(2*n+1,n+j,n+i),(i,n+i,n+j,j)])
        mesh=bpy.data.meshes.new(obj.name+' traced closed leaf');mesh.from_pydata(verts,[],faces);mesh.update()
        for mat in obj.data.materials:mesh.materials.append(mat)
        obj.data=mesh
        for face in mesh.polygons:face.use_smooth=True
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
        # Front outline traced against source pixels: beak (296,590),
        # central crown (296,369), outer leaf tips (130,368)/(463,367).
        # Mapping: source x=296+166*x; source y=755-190*z.
        center=[(0,.84),(-.105,1.22),(-.23,1.62),(-.16,1.86),(0,2.03),(.16,1.86),(.23,1.62),(.105,1.22)]
        side_outline=[(.055,.84),(.18,1.34),(.39,1.66),(.72,1.88),(1.00,2.04),(.91,1.68),(.66,1.38),(.27,1.08)]
        depth=lambda x,z:-1.47+max(0,(z-1.14))*1.05
        for o in named('Central owl forehead leaf'):traced_leaf(o,center,depth)
        for o in named('Pointed owl brow leaf'):
            sign=1 if sum(v.co.x for v in o.data.vertices)>0 else -1
            traced_leaf(o,[(sign*x,z) for x,z in side_outline],depth)
        for o in named('Owl rounded feather body'):o.scale.z=1.15
        # A broad dorsal leaf overlaps the front crest in top view, as in the sheet.
        template=named('Central owl forehead leaf')[0]
        dorsal=template.copy();dorsal.data=template.data.copy();bpy.context.collection.objects.link(dorsal)
        dorsal.name='Owl dorsal central leaf'
        outline=[(0,-1.15),(-.34,-.80),(-.43,-.24),(-.35,.35),(0,.94),(.35,.35),(.43,-.24),(.34,-.80)]
        # reuse closed lens construction, then turn the traced plane onto the roof.
        traced_leaf(dorsal,outline,lambda x,z:-1.82-.10*(1-abs(z)))
        for v in dorsal.data.vertices:
            x,y=v.co.x*1.45,v.co.z+.25
            z=1.13+.75*math.sqrt(max(.015,1-(x/1.1)**2-(y/1.35)**2))+.045
            v.co=Vector((x,y,z+(-v.co.y-1.82)*.55))
        for o in named('Small hooked owl beak'):o.location.y-=.15;o.location.z-=.06
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
            o.scale.x=1.07;o.scale.y=1.07;o.location.z+=.06
            o.rotation_euler.x=math.radians(4)
            api['cyl']('Captain hatch seat',(0,1.04,-.28),.70,.23,'Navy',o.parent,seg=48)
            for ch in o.children:
                if ch.name.startswith('Navy round hatch'):ch.scale.z*=1.55
                elif ch.name.startswith('Curved hatch crown'):ch.scale.z*=1.55
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
        # Source fan tiers, traced from front pixels (center x=288): central
        # tip (288,160), mid tips (125,212)/(450,212), low (90,298)/(484,298).
        specs=[([(0,.65),(-.25,1.12),(-.31,1.61),(-.19,1.96),(0,2.20),(.19,1.96),(.31,1.61),(.25,1.12)],0),
               ([(.10,.68),(.31,1.22),(.60,1.65),(.93,1.84),(.92,1.38),(.69,.96),(.32,.72)],1),
               ([(.19,.64),(.53,.94),(.96,1.20),(1.18,1.23),(1.01,.86),(.71,.67),(.34,.58)],1)]
        leaves=named('Broad folded carrot crown leaf')
        outlines=[specs[0][0],specs[1][0],[(-x,z) for x,z in specs[1][0]],specs[2][0],[(-x,z) for x,z in specs[2][0]]]
        for i,o in enumerate(leaves):
            outline=outlines[min(i,4)]
            # upper/mid leaves lean back so they remain wide in top and side views.
            depth=lambda x,z: .46+(z-.65)*(.78 if i<3 else .50)
            traced_leaf(o,outline,depth,.065)
            if i>=3:
                for v in o.data.vertices:v.co.z+=.25
            if i==5:
                # A rear leaf turned into the side plane gives the fan real side
                # breadth, matching the side drawing rather than knife edges.
                traced_leaf(o,specs[0][0],lambda x,z:0,.07)
                for v in o.data.vertices:
                    x,y,z=v.co
                    v.co=Vector((y,.75+x*1.9+(z-.65)*.55,.65+(z-.65)*.83))
    root['approvedMethodGeometry']='organic-a-v1'
    bpy.context.view_layer.update()
    return root
