"""Render actual editable Blender models individually for honest concept comparison."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/tankfall_roster.blend'))
OUT=ROOT/'output/native-models/comparison'; OUT.mkdir(parents=True,exist_ok=True)
kinds=['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','Laser','IonAttacker','Poseidon','SecWind']
if '--' in sys.argv:kinds=[k for k in kinds if k in sys.argv[sys.argv.index('--')+1:]]
for o in bpy.data.objects: o.hide_render=True
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=True
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.world.color=(.16,.16,.16);scene.view_settings.view_transform='AgX'
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
for pos,power,size in [((3,-6,11),1150,6),((-8,-2,6),650,7),((3,6,8),1200,5)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,1))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.12));floor=bpy.context.object
mat=bpy.data.materials.new('Comparison ivory studio');mat.diffuse_color=(.72,.68,.59,1);mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.72,.68,.59,1);floor.data.materials.append(mat)
for kind in kinds:
    root=bpy.data.objects[kind];root.location=(0,0,0);root.rotation_euler=(0,0,0)
    for o in [root]+list(root.children_recursive):o.hide_render=o.name.startswith('Snow')
    for o in root.children_recursive:
        if o.name=='Barrel' or o.name.startswith('Barrel.') :o.rotation_euler[0]=math.radians(-{'Missile':28,'Cannon':18,'Carrot':15,'MultiMissile':18,'SuperTank':5,'Laser':0}.get(kind,12))
    bpy.context.view_layer.update()
    points=[o.matrix_world@Vector(p) for o in root.children_recursive if o.type=='MESH' and not o.hide_render for p in o.bound_box]
    center=Vector(tuple((min(p[i] for p in points)+max(p[i] for p in points))*.5 for i in range(3)))
    cam.location=center+Vector((-12,-9,3.2) if kind=='Catapult' else (-12,-9,4.8));aim(cam,center);bpy.context.view_layer.update()
    local=[cam.matrix_world.inverted()@p for p in points]
    width=max(p.x for p in local)-min(p.x for p in local);height=max(p.y for p in local)-min(p.y for p in local)
    cam.data.ortho_scale=max(width,height)*1.10
    scene.render.filepath=str(OUT/(kind+'.png'));bpy.ops.render.render(write_still=True)
    for o in [root]+list(root.children_recursive):o.hide_render=True
    print('CONCEPT_COMPARISON_RENDER',kind,flush=True)
