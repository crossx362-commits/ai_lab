"""Per-character surface UV decals. Separate meshes/materials keep team paint intact."""
from pathlib import Path
import bpy
from mathutils import Vector
# prefix, atlas tile, projection. Normal directions are world-space at authored rest.
RULES={
 'Catapult':[('Oak end face',2,'front'),('Oak end face',2,'back'),('Longitudinal oak stave',2,'top'),('Sloping carved oak eyebrow',1,'front'),('Deep leather sling',1,'back')],
 'CrossBow':[('Overlapping owl leaf',3,'front'),('Rear owl overlapping leaf',3,'back'),('Pointed owl brow leaf',3,'front'),('Central owl forehead leaf',3,'front'),('Teal wheel arch',0,'front'),('Flat laminated recurved oak limb',2,'front'),('Low curved owl carriage',2,'front')],
 'Cannon':[('Great spherical navy cannon',1,'front'),('Great spherical navy cannon',4,'back'),('Navy round hatch',0,'top'),('Wooden carriage cheek',2,'side'),('Compact wooden gun carriage',2,'front')],
 'Carrot':[('Smooth tapered carrot armor',9,'front'),('Smooth tapered carrot armor',9,'back'),('Broad folded carrot crown leaf',3,'front'),('Broad folded carrot crown leaf',3,'back'),('Wrapped curved fender',0,'front')],
 'Duke':[('Faceted frog chin',1,'front'),('Frog low armored back',5,'back'),('Faceted broad frog snout',15,'front'),('Toxic warning triangle',5,'front'),('Wrapped curved fender',0,'front')],
 'MineLander':[('Rounded mole armored cab',0,'front'),('Rounded mole armored cab',13,'back'),('Concave steel digging scoop',13,'front'),('Mole muzzle',1,'front'),('Wrapped curved fender',0,'front')],
 'Missile':[('Red rocket shell',9,'front'),('Rocket rounded tail',9,'back'),('Compact centered launch cradle',0,'front'),('Curved armored track fender',0,'front')],
 'MultiMissile':[('Low tiled turtle shell',6,'back'),('Low tiled turtle shell',6,'top'),('Nine cell dorsal rocket pack',0,'front'),('Nine cell dorsal rocket pack',6,'back'),('Turtle wheel boot',0,'front'),('Turtle forward head',15,'front'),('Turtle belly',9,'front')],
 'SuperTank':[('Thick sloped frontal armor',0,'front'),('Golden shoulder launcher',0,'front'),('Broad lion lower hull',7,'back'),('Swept sculpted lion mane',11,'back'),('Wrapped curved fender',0,'front')],
 'IonAttacker':[('Pink orbital sphere',10,'front'),('Pink orbital sphere',10,'back'),('Pink satellite eye shell',10,'back'),('Pink top cap',0,'top'),('Lower maroon keel',1,'front')],
 'Poseidon':[('Blue whale upper shell',12,'back'),('Blue whale upper shell',1,'front'),('Whale side flipper',1,'front'),('Horizontal whale tail fluke',12,'top'),('Wrapped curved fender',0,'front')],
 'SecWind':[('Green bird rounded head',3,'back'),('Swept green primary wing',11,'front'),('Ivory overlapping secondary',11,'front'),('Swept bird crest',3,'front'),('High wing-root green turbine',0,'back'),('Wrapped curved fender',0,'front')]
}
def apply(kind,root,api):
 if kind not in RULES:return
 image=bpy.data.images.load(str(Path(__file__).resolve().parents[1]/'textures'/f'{kind}ArmorDecal.png'),check_existing=True);image.pack()
 mat=bpy.data.materials.new(kind+'Decal');mat.use_nodes=True
 nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
 tex=nodes.new('ShaderNodeTexImage');tex.image=image
 transparent=nodes.new('ShaderNodeBsdfTransparent');ink=nodes.new('ShaderNodeEmission');ink.inputs['Strength'].default_value=1
 mix=nodes.new('ShaderNodeMixShader');output=nodes.new('ShaderNodeOutputMaterial')
 links.new(tex.outputs['Color'],ink.inputs[0]);links.new(tex.outputs['Alpha'],mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(ink.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
 api['MATS'][kind+'Decal']=mat
 sources=[o for o in root.children_recursive if o.type=='MESH']
 count=0;stats=[]
 for prefix,tile,view in RULES[kind]:
  for source in sources:
   if not source.name.startswith(prefix):continue
   direction=Vector({'front':(0,-1,0),'back':(0,1,0),'top':(0,0,1),'side':(-1,0,0)}[view])
   matrix=source.matrix_world;normal_matrix=matrix.to_3x3().inverted().transposed()
   points=[matrix@v.co for v in source.data.vertices]
   if not points:continue
   axes=(0,2) if view in ('front','back') else (0,1) if view=='top' else (1,2)
   low=[min(p[axis] for p in points) for axis in axes];high=[max(p[axis] for p in points) for axis in axes]
   if min(high[j]-low[j] for j in (0,1))<.005:continue
   inverse=matrix.inverted();vertices=[];faces=[];coords=[]
   source.data.calc_loop_triangles()
   for tri in source.data.loop_triangles:
    face=source.data.polygons[tri.polygon_index]
    normal=(normal_matrix@face.normal).normalized()
    if normal.dot(direction)<.12:continue
    face_indices=[]
    for vi in tri.vertices:
     wp=points[vi]+normal*.004
     face_indices.append(len(vertices));vertices.append(inverse@wp)
     u=(points[vi][axes[0]]-low[0])/(high[0]-low[0]);v=(points[vi][axes[1]]-low[1])/(high[1]-low[1])
     # Pixel atlas has 32px gutters; repeat is never sampled across a tile.
     coords.append(((tile%4*512+32+u*448)/2048,1-(tile//4*512+32+(1-v)*448)/2048))
    faces.append(tuple(face_indices))
   if not faces:continue
   data=bpy.data.meshes.new(kind+' UV '+source.name);data.from_pydata(vertices,[],faces);data.update();data.materials.append(mat)
   uv=data.uv_layers.new(name='ArmorAtlasUV')
   for loop in data.loops:uv.data[loop.index].uv=coords[loop.vertex_index]
   obj=bpy.data.objects.new('UV decal '+source.name+' '+view,data);bpy.context.collection.objects.link(obj)
   obj.parent=source.parent;obj.matrix_local=source.matrix_local.copy();obj['decalSource']=source.name;obj['atlasTile']=tile
   count+=1;stats.append((source.name,view,len(faces)))
 if not count:raise RuntimeError('No decal surfaces for '+kind)
 root['decalSurfaceCount']=count;root['decalAtlas']=image.name
 print('ROSTER_DECAL_APPLIED',kind,count,sum(s[2] for s in stats),flush=True)
