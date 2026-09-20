"""Three live orthographic source planes per editable character scene; no hidden side/top."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/threeview-correction';REG=json.loads((ROOT/'art/blender/threeview-registration.json').read_text());POSE={'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}
bpy.ops.wm.read_factory_settings(use_empty=True)
first=None;records=[]
for kind,views in REG.items():
 scene=bpy.data.scenes.new(kind+' — FRONT SIDE TOP');bpy.context.window.scene=scene
 if first is None:first=scene
 source=OUT/'review'/kind/'proposed.blend'
 with bpy.data.libraries.load(str(source),link=False) as (src,dst):dst.collections=[kind]
 col=dst.collections[0];scene.collection.children.link(col);root=next(o for o in col.objects if o.parent is None);root.location=(0,0,0);root.rotation_euler=(0,0,0)
 barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel');barrel.rotation_euler.x=-math.radians(POSE.get(kind,0))
 refs=bpy.data.collections.new(kind+' ORIGINAL THREE AXES');scene.collection.children.link(refs)
 for view,delta,rot in [('front',(0,-10,0),(math.pi/2,0,0)),('side',(-10,0,0),(math.pi/2,0,-math.pi/2)),('top',(0,0,10),(0,0,-math.pi/2 if kind=='Missile' else 0))]:
  rec=views[view];img=bpy.data.images.load(str(OUT/'reference'/kind/(view+'-reference.png')),check_existing=True);img.pack()
  obj=bpy.data.objects.new('ORIGINAL '+view.upper()+' '+kind,None);refs.objects.link(obj);obj.empty_display_type='IMAGE';obj.data=img
  obj.location=Vector(rec['center'])+Vector(delta);obj.rotation_euler=rot;obj.empty_display_size=rec['ortho_scale'];obj.color=(1,1,1,.45);obj.use_empty_image_alpha=True;obj.empty_image_depth='FRONT';obj.show_empty_image_perspective=False;obj.show_empty_image_orthographic=True;obj.show_empty_image_only_axis_aligned=True;obj.hide_render=True;obj.hide_select=True
  obj['reference_role']='Active source geometry constraint; original pixels at fixed registration'
  data=bpy.data.cameras.new(kind+' '+view+' orthographic');cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=obj.location;cam.rotation_euler=(Vector(rec['center'])-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=rec['ortho_scale']
  if view=='top':cam.rotation_euler=rot
  if view=='front':scene.camera=cam
 scene['reference_views']='front,side,top — all active';scene['art_acceptance']='NOT_ACCEPTED';scene['game_deployed']=False;records.append({'kind':kind,'reference_planes':3,'source':str(source),'fixed_registration':views,'art_acceptance':'NOT_ACCEPTED','game_deployed':False})
bpy.context.window.scene=first
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2);sp.region_3d.view_perspective='ORTHO';sp.region_3d.view_location=(0,0,1.5);sp.region_3d.view_distance=6
   sp.shading.type='MATERIAL';sp.overlay.show_floor=False;sp.overlay.show_axis_x=False;sp.overlay.show_axis_y=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'three_axes_workbench.blend'))
(OUT/'three-axes-manifest.json').write_text(json.dumps(records,indent=2));print('THREE_AXES_WORKBENCH',len(records),'kinds',len(records)*3,'active reference planes')
