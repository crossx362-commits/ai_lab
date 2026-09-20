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
            # Source: socket ~112 px / 495 px full front width, not a third of the hull.
            eye.scale=(.91,1.06,.97);eye.location+=a.v((0,-.035,.19))
        # Make the snout a cheeked dome and deepen the angular chin to the tread baseline.
        for o in named('Faceted broad frog snout'):
            # A sloping cheek plate replaces the old thick horizontal cylinder lip.
            profile=[(-1.06,1.30),(-.92,1.57),(-.55,1.77),(0,1.84),(.55,1.77),(.92,1.57),(1.06,1.30),(.81,1.23),(.43,1.41),(0,1.43),(-.43,1.41),(-.81,1.23)]
            verts=[a.v((x,y,z)) for z in (1.10,1.56) for x,y in profile]
            n=len(profile);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
            faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
            o.data.clear_geometry();o.data.from_pydata(verts,[],faces);o.data.update()
            for polygon in o.data.polygons:polygon.use_smooth=False
        move('Toxic warning triangle',(0,0,.09))
        for o in named('Surface fitted frog mouth'):
            objects.remove(o);bpy.data.objects.remove(o,do_unlink=True)
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
        nose_base=min(-v.co.y for v in nose.data.vertices)
        nose_length=max(-v.co.y for v in nose.data.vertices)-nose_base
        for vertex in nose.data.vertices:
            t=(-vertex.co.y-nose_base)/nose_length
            vertex.co.z+=.38*t
            vertex.co.y+=.055*t
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
            eye.location.x*=.98
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
        scale('Turtle forward head',(1.20,1.16,1))
        move('Turtle forward head',(0,-.28,0))
        for throat in named('Turtle ivory throat'):
            # A volumetric cream underside intersects the green head to form the smile;
            # unlike a crescent sheet it remains rounded and connected in side view.
            volume=a.ball('Refined throat volume',(0,0,0),(.74,.39,.62),'Ivory',root)
            throat.data=volume.data;throat.location=a.v((0,1.04,1.94));throat.scale=(1,1,1)
            bpy.data.objects.remove(volume,do_unlink=True)
        for eye in named('Eye'):
            eye.rotation_euler.z=math.radians(23 if eye.location.x>0 else -23)
            eye.location+=a.v((0,-.30,.12));eye.scale=(.92,1.04,1.10)
        for o in named('Turtle friendly mouth'):
            objects.remove(o);bpy.data.objects.remove(o,do_unlink=True)
        # The reference cream throat continues into a broad chest in front of the shell.
        a.sculpt('Turtle connected ivory chest',(0,.80,1.82),(.60,.22,.32),'Ivory',root,.75)
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
            a.ball('Turtle inset nostril',(s*.20,1.35,2.568),(.025,.035,.015),'Teal',root)
    root['approvedMethodGeometry']='mechanical-b-v1'
    return root


def post_fit(kind, root, api):
    """Restore face depth ordering after FRONT-only landmark fitting.

    Only posed world depth is edited; registered world X/height and rig anchors
    are unchanged. The authored barrel pose is restored before returning.
    """
    if kind not in ('Duke','MineLander','Missile'):
        return root
    objects=list(root.children_recursive)
    barrel=next(o for o in objects if o.name.split('.')[0]=='Barrel')
    previous=barrel.rotation_euler.copy()
    barrel.rotation_euler.x=-math.radians({'Duke':0,'MineLander':55,'Missile':20}[kind])
    bpy.context.view_layer.update()
    def named(prefix):
        return [o for o in objects if o.type=='MESH' and o.name.split('.')[0]==prefix]
    def ys(group):
        return [(o.matrix_world@v.co).y for o in group for v in o.data.vertices]
    def forward(group,delta):
        if delta>=0:return
        for obj in group:
            matrix=obj.matrix_world.copy();inverse=matrix.inverted()
            for vertex in obj.data.vertices:
                point=matrix@vertex.co;point.y+=delta;vertex.co=inverse@point
            obj.data.update()
    if kind in ('Duke','Missile'):
        occluders=named('Faceted broad frog snout' if kind=='Duke' else 'Red rocket shell')
        front=min(ys(occluders))
        for eye in [o for o in objects if o.name.split('.')[0]=='Eye']:
            group=[o for o in eye.children_recursive if o.type=='MESH']
            white=[o for o in group if o.name.startswith('Ivory sclera')]
            if kind=='Duke':forward(group,front-.025-max(ys(white)))
            # Reproject the pupil onto the curved white surface at the same FRONT
            # coordinates; a whole-pupil offset would leave it floating in side view.
            from mathutils.bvhtree import BVHTree
            sclera=white[0]
            surface=BVHTree.FromPolygons([sclera.matrix_world@v.co for v in sclera.data.vertices],
                                         [tuple(p.vertices) for p in sclera.data.polygons])
            pupil=[o for o in group if o.name.startswith(('Deep pupil','Eye highlight'))]
            origin_y=min(ys(white))-2
            for obj in pupil:
                matrix=obj.matrix_world.copy();inverse=matrix.inverted()
                values=ys([obj]);lo,hi=min(values),max(values)
                for vertex in obj.data.vertices:
                    point=matrix@vertex.co
                    hit,normal,_,_=surface.ray_cast(Vector((point.x,origin_y,point.z)),Vector((0,1,0)))
                    if hit is not None:
                        thickness=.012+.018*(hi-point.y)/max(1e-6,hi-lo)
                        if obj.name.startswith('Eye highlight'):thickness+=.028
                        point.y=hit.y-thickness;vertex.co=inverse@point
                obj.data.update()
    else:
        group=[o for name in ('Large goggle housing','Goggle metal rim','Black goggle lens','Goggle glint') for o in named(name)]
        lens=named('Black goggle lens')
        forward(group,min(ys(named('Mole muzzle')))-.035-max(ys(lens)))
    barrel.rotation_euler=previous
    bpy.context.view_layer.update()
    return root
