"""Fixed pre-edit cameras for original art / editable mesh / 50% overlay review."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/roster-approved-method';CROPS=json.loads((ROOT/'art/concepts/orthographic-v1/reference-crops.json').read_text())
KINDS=['Catapult','CrossBow','Cannon','Carrot','Duke','MineLander','Missile','MultiMissile','SuperTank','IonAttacker','Poseidon','SecWind']
if '--' in sys.argv:KINDS=sys.argv[sys.argv.index('--')+1:]
POSE={'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}
regfile=OUT/'fixed-registration.json';registrations=json.loads(regfile.read_text()) if regfile.exists() else {}
def prepare_root(kind):
 root=bpy.data.objects[kind];root.location=(0,0,0);root.rotation_euler=(0,0,0)
 barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel');barrel.rotation_euler.x=-math.radians(POSE.get(kind,0));bpy.context.view_layer.update();return root
for kind in KINDS:
 if kind not in registrations:
  bpy.ops.wm.open_mainfile(filepath=str(OUT/'baseline'/kind/'model.blend'));root=prepare_root(kind)
  points=[o.matrix_world@v.co for o in root.children_recursive if o.type=='MESH' for v in o.data.vertices]
  low=Vector([min(p[j] for p in points) for j in range(3)]);high=Vector([max(p[j] for p in points) for j in range(3)]);center=(high+low)*.5
  views={}
  for index,view in enumerate(['front','side','top']):
   _,_,w,h=CROPS[kind]['boxes'][index]
   height=(high-low).y if view=='top' else (high-low).z
   if kind=='Missile' and view=='top':height=(high-low).x
   views[view]={'center':list(center),'ortho_scale':height*max(1,w/h)*1.16,'units_per_reference_pixel':height/h,'reference_crop':CROPS[kind]['boxes'][index]}
  registrations[kind]=views;regfile.write_text(json.dumps(registrations,indent=2))
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/characters'/f'{kind}.blend'));root=prepare_root(kind);scene=bpy.context.scene
 scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.world.use_nodes=True;bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.73,.71,.65,1);bg.inputs[1].default_value=.7
 for pos,power,size in [((4,-7,10),1000,7),((-7,-2,5),500,6),((3,6,7),800,5)]:
  bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=size;o.rotation_euler=(Vector((0,0,1.5))-o.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';folder=OUT/'fixed-review'/kind;folder.mkdir(parents=True,exist_ok=True)
 for view,delta in [('front',(0,-20,0)),('side',(-20,0,0)),('top',(0,0,20))]:
  rec=registrations[kind][view];center=Vector(rec['center']);cam.data.ortho_scale=rec['ortho_scale'];cam.location=center+Vector(delta);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  if view=='top':cam.rotation_euler=(0,0,-math.pi/2 if kind=='Missile' else 0)
  scene.render.filepath=str(folder/f'{view}.png');bpy.ops.render.render(write_still=True)
  print('FIXED_ROSTER_RENDER',kind,view,flush=True)
print('FIXED_ROSTER_REVIEW_PASS',flush=True)
