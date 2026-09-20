"""Source-sheet proportional refinement; no gameplay anchor or shape edits."""
import math
from types import SimpleNamespace
import bpy
from mathutils import Vector


def apply(kind, root, api):
    if kind not in ('Duke', 'MineLander', 'Missile', 'MultiMissile'):
        return root
    a = SimpleNamespace(**api)
    objects = list(root.children_recursive)
    def named(prefix):
        return [o for o in objects if o.name.split('.')[0] == prefix]
    def move(prefix, delta):
        for o in named(prefix): o.location += a.v(delta)
    def scale(prefix, xyz):
        for o in named(prefix):
            o.scale.x *= xyz[0]; o.scale.z *= xyz[1]; o.scale.y *= xyz[2]
    def bolt(p, parent=root, mat='Edge', radius=.032):
        return a.cyl('Refined mechanical rivet',p,radius,.026,mat,parent,'z',seg=12)
    def mesh(name, verts, faces, mat, parent=root):
        data=bpy.data.meshes.new(name);data.from_pydata([a.v(p) for p in verts],[],faces);data.update()
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
        return a.finish(obj,name,mat,parent,.015)
    if kind == 'Duke':
        # Projected eye whites in the source are large and almost frontal.
        for eye in named('Eye'):
            eye.rotation_euler.z=math.radians(18 if eye.location.x>0 else -18)
            eye.scale=(1.22,1.12,1.15);eye.location+=a.v((0,-.035,.19))
        # Make the snout a cheeked dome and deepen the angular chin to the tread baseline.
        scale('Faceted broad frog snout',(1.03,1.0,1.12))
        for o in named('Faceted broad frog snout'):
            for vertex in o.data.vertices:
                vertex.co.z-=.12+.16*abs(vertex.co.x)
        move('Toxic warning triangle',(0,0,.09))
        for o in named('Faceted frog chin'):
            for vertex in o.data.vertices:
                y=vertex.co.z
                vertex.co.z=.13+(y-.37)*(1.30/1.06)
                vertex.co.x*=1.03
        # Close the corresponding lower armor seam rather than leaving floating old lines.
        for prefix in ('Surface fitted chin plate joint','Surface fitted lower chin seam','Small chin plate rivet'):
            for o in named(prefix):
                for vertex in o.data.vertices:
                    if vertex.co.z<1.43:vertex.co.z=.13+(vertex.co.z-.37)*(1.30/1.06)
        for prefix in ('Transparent poison glass','Poison liquid inside','Visible liquid surface','Bottle metal collar'):
            scale(prefix,(1.17,1,1.17))
        scale('Bottle neck cap',(1.45,.80,1.45))
        # Visible front cage uprights and collars/rivets are genuine round metal sections.
        for s in (-1,1):
            for dx in (-.34,.34):
                a.beam('Front poison cage upright',(s*.65+dx,1.70,-.79),(s*.65+dx,3.06,-.79),.035,'Metal',root)
            for y in (1.64,3.12):
                for dx in (-.25,0,.25):bolt((s*.65+dx,y,-.59))
            a.box('Frog forward track armor',(s*1.33,.89,1.51),(.62,.40,.33),'Body',root,.065)
            a.box('Frog inset headlamp frame',(s*1.33,.95,1.69),(.25,.15,.025),'Metal',root,.016)
            a.box('Frog ivory headlamp',(s*1.33,.95,1.71),(.18,.09,.015),'Ivory',root,.008)
            for y,z in ((.20,1.40),(.38,1.52),(.56,1.54),(.72,1.48)):
                a.box('Frog forward segmented track pad',(s*1.325,y,z),(.64,.125,.16),'Edge',root,.018)
            for y in (.28,.85,1.29):bolt((s*(.32 if y<.5 else .60),y,1.46))
    elif kind == 'MineLander':
        # Scoop reveals side tracks in the reference instead of spanning the full chassis.
        for prefix in ('Concave steel digging scoop','Scoop raised side wing','Sculpted triangular digging tooth'):
            for o in named(prefix):
                o.location.x*=.84
                for v in o.data.vertices:v.co.x*=.84
        for s in (-1,1):
            for j in range(10):
                q=math.tau*j/10
                bolt((s*.48+math.cos(q)*.30,1.76+math.sin(q)*.30,1.025),radius=.020)
            a.box('Mole front track fender',(s*1.30,.90,.90),(.55,.32,.57),'Body',root,.055)
            a.box('Mole fender inset',(s*1.30,1.075,1.01),(.22,.018,.18),'Metal',root,.016)
            a.cyl('Mole exposed pivot hub',(s*1.10,1.38,-.42),.17,.04,'Metal',root,'x',seg=32)
            a.cyl('Mole dark pivot center',(s*1.125,1.38,-.42),.095,.045,'Rubber',root,'x',seg=32)
            a.beam('Mole yellow piston housing',(s*1.05,1.38,.58),(s*1.11,.92,1.24),.20,'Body',root)
            for y,z in ((.19,1.22),(.36,1.35),(.53,1.37),(.70,1.32)):
                a.box('Mole forward segmented track pad',(s*1.30,y,z),(.64,.125,.16),'Edge',root,.018)
        # Open-bore tube gets a fine metal rim, rather than a solid oversized cap.
        barrel=named('Barrel')[0]
        a.torus('Mole machined muzzle edge',(0,0,1.24),.401,.020,'Edge',barrel)
    elif kind == 'Missile':
        # The ivory nose occupies less width than the red cheeks in the source front.
        nose=named('Long ivory ogive')[0]
        for v in nose.data.vertices:v.co.x*=.78;v.co.z*=.78
        scale('Nose seam',(.78,.78,.78))
        # Old seams were authored on the untapered cylinder; the shared UV paint pass
        # supplies panel lines on the new surface instead of detached wire hoops.
        for prefix in ('Rocket panel seam','Longitudinal shell seam'):
            for o in named(prefix):
                objects.remove(o)
                bpy.data.objects.remove(o,do_unlink=True)
        for shell in named('Red rocket shell'):
            # Cylinder's local Z is the longitudinal axis before its authored rotation.
            hi=max(v.co.z for v in shell.data.vertices)
            for vertex in shell.data.vertices:
                if vertex.co.z>0:
                    factor=1-.08*vertex.co.z/hi
                    vertex.co.x*=factor;vertex.co.y*=factor
        for eye in named('Eye'):
            eye.rotation_euler.z=math.radians(24 if eye.location.x>0 else -24)
            eye.location.x*=1.13
            eye.scale.z*=1.07
        # Raise the visual assembly with its cradle, keep Turret/Barrel/FirePoint intact.
        rocket=named('Rocket body group')[0];rocket.location+=a.v((0,.19,-.15))
        a.box('Missile front central chassis',(0,.53,1.08),(.83,.74,.21),'Navy',root,.05)
        a.box('Missile central recessed plate',(0,.57,1.20),(.52,.50,.026),'Metal',root,.025)
        a.box('Missile upright bearing support',(0,1.08,.72),(.42,.62,.22),'Navy',root,.035)
        a.cyl('Missile front bearing',(0,1.26,.85),.13,.07,'Metal',root,'z',seg=32)
        for s in (-1,1):
            for y in (.30,.74):bolt((s*.31,y,1.204))
            for y,z in ((.19,1.10),(.36,1.19),(.53,1.20),(.70,1.14)):
                a.box('Missile front tread armor pad',(s*1.04,y,z),(.66,.125,.13),'Navy',root,.018)
    elif kind == 'MultiMissile':
        # A taller forehead, lower eyes and a narrower ivory throat read as one turtle head.
        scale('Turtle forward head',(1.12,1.16,1))
        scale('Turtle ivory throat',(.98,1.02,.90))
        move('Turtle ivory throat',(0,-.06,.17))
        for eye in named('Eye'):
            eye.rotation_euler.z=math.radians(23 if eye.location.x>0 else -23)
            eye.location+=a.v((0,-.04,.12));eye.scale=(.92,1.04,1.10)
        # Keep neck rooted to chassis; its extension and shell remain actual 3D volumes.
        scale('Turtle belly',(.96,.73,1))
        for wheel in named('Wheel'):
            wheel.scale=(1.60,1.15,1.15)
        for leg in named('Separate turtle wheel leg'):leg.scale.x=1.25
        for lip in named('Dorsal bore lip'):
            lip.scale=(1.05,1,1.05)
            lip.data.materials[0]=a.MATS['Teal']
        # Front pack rim and shallow panel divisions use physical thin strips.
        barrel=named('Barrel')[0]
        for x in (-1.04,1.04):a.box('Dorsal vertical face border',(x,0,.855),(.035,1.23,.024),'Body',barrel,.009)
        for y in (-.66,.66):a.box('Dorsal horizontal face border',(0,y,.855),(1.94,.035,.024),'Body',barrel,.009)
        for s in (-1,1):
            for y,z in ((.18,1.14),(.36,1.26),(.54,1.22)):
                a.box('Turtle broad forward tire pad',(s*1.11,y,z),(.52,.125,.10),'Metal',root,.025)
            for z in (-.92,.81):
                for dx in (-.16,.16):bolt((s*1.11+dx,.69,z+.435),mat='Gold')
            a.ball('Turtle inset nostril',(s*.20,1.63,2.568),(.025,.035,.015),'Teal',root)
    root['approvedMethodGeometry']='mechanical-b-v1'
    return root
