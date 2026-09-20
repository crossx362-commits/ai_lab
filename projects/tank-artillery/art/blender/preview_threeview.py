"""Stage actual three-axis edits without writing any game model or accepted source."""
import bpy,math,json,sys,importlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'output/threeview-correction';REG=json.loads((ROOT/'art/blender/threeview-registration.json').read_text());POSE={'Cannon':12,'Carrot':10,'MineLander':55,'Missile':20,'MultiMissile':10,'Poseidon':20}
KINDS=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(REG)
for kind in KINDS:
 # Locked image empties are intentionally unselectable, so select-all/delete is
 # insufficient between characters. Start a fresh scene before placing sources.
 bpy.ops.wm.read_factory_settings(use_empty=False)
 script=ROOT/'art/blender/build_models.py';ns={'__file__':str(script),'__name__':'threeview_stage'};exec(compile(script.read_text().split('models=[]; stats=[]; libraries=[]')[0],str(script),'exec'),ns);ns['SKIP_THREEVIEW_REFINEMENT']=True
 root=ns['tank'](kind);scene=bpy.context.scene
 # Place immutable three-axis source images BEFORE editing this model.
 for view,delta,rot in [('front',(0,-10,0),(math.pi/2,0,0)),('side',(-10,0,0),(math.pi/2,0,-math.pi/2)),('top',(0,0,10),(0,0,-math.pi/2 if kind=='Missile' else 0))]:
  rec=REG[kind][view];img=bpy.data.images.load(str(OUT/'reference'/kind/(view+'-reference.png')));img.pack();o=bpy.data.objects.new('ORIGINAL '+view.upper(),None);scene.collection.objects.link(o);o.empty_display_type='IMAGE';o.data=img;o.location=Vector(rec['center'])+Vector(delta);o.rotation_euler=rot;o.empty_display_size=rec['ortho_scale'];o.color=(1,1,1,.45);o.use_empty_image_alpha=True;o.empty_image_depth='FRONT';o.show_empty_image_perspective=False;o.show_empty_image_orthographic=True;o.show_empty_image_only_axis_aligned=True;o.hide_render=True;o.hide_select=True
 references=[o for o in scene.objects if o.type=='EMPTY' and o.empty_display_type=='IMAGE' and o.name.startswith('ORIGINAL ')]
 assert len(references)==3,kind+' must have exactly its own three source planes'
 for o in references:o['source_kind']=kind
 print('SOURCE_PLANES_BEFORE_EDIT',kind,len(references),flush=True)
 controls={o.name:o.matrix_basis.copy() for o in root.children_recursive if o.name.split('.')[0] in ('Turret','Barrel','FirePoint')}
 for o in list(root.children_recursive):
  if o.type=='MESH' and o.data.materials and (o.data.materials[0].name.endswith('Decal') or o.data.materials[0].name=='Team'):bpy.data.objects.remove(o,do_unlink=True)
 letter='a' if kind in ('Catapult','CrossBow','Cannon','Carrot') else 'b' if kind in ('Duke','MineLander','Missile','MultiMissile') else 'c';importlib.import_module('refine_threeview_'+letter).apply(kind,root,ns);bpy.context.view_layer.update()
 for name,before in controls.items():
  after=bpy.data.objects[name].matrix_basis
  assert all(abs(before[i][j]-after[i][j])<1e-6 for i in range(4) for j in range(4)),kind+' changed control '+name
 scene['threeview_source_before_edit']=True;scene['control_basis_unchanged']=True;scene['art_acceptance']='NOT_ACCEPTED';scene['source_registration']='art/blender/threeview-registration.json'
 from roster_refinement import team_badges
 team_badges(kind,root,ns)
 from roster_decals import apply as decals
 decals(kind,root,ns)
 color=ns['linear'](ns['COLORS'][ns['KINDS'].index(kind)]);mat=ns['MATS']['Body'];mat.diffuse_color=(*color,1);mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
 if mat.node_tree.nodes.get('Authored paint'):mat.node_tree.nodes['Authored paint'].outputs[0].default_value=(*color,1)
 col=bpy.data.collections.new(kind);scene.collection.children.link(col)
 for o in [root]+list(root.children_recursive):
  for old in list(o.users_collection):old.objects.unlink(o)
  col.objects.link(o)
 barrel=next(o for o in root.children_recursive if o.name.split('.')[0]=='Barrel');barrel.rotation_euler.x=-math.radians(POSE.get(kind,0));bpy.context.view_layer.update()
 scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.world.use_nodes=True;bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.73,.71,.65,1);bg.inputs[1].default_value=.7
 for pos,power,size in [((4,-7,10),1000,7),((-7,-2,5),500,6),((3,6,7),800,5)]:
  bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=size;o.rotation_euler=(Vector((0,0,1.5))-o.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';folder=OUT/'review'/kind;folder.mkdir(parents=True,exist_ok=True)
 for view,delta in [('front',(0,-20,0)),('side',(-20,0,0)),('top',(0,0,20))]:
  rec=REG[kind][view];center=Vector(rec['center']);cam.data.ortho_scale=rec['ortho_scale'];cam.location=center+Vector(delta);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  if view=='top':cam.rotation_euler=(0,0,-math.pi/2 if kind=='Missile' else 0)
  scene.render.filepath=str(folder/(view+'.png'));bpy.ops.render.render(write_still=True);print('THREEVIEW_RENDER',kind,view,flush=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'proposed.blend'));print('THREEVIEW_STAGE_PASS',kind,flush=True)
