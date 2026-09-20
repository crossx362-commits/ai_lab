"""Editable SuperTank bore; preserves control objects and existing barrel exterior.
One material per mesh, compatible with the roster exporter. No scene/output writes.
"""
import bpy
import bmesh
import math
from mathutils import Vector

def apply(root,api):
    if root.get('superBarrelAnnular'):return root
    bpy.context.view_layer.update()
    objects=list(root.children_recursive)
    named=lambda names:[o for o in objects if o.type=='MESH' and o.name.split('.')[0] in names]
    tubes=named(('Cannon tube','Cannon body','Broad muzzle'))
    mouths=named(('Dark open bore','Recessed bore'))
    if not tubes or len(mouths)!=1:raise RuntimeError('SuperTank expected tube meshes and one bore terminus')
    mouth=mouths[0];mouth.data=mouth.data.copy()
    # Original cylinder constructors use local Z as their axial direction. Their
    # matrices carry all pitch/registration offsets; do not rotate or move the rig.
    mp=[v.co.copy() for v in mouth.data.vertices]
    aperture=[(max(p[i] for p in mp)-min(p[i] for p in mp))/2 for i in (0,1)]
    def put(obj,verts,faces,material):
        data=bpy.data.meshes.new(obj.name+' annular solid');data.from_pydata(verts,[],faces);data.materials.append(material)
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free();obj.data=data
        for p in data.polygons:p.use_smooth=True
        data.update()
    lengths=[]
    for obj in tubes:
        pts=[v.co.copy() for v in obj.data.vertices];lo=[min(p[i] for p in pts) for i in range(3)];hi=[max(p[i] for p in pts) for i in range(3)]
        cx,cy=(lo[0]+hi[0])/2,(lo[1]+hi[1])/2;rx,ry=(hi[0]-lo[0])/2,(hi[1]-lo[1])/2
        # Recover every existing bevel/side axial station. Keep exterior envelope
        # rather than making a new arbitrary cylinder with a guessed length.
        stations={}
        for p in pts:
            z=round(p.z,6);r=math.hypot((p.x-cx)/rx,(p.y-cy)/ry);stations[z]=max(stations.get(z,0),r)
        exterior=[(z,r*rx,r*ry) for z,r in sorted(stations.items()) if r>.1]
        ax=min(aperture[0],rx*.86);ay=min(aperture[1],ry*.86)
        profile=exterior+[(hi[2],ax,ay),(lo[2],ax*.91,ay*.91)]
        vertices=[];faces=[];n=96
        for z,xr,yr in profile:
            for j in range(n):
                a=math.tau*j/n;vertices.append((cx+xr*math.cos(a),cy+yr*math.sin(a),z))
        for i in range(len(profile)):
            for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,((i+1)%len(profile))*n+(j+1)%n,((i+1)%len(profile))*n+j))
        mat=obj.data.materials[0];put(obj,vertices,faces,mat)
        # Leave both annular end faces planar, while the barrel walls are smooth.
        for face in obj.data.polygons:
            row=face.index//n
            if row in (len(exterior)-1,len(profile)-1):face.use_smooth=False
        sleeve=obj.copy();sleeve.data=obj.data.copy();bpy.context.collection.objects.link(sleeve);sleeve.name='SuperTank recessed metal sleeve'
        sleeve_verts=[];sleeve_faces=[]
        for z,xr,yr in [(lo[2],ax*.91-.0015,ay*.91-.0015),(hi[2],ax-.0015,ay-.0015),
                        (hi[2],ax-.0035,ay-.0035),(lo[2],ax*.91-.0035,ay*.91-.0035)]:
            for j in range(n):
                a=math.tau*j/n;sleeve_verts.append((cx+xr*math.cos(a),cy+yr*math.sin(a),z))
        for i in range(4):
            for j in range(n):sleeve_faces.append((i*n+j,i*n+(j+1)%n,((i+1)%4)*n+(j+1)%n,((i+1)%4)*n+j))
        put(sleeve,sleeve_verts,sleeve_faces,api['MATS']['Metal'])
        lengths.append(hi[2]-lo[2])
    # Recess only the visual disk inside the original tube. No FirePoint change.
    depth=max(lengths)*.55
    axis_world=mouth.matrix_world.to_3x3()@Vector((0,0,-depth))
    mouth.location+=mouth.parent.matrix_world.to_3x3().inverted()@axis_world
    cx=(max(p.x for p in mp)+min(p.x for p in mp))/2;cy=(max(p.y for p in mp)+min(p.y for p in mp))/2
    for v in mouth.data.vertices:v.co.x=cx+(v.co.x-cx)*.91;v.co.y=cy+(v.co.y-cy)*.91
    root['superBarrelAnnular']=True;root['superBarrelVisualRecess']=depth
    bpy.context.view_layer.update()
    return root
