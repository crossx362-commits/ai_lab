"""Render against fixed image-plane registration; never refit to current mesh bounds."""
import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/laser-position-fit';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/characters/Laser.blend'))
scene=bpy.context.scene;root=bpy.data.objects['Laser'];root.location=(0,0,0);root.rotation_euler=(0,0,0)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.resolution_percentage=100
scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.73,.71,.65,1);bg.inputs[1].default_value=.7
for pos,power,size in [((4,-7,10),1000,7),((-7,-2,5),500,6),((3,6,7),800,5)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=size;o.rotation_euler=(Vector((0,0,1.5))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
views=[('front',(0,-10,1.423),(0,0,1.423),3.4,555,360),('side',(-10,-.60,1.423),(0,-.60,1.423),3.76,564,333),('top',(0,-.393,10),(0,-.393,0),4.426,543,726)]
for name,pos,target,scale,w,h in views:
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 if name=='top':cam.rotation_euler=(0,0,0)
 cam.data.ortho_scale=scale;scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'-fixed.png'));bpy.ops.render.render(write_still=True)
# Eye visibility mask, retaining all occluders.
def emission(name,color):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();a=n.new('ShaderNodeEmission');a.inputs[0].default_value=(*color,1);o=n.new('ShaderNodeOutputMaterial');m.node_tree.links.new(a.outputs[0],o.inputs[0]);return m
white=emission('Eye metric white',(1,1,1));black=emission('Occluder metric black',(0,0,0))
cam.location=(0,-10,1.423);cam.rotation_euler=(math.pi/2,0,0);cam.data.ortho_scale=3.4;scene.render.resolution_x=555;scene.render.resolution_y=360
scene.cycles.samples=16;scene.cycles.use_denoising=False
for label,terms in [('eye',['traced cyan visor eye']),('fins',['registered lateral fin','registered central fin']),('thrusters',['Cyan lift disc']),('helmet',['registered helmet']),('shoulders',['registered shoulder']),('blades',['registered launch blade'])]:
 for o in root.children_recursive:
  if o.type in ('MESH','CURVE'):
   if o.name.startswith('UV decal '):
    o.hide_render=True; continue  # transparent ink does not define a silhouette
   o.data.materials.clear();o.data.materials.append(white if any(t in o.name for t in terms) else black)
 scene.render.filepath=str(OUT/('front-'+label+'-mask.png'));bpy.ops.render.render(write_still=True)
print('FIXED_PROJECTION_RENDER_PASS')
