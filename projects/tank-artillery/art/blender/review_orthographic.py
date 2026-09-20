"""Render genuine front/side/top views and pack matching art/model boards in Blender."""
import bpy,math,sys,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/orthographic-review';OUT.mkdir(parents=True,exist_ok=True)
(OUT/'index.html').write_text(Path(__file__).with_name('orthographic_overlay.html').read_text())
KINDS=['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','Laser','IonAttacker','Poseidon','SecWind']
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else KINDS
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/tankfall_roster.blend'))
scene=bpy.context.scene
for o in bpy.data.objects:o.hide_render=True
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True;scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.73,.71,.65,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
lights=[]
for pos,power,size in [((4,-7,10),1000,7),((-7,-2,5),500,6),((3,6,7),800,5)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;aim(o,(0,0,1.5));lights.append(o)
bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO'
poses={'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}
metadata=[]
for kind in selected:
    root=bpy.data.objects[kind];oldpos=root.location.copy();oldrot=root.rotation_euler.copy();root.location=(0,0,0);root.rotation_euler=(0,0,0)
    children=list(root.children_recursive)
    for o in [root]+children:o.hide_render=o.name.startswith('Snow')
    barrel=next(o for o in children if o.name.split('.')[0]=='Barrel');previous=barrel.rotation_euler.copy();barrel.rotation_euler[0]=-math.radians(poses.get(kind,0))
    bpy.context.view_layer.update()
    points=[o.matrix_world@v.co for o in children if o.type=='MESH' and not o.hide_render for v in o.data.vertices]
    low=Vector(tuple(min(p[j] for p in points) for j in range(3)));high=Vector(tuple(max(p[j] for p in points) for j in range(3)));center=(low+high)*.5
    camera.data.ortho_scale=max(high-low)*1.12
    folder=OUT/kind;folder.mkdir(exist_ok=True)
    for view,offset in [('front',(0,-20,0)),('side',(-20,0,0)),('top',(0,0,20))]:
        camera.location=center+Vector(offset);aim(camera,center)
        if view=='top':camera.rotation_euler=(0,0,-math.pi/2 if kind=='Missile' else 0)
        scene.render.filepath=str(folder/(view+'.png'));bpy.ops.render.render(write_still=True)
        print('ORTHOGRAPHIC_RENDER',kind,view,flush=True)
    metadata.append({'kind':kind,'dimensions':list(high-low),'pitch':poses.get(kind,0),'scale':camera.data.ortho_scale})
    for o in [root]+children:o.hide_render=True
    root.location=oldpos;root.rotation_euler=oldrot;barrel.rotation_euler=previous
previous=json.loads((OUT/'render-metadata.json').read_text()) if (OUT/'render-metadata.json').exists() else []
merged={r['kind']:r for r in previous};merged.update({r['kind']:r for r in metadata})
(OUT/'render-metadata.json').write_text(json.dumps(list(merged.values()),indent=2))

# Native Blender comparison board. Images are genuine renders; editable roster remains in file.
for o in lights+[camera]:o.hide_render=True
scene.render.engine='CYCLES';scene.cycles.samples=1;scene.view_settings.view_transform='Standard';scene.render.film_transparent=False
scene.render.resolution_x=1440;scene.render.resolution_y=1120
boards=bpy.data.collections.new('ORTHOGRAPHIC REVIEW BOARDS');scene.collection.children.link(boards)
def board_image(name,path,x,z,width):
    img=bpy.data.images.load(str(path),check_existing=True);img.pack();height=width*img.size[1]/img.size[0]
    m=bpy.data.materials.new(name);m.use_nodes=True;nodes=m.node_tree.nodes;nodes.clear()
    tex=nodes.new('ShaderNodeTexImage');tex.image=img;em=nodes.new('ShaderNodeEmission');out=nodes.new('ShaderNodeOutputMaterial');mix=nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.93,.91,.85,1);m.node_tree.links.new(tex.outputs['Alpha'],mix.inputs[0]);m.node_tree.links.new(tex.outputs['Color'],mix.inputs[2]);m.node_tree.links.new(mix.outputs[0],em.inputs[0]);m.node_tree.links.new(em.outputs[0],out.inputs[0])
    bpy.ops.mesh.primitive_plane_add(size=1,location=(x,0,z),rotation=(math.pi/2,0,0));o=bpy.context.object;o.name=name;o.scale=(width,height,1);o.data.materials.append(m)
    for c in list(o.users_collection):c.objects.unlink(o)
    boards.objects.link(o);return o
board_objects=[]
for i,kind in enumerate(KINDS):
    folder=OUT/kind
    if not (folder/'top.png').exists():continue
    x=i*16
    board_objects.append(board_image(kind+' concept — FRONT SIDE TOP',ROOT/'art/concepts/orthographic-v1'/f'{kind}.png',x,3.0,12))
    for j,view in enumerate(('front','side','top')):board_objects.append(board_image(kind+' actual mesh '+view,folder/(view+'.png'),x+(j-1)*4,-2.20,3.9))
    bpy.ops.object.camera_add(location=(x,-20,.60));cam=bpy.context.object;cam.name=kind+' Review Camera';cam.data.type='ORTHO';cam.data.ortho_scale=13.2;aim(cam,(x,0,.60))
    if kind in selected:
        scene.camera=cam;scene.render.filepath=str(folder/'board.png');bpy.ops.render.render(write_still=True)
for o in bpy.data.objects:
    if o.type=='MESH' and o not in board_objects:o.hide_set(True)
scene.camera=bpy.data.objects.get('CrossBow Review Camera')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/blender/orthographic_review.blend'))
print('ORTHOGRAPHIC_REVIEW_SAVED',flush=True)
