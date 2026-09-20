"""Blender 저장 원본에서 모델 비교 시트 렌더. --python render_models.py"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
bpy.context.preferences.filepaths.save_version=0
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'art/blender'
RENDER=ROOT/'output/blender'
RENDER.mkdir(parents=True,exist_ok=True)
if not globals().get('models'):
    bpy.ops.wm.open_mainfile(filepath=str(ART/'tankfall_roster.blend'))
    KINDS=['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','Laser','IonAttacker','Poseidon','SecWind']
    COLORS=[(0.54, 0.31, 0.15), (0.12, 0.46, 0.4), (0.15, 0.2, 0.32), (0.93, 0.37, 0.085), (0.39, 0.55, 0.15), (0.88, 0.58, 0.19), (0.9, 0.23, 0.075), (0.1, 0.53, 0.57), (0.88, 0.61, 0.2), (0.46, 0.22, 0.64), (0.83, 0.27, 0.53), (0.14, 0.55, 0.77), (0.48, 0.66, 0.19)]
    models=[bpy.data.objects[k+suffix] for k in KINDS for suffix in ('','_Normal','_Special')]
    MATS={n:bpy.data.materials[n] for n in ('Body','Rubber','Ivory')}
def linear(c): return tuple(x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in c)
def v(p): return Vector((p[0],-p[2],p[1]))
def visibility(root,visible):
    root.hide_render=not visible
    for ch in root.children_recursive: ch.hide_render=not visible or ch.name.startswith('Snow')
def box(n,p,s,mat,parent,**kw):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p)); o=bpy.context.object; o.name=n; o.dimensions=(s[0],s[2],s[1]); o.data.materials.append(MATS[mat]); return o
# Source/FBX retain the individual kind paint, while runtime uses its existing palette.
for i,k in enumerate(KINDS):
    paint=MATS['Body'].copy(); paint.name=k+' paint'; paint.diffuse_color=(*linear(COLORS[i]),1)
    paint.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*linear(COLORS[i]),1)
    if paint.node_tree.nodes.get('Authored paint'):paint.node_tree.nodes['Authored paint'].outputs[0].default_value=(*linear(COLORS[i]),1)
    for j in range(3):
        root=models[i*3+j]
        for ch in root.children_recursive:
            if ch.type=='MESH' and ch.data.materials[0].name.split('.')[0]=='Body': ch.data.materials[0]=paint
        root.location=(0,0,0); root.rotation_euler=(0,0,0); visibility(root,True)
        bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
        for ch in root.children_recursive: ch.select_set(True)
        bpy.context.view_layer.objects.active=root
        bpy.ops.export_scene.fbx(filepath=str(ART/'exports'/(root.name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
        x=(i%4-1.5)*10; z=(1.5-i//4)*11
        root.location=v((x if j==0 else x+(j-1.5)*1.8,0 if j==0 else 1,z if j==0 else z-3.3))
# Keep the editable source framed as a roster on opening.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=48
            area.spaces.active.region_3d.view_location=(0,0,1)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'tankfall_roster.blend'))
for root in models: visibility(root,False)


scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=20
scene.render.resolution_x=1800; scene.render.resolution_y=1600; scene.render.resolution_percentage=100
scene.world.color=(.16,.16,.16)
scene.view_settings.view_transform='AgX'
bpy.ops.object.camera_add(); cam=bpy.context.object; scene.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=40

def point(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in (((-10,-14,25),4200,16),((14,-2,18),3000,12),((0,15,20),3800,10)):
    bpy.ops.object.light_add(type='AREA',location=pos); light=bpy.context.object; light.data.energy=power; light.data.shape='DISK'; light.data.size=size; point(light,(0,0,0))
box('Studio floor',(0,-.18,0),(100,.2,100),'Rubber',None)
labels=[]
def text_label(body,pos,size=.36):
    curve=bpy.data.curves.new('Label','FONT'); curve.body=body; curve.size=size; curve.align_x='CENTER'; curve.extrude=0
    obj=bpy.data.objects.new('Label',curve); scene.collection.objects.link(obj); obj.location=pos; obj.rotation_euler=(0,0,0); obj.data.materials.append(MATS['Ivory']); labels.append(obj)
roles=['SIEGE / FIRE','PRECISION / VENOM','BLAST / BURST','BALANCED / CLUSTER','ARMOR / GAS','DURABILITY / MINES','ROCKET / SEEKER','SALVO / NINE-CELL','ELITE / GUIDED','LOW ARC / LANCE','ION / ORBITAL','WATER / BIND','SPEED / OVERDRIVE']
for i,k in enumerate(KINDS):
    root=models[i*3]; visibility(root,True); x=(i%4-1.5)*9.2; z=(1.5-i//4)*9.0; root.location=v((x,0,z)); root.rotation_euler[2]=math.radians(-27)
    mat=MATS['Body'].copy(); mat.name=k+' paint'; mat.diffuse_color=(*linear(COLORS[i]),1); mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*linear(COLORS[i]),1)
    for ch in root.children_recursive:
        if ch.type=='MESH' and ch.data.materials[0]==MATS['Body']: ch.data.materials[0]=mat
    text_label(k.upper(),(x,-z+3.3,.04),.46); text_label(roles[i],(x,-z+3.9,.04),.24)
cam.location=(0,-26,42); point(cam,(0,0,0)); cam.data.ortho_scale=40
scene.render.filepath=str(RENDER/'tank_roster.png'); bpy.ops.render.render(write_still=True)
for o in models: visibility(o,False); o.location=(0,0,0); o.rotation_euler=(0,0,0)
for obj in labels: bpy.data.objects.remove(obj,do_unlink=True)
labels=[]
scene.render.resolution_x=2000; scene.render.resolution_y=1450
for i,k in enumerate(KINDS):
    col=i%7; row=i//7; x=(col-3)*4.4; z=(.5-row)*9
    for s in range(2):
        root=models[i*3+1+s]; visibility(root,True); root.location=v((x+(s-.5)*1.6,1,z)); root.rotation_euler[2]=math.radians(-25)
    text_label(k.upper(),(x,-z+2.0,.04),.28)
    text_label('NORMAL   /   SPECIAL',(x,-z+2.6,.04),.20)
cam.location=(0,-18,30); point(cam,(0,0,0)); cam.data.ortho_scale=33
scene.render.filepath=str(RENDER/'projectile_roster.png'); bpy.ops.render.render(write_still=True)
print('BLENDER_RENDER_PASS',len(models),'models',flush=True)
