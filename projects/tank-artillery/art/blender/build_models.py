"""Blender 5.x: TANKFALL 모델 원본, FBX, Unity 메시 데이터와 비교 렌더 생성.
실행: Blender --background --python art/blender/build_models.py
좌표 입력은 게임 좌표(X 오른쪽/Y 위/Z 전방), Blender 내부에서는 Z-up.
게임 수치 원본 TankShape에서 크기와 회전축을 읽고 조형은 여기에서 만든다.
"""
import bpy, math, json, re, sys
from pathlib import Path
from mathutils import Vector
bpy.context.preferences.filepaths.save_version=0

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'unity/Assets/_Project/Resources/BlenderModels'
ART = ROOT / 'art/blender'
sys.path.insert(0,str(ART))
from mesh_pack import pack
RENDER = ROOT / 'output/blender'
for p in (OUT, ART / 'exports', RENDER): p.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for d in list(bpy.data.materials): bpy.data.materials.remove(d)
KINDS = ['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','Laser','IonAttacker','Poseidon','SecWind']
COLORS = [(0.54, 0.31, 0.15), (0.12, 0.46, 0.4), (0.15, 0.2, 0.32), (0.93, 0.37, 0.085), (0.39, 0.55, 0.15), (0.88, 0.58, 0.19), (0.9, 0.23, 0.075), (0.1, 0.53, 0.57), (0.88, 0.61, 0.2), (0.46, 0.22, 0.64), (0.83, 0.27, 0.53), (0.14, 0.55, 0.77), (0.48, 0.66, 0.19)]
PALETTE = {'Body':(.65,.52,.28),'Team':(.18,.52,.94),'Rubber':(.065,.085,.105),'Metal':(.22,.28,.33),'Edge':(.58,.66,.69),'Wood':(.34,.17,.07),'WoodLight':(.64,.39,.17),'Ivory':(.91,.88,.72),'Glass':(.1,.3,.36),'Energy':(.15,.95,.8),'Poison':(.48,.88,.13),'Hot':(1,.22,.035),'Snow':(.91,.96,1),'EyeWhite':(1,1,1)}
PALETTE.update({'GlassBottle':(.68,.94,.76),'LeafGreen':(.29,.48,.10),'Stone':(.44,.415,.38),'Rocket':(.92,.23,.075),'Crimson':(.55,.12,.16),'Teal':(.08,.43,.43),'Navy':(.12,.19,.34),'Gold':(.88,.57,.16),'Plum':(.38,.18,.43)})
CUTE=False
def linear(c): return tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in c)
MATS={}
for n,c in PALETTE.items():
    m=bpy.data.materials.new(n); m.diffuse_color=(*linear(c),1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*linear(c),1); bs.inputs['Roughness'].default_value=.73 if n in ('Wood','WoodLight','Rubber') else .22 if n in ('EyeWhite','Glass') else .56
    bs.inputs['Metallic'].default_value=.45 if n in ('Metal','Edge','Gold') else .02
    if n in ('Energy','Hot','Poison'):
        bs.inputs['Emission Color'].default_value=(*linear(c),1); bs.inputs['Emission Strength'].default_value=.6
    if n not in ('EyeWhite','Energy','Hot','Poison','Glass','Snow'):
        nodes=m.node_tree.nodes;links=m.node_tree.links
        rgb=nodes.new('ShaderNodeRGB');rgb.name='Authored paint';rgb.outputs[0].default_value=(*linear(c),1)
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=12;noise.inputs['Detail'].default_value=3
        ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.76,.76,.76,1);ramp.color_ramp.elements[1].color=(1.04,1.04,1.04,1)
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
        links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(rgb.outputs[0],mix.inputs[1]);links.new(ramp.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],bs.inputs['Base Color'])
    if n=='GlassBottle':
        bs.inputs['Base Color'].default_value=(*linear(c),.16);bs.inputs['Alpha'].default_value=.16;bs.inputs['Roughness'].default_value=.13;bs.inputs['Transmission Weight'].default_value=.15
    MATS[n]=m

def v(p): return Vector((p[0],-p[2],p[1]))
def uv(p): return [round(p.x,6),round(p.z,6),round(-p.y,6)]
def empty(n,p=(0,0,0),parent=None):
    o=bpy.data.objects.new(n,None); bpy.context.collection.objects.link(o); o.parent=parent; o.location=v(p); return o

def finish(o,n,mat,parent,bevel=0):
    o.name=n; o.parent=parent
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft machined edges','BEVEL'); mod.width=bevel; mod.segments=4 if CUTE else 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(MATS[mat])
    # Weighted normals keep broad armor faces calm while rounding their edges.
    for p in o.data.polygons: p.use_smooth=True
    mod=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL'); mod.keep_sharp=True; mod.weight=40
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def box(n,p,s,mat,parent,bev=.07,rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p)); o=bpy.context.object; o.dimensions=(s[0],s[2],s[1])
    finish(o,n,mat,parent,min(s)*.34 if CUTE and bev else min(bev,min(s)*.24))
    if rot: o.rotation_euler=tuple(math.radians(a) for a in (rot[0],-rot[2],rot[1]))
    return o

def cyl(n,p,r,length,mat,parent,axis='y',r2=None,seg=16):
    bpy.ops.mesh.primitive_cone_add(vertices=seg,radius1=r,radius2=r if r2 is None else r2,depth=length,location=v(p))
    o=bpy.context.object
    direction=v({'x':(1,0,0),'y':(0,1,0),'z':(0,0,1)}[axis]); o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return finish(o,n,mat,parent,.025)

def ball(n,p,s,mat,parent):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1,location=v(p)); o=bpy.context.object
    o.scale=(s[0],s[2],s[1]); return finish(o,n,mat,parent)

def torus(n,p,r,t,mat,parent,axis='z'):
    bpy.ops.mesh.primitive_torus_add(major_segments=40,minor_segments=8,location=v(p),major_radius=r,minor_radius=t)
    o=bpy.context.object; direction=v({'x':(1,0,0),'y':(0,1,0),'z':(0,0,1)}[axis]); o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return finish(o,n,mat,parent)

def beam(n,a,b,width,mat,parent):
    a,b=Vector(a),Vector(b); mid=(a+b)/2
    o=box(n,mid,(width,width,(b-a).length),mat,parent,.03)
    o.rotation_euler=v(b-a).to_track_quat('-Y','Z').to_euler(); return o

def tube(n,p,r,length,mat,parent,axis='z'):
    # Open muzzle with a dark recessed bore. No closed disc in front of FirePoint.
    c=list(p); j={'x':0,'y':1,'z':2}[axis]
    cyl(n,c,r,length,mat,parent,axis)
    c[j]+=length*.5+.005
    cyl(n+' bore',c,r*.7,.025,'Rubber',parent,axis)
    torus(n+' lip',c,r*.9,r*.12,'Edge',parent,axis)

def wheel(parent,p,r,width,wood=False):
    w=empty('Wheel',p,parent)
    cyl('Tire',(0,0,0),r,width,'WoodLight' if wood else 'Rubber',w,'x',seg=20)
    for side in (-1,1):
        torus('Rim',(side*width*.52,0,0),r*.77,r*.075,'Metal',w,'x')
        cyl('Hub',(side*width*.55,0,0),r*.23,.15,'Team',w,'x')
        for a in range(0,360,60):
            ang=math.radians(a)
            beam('Spoke',(side*width*.55,0,0),(side*width*.55,math.cos(ang)*r*.7,math.sin(ang)*r*.7),r*.12,'Wood' if wood else 'Edge',w)
    return w

# Extract the existing art anchors instead of silently changing muzzle geometry.
src=(ROOT/'unity/Assets/_Project/Scripts/View/ProceduralTank.cs').read_text()
rows=re.findall(r'return Make\(([^;]+)\);',src)[:13]
SHAPES={}
for k,row in zip(KINDS,rows):
    a=[x.strip().replace('f','') for x in row.split(',')]
    SHAPES[k]=[float(x) if x not in ('true','alse') else x for x in a]
assert len(SHAPES)==13

def sculpt(n,p,s,mat,parent,roundness=.65):
    """Superellipsoid surface: broad soft panels without a pile of intersecting spheres."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1,location=v(p))
    o=bpy.context.object
    def power(a): return math.copysign(abs(a)**roundness,a)
    for vert in o.data.vertices:
        q=vert.co
        q.x=power(q.x)*s[0]; q.y=power(q.y)*s[2]; q.z=power(q.z)*s[1]
    o.name=n; o.parent=parent; o.data.materials.append(MATS[mat])
    for face in o.data.polygons: face.use_smooth=True
    return o

def toywheel(parent,p,r,width,paint):
    w=empty('Wheel',p,parent)
    cyl('Rounded tire',(0,0,0),r,width,'Rubber',w,'x',seg=24)
    for side in (-1,1):
        cyl('Enamel wheel',(side*width*.51,0,0),r*.76,.10,paint,w,'x',seg=24)
        torus('Wheel lip',(side*width*.58,0,0),r*.65,r*.06,'Ivory',w,'x')
        cyl('Axle cap',(side*width*.64,0,0),r*.27,.14,'Gold',w,'x',seg=16)
        for a in range(0,360,120):
            q=math.radians(a)
            cyl('Wheel bolt',(side*width*.61,math.sin(q)*r*.46,math.cos(q)*r*.46),r*.075,.035,'Metal',w,'x',seg=8)
    return w

def tank(k):
    from concept_roster import build_tank
    root=build_tank(k,SHAPES[k],globals())
    if k != "Laser" and not globals().get("SKIP_ROSTER_REFINEMENT",False):
        from roster_refinement import apply
        apply(k,root,globals())
    return root

def projectile(k,special):
    root=empty(k+('_Special' if special else '_Normal'))
    hot='Poison' if k in ('CrossBow','Duke') and special else 'Hot' if special else 'Energy'
    if k=='Catapult':
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.52); o=bpy.context.object
        for vert in o.data.vertices: vert.co*=1+.1*math.sin(vert.index*12.9)
        finish(o,'Boulder','Metal',root)
        if special:
            for i in range(7):
                a=i*math.tau/7; ball('Molten seam',(math.sin(a)*.4,math.cos(a)*.4,0),(.12,.12,.45),'Hot',root)
    elif k=='CrossBow':
        cyl('Bolt shaft',(0,0,-.05),.055,1.7,'WoodLight',root,'z')
        cyl('Piercing head',(0,0,.91),.21,.46,'Poison' if special else 'Edge',root,'z',0,4)
        for side in (-1,1): box('Fletching',(side*.14,0,-.65),(.27,.045,.44),'Poison' if special else 'Ivory',root,.01,(0,side*18,0))
        if special: torus('Venom vial',(0,0,.23),.12,.075,'Poison',root)
    elif k=='Cannon':
        ball('Bomb',(0,0,0),(.5,.5,.5),'Hot' if special else 'Rubber',root)
        cyl('Fuse socket',(0,.50,0),.12,.17,'WoodLight',root)
        beam('Fuse',(0,.55,0),(.17,.83,.06),.05,'Ivory',root)
        ball('Spark',(.17,.83,.06),(.09,.09,.09),'Hot',root)
        if special:
            torus('Reinforcing equator',(0,0,0),.50,.055,'Edge',root,'y')
            for x in (-.18,.18): cyl('Charge pin',(x,.43,.1),.06,.2,'Metal',root)
    elif k in ('Missile','MultiMissile','SuperTank'):
        r=.22 if k=='MultiMissile' else .30 if k=='Missile' else .38
        length=1.3 if k=='MultiMissile' else 1.65
        cyl('Rocket body',(0,0,0),r,length,'Ivory' if not special else 'Body',root,'z')
        cyl('Seeker nose',(0,0,length*.5+.19),r,.40,'Hot' if k=='SuperTank' else 'Team',root,'z',.025)
        tube('Engine nozzle',(0,0,-length*.5),r*.68,.24,'Metal',root)
        for a in (0,90,180,270):
            q=math.radians(a)
            o=box('Tail stabilizer',(math.cos(q)*r,math.sin(q)*r,-length*.33),(.12,.50,.44),'Team',root,.02)
            o.rotation_euler[1]=math.radians(-a)
        torus('Identification band',(0,0,.32),r,.035,'WoodLight',root)
        if special:
            for side in (-1,1): box('Guidance wing',(side*(r+.16),0,.3),(.45,.08,.28),'Edge',root,.02,(0,side*25,0))
            ball('Guidance lens',(0,0,length*.5+.4),(.075,.075,.075),'Energy',root)
        if k=='SuperTank':
            for side in (-1,1): cyl('Booster',(side*.38,0,-.38),.13,.88,'Metal',root,'z')
    elif k=='Carrot':
        cyl('Artillery casing',(0,0,-.2),.26,.78,'WoodLight',root,'z')
        cyl('Ogive',(0,0,.38),.26,.42,'Hot' if not special else 'Poison',root,'z',.055)
        for z in (-.5,-.36): torus('Driving band',(0,0,z),.26,.035,'Edge',root)
        if special:
            for a in range(3):
                q=a*math.tau/3; cyl('Cluster chamber',(math.cos(q)*.24,math.sin(q)*.24,.1),.11,.7,'Poison',root,'z')
    elif k=='Duke':
        cyl('Chemical canister',(0,0,0),.32,1.05,'Poison' if special else 'Body',root,'z')
        for z in (-.5,.5): cyl('Sealed end',(0,0,z),.34,.12,'Metal',root,'z')
        for z in (-.28,.28): torus('Safety strap',(0,0,z),.33,.04,'Ivory',root)
        cyl('Valve',(0,0,.64),.10,.2,'Hot',root,'z')
        if special:
            for side in (-1,1): cyl('Dispersion bottle',(side*.36,0,0),.12,.7,'Poison',root,'z')
    elif k=='MineLander':
        cyl('Mine disc',(0,0,0),.50,.20,'Body',root)
        cyl('Pressure plate',(0,.15,0),.34,.10,'Hot' if special else 'Metal',root)
        for a in range(0,360,60):
            q=math.radians(a); cyl('Trigger stud',(math.cos(q)*.40,.13,math.sin(q)*.40),.055,.12,'Edge',root)
        if special:
            for a in range(0,360,90):
                q=math.radians(a); beam('Anchor tooth',(math.cos(q)*.4,0,math.sin(q)*.4),(math.cos(q)*.68,-.14,math.sin(q)*.68),.13,'Metal',root)
    elif k=='Laser':
        cyl('Energy lance',(0,0,.08),.13,1.65,'Energy',root,'z',.035,6)
        for z in (-.5,-.15,.20): torus('Pulse ring',(0,0,z),.22 if special else .16,.035,'Team',root)
        if special:
            for a in (0,120,240):
                q=math.radians(a); cyl('Helical pulse',(math.cos(q)*.18,math.sin(q)*.18,0),.05,1.28,'Hot',root,'z',.015,6)
    elif k=='IonAttacker':
        ball('Plasma core',(0,0,0),(.30,.30,.36),'Energy',root)
        for axis in ('x','y','z'): torus('Containment ring',(0,0,0),.44,.055,'Edge',root,axis)
        if special:
            for side in (-1,1): box('Satellite wing',(side*.60,0,0),(.46,.08,.7),'Team',root,.03)
            cyl('Orbital penetrator',(0,0,.56),.22,.50,'Energy',root,'z',0,6)
    elif k=='Poseidon':
        ball('Water capsule',(0,0,0),(.31,.31,.60),'Energy',root)
        torus('Pressure collar',(0,0,-.27),.29,.065,'Edge',root)
        for side in (-1,1): box('Tail fin',(side*.23,0,-.45),(.38,.06,.4),'Team',root,.02,(0,side*20,0))
        if special:
            for a in range(0,360,90):
                q=math.radians(a); beam('Binding prong',(math.sin(q)*.3,math.cos(q)*.3,-.25),(math.sin(q)*.48,math.cos(q)*.48,.5),.07,'Ivory',root)
            torus('Binding ring',(0,0,.5),.48,.055,'Energy',root)
    else:
        cyl('Rotor hub',(0,0,0),.18,.20,'Metal',root,'z')
        for a in range(0,360,60 if special else 120):
            q=math.radians(a)
            beam('Wind cutter',(math.cos(q)*.15,math.sin(q)*.15,0),(math.cos(q+.60)*.60,math.sin(q+.60)*.60,.08),.14,'Energy',root)
            beam('Blade tip',(math.cos(q+.60)*.60,math.sin(q+.60)*.60,.08),(math.cos(q+1)*.73,math.sin(q+1)*.73,.08),.07,'Edge',root)
        if special: torus('Vortex rim',(0,0,0),.70,.035,'Hot',root)
    return root

# Bake modifiers/transforms in Blender; Unity reads finished triangle meshes, no shape generation.
def bake(root):
    nodes=[]; total=0
    def visit(o,parentidx):
        nonlocal total
        idx=len(nodes); node={'name':o.name.split('.')[0], 'parent':parentidx,'position':uv(o.location),'vertices':[], 'normals':[], 'triangles':[], 'material':''}; nodes.append(node)
        if o.type!='MESH':
            q=o.rotation_euler.to_quaternion();node['rotation']=[round(q.x,7),round(q.z,7),round(-q.y,7),round(q.w,7)];node['scale']=[round(o.scale.x,7),round(o.scale.z,7),round(o.scale.y,7)]
        if o.type=='MESH':
            mesh=o.data; mesh.calc_loop_triangles()
            if mesh.uv_layers.active: node['uv']=[]
            mat=o.matrix_local.copy(); mat.translation=Vector((0,0,0)); nm=mat.to_3x3().inverted().transposed()
            for tri in mesh.loop_triangles:
                for li in tri.loops:
                    lp=mesh.loops[li]; node['vertices']+=uv(mat@mesh.vertices[lp.vertex_index].co)
                    node['normals']+=uv((nm@mesh.corner_normals[li].vector).normalized())
                    if mesh.uv_layers.active: node['uv'] += [round(float(v),7) for v in mesh.uv_layers.active.data[li].uv]
                    node['triangles'].append(len(node['triangles']))
            node['material']=mesh.materials[0].name
            total+=len(node['triangles'])//3
        for ch in o.children: visit(ch,idx)
    bpy.context.view_layer.update(); visit(root,-1)
    result={'version':1,'name':root.name,'wheelRadius':float(root.get('wheelRadius',0)),'hover':bool(root.get('hover',False)),'nodes':nodes}
    (OUT/(root.name+'.json')).write_text(json.dumps(pack(result),separators=(',',':')))
    return total

def export_fbx(root):
    bpy.ops.object.select_all(action='DESELECT')
    def select(o):
        o.select_set(True)
        for c in o.children: select(c)
    select(root); bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(ART/'exports'/(root.name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)

models=[]; stats=[]; libraries=[]
source_cache=RENDER/'source_parts'; source_cache.mkdir(exist_ok=True)
for k in KINDS:
    print('BUILD_START',k,flush=True)
    for o in (tank(k),projectile(k,False),projectile(k,True)):
        triangles=bake(o); export_fbx(o); stats.append({'name':o.name,'triangles':triangles})
        col=bpy.data.collections.new(o.name); bpy.context.scene.collection.children.link(col)
        for obj in [o]+list(o.children_recursive):
            for old in list(obj.users_collection): old.objects.unlink(obj)
            col.objects.link(obj)
        path=source_cache/(o.name+'.blend')
        bpy.data.libraries.write(str(path),{col},fake_user=True,compress=True)
        libraries.append(path)
        print('MODEL_EXPORTED',o.name,triangles,flush=True)
    # A fresh dependency graph per kind prevents quadratic evaluation of the entire roster.
    for obj in list(bpy.data.objects): bpy.data.objects.remove(obj,do_unlink=True)
    for col in list(bpy.data.collections):
        if col.name!='Collection': bpy.data.collections.remove(col)
    for mesh in list(bpy.data.meshes):
        if mesh.users==0: bpy.data.meshes.remove(mesh)
(ART/'manifest.json').write_text(json.dumps({'generator':'Blender '+bpy.app.version_string,'models':stats},indent=2))
for path in libraries:
    with bpy.data.libraries.load(str(path),link=False) as (data_from,data_to): data_to.collections=data_from.collections
    for col in data_to.collections: bpy.context.scene.collection.children.link(col)
models=[bpy.data.objects[k+suffix] for k in KINDS for suffix in ('','_Normal','_Special')]
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'tankfall_roster.blend'))
print('BLENDER_EXPORT_PASS',len(stats),flush=True)
exec(compile((ART/'render_models.py').read_text(),str(ART/'render_models.py'),'exec'))
