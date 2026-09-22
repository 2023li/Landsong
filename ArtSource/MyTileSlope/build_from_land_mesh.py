"""Adapt the live Unity prefab meshes exported by ExportSlopeReferences.
The source JSON includes prefab transforms; do not import raw FBX with guessed scale.
"""
import bpy, math, json
from pathlib import Path
from collections import Counter
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
OUT=ROOT/'Assets/TileWorldCreator/Tiles URP/MyTile/Mesh_Slope'
data=json.loads((HERE/'land_reference_meshes.json').read_text(encoding='utf-8'))['meshes']
edge_grass=next(m for m in data if m['name']=='Grass_Edge')
edge_rock=next(m for m in data if m['name']=='cliff_edge_A')
center=next(m for m in data if len(m['vertices'])==4)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1

def coord(p):return (p[0],p[2],p[1]) # Verified FBX -> Unity axes.
def canonical(m):return [(-v['x'],v['y'],-v['z']) for v in m['vertices']]
def mix(a,b,t):return a+(b-a)*t
def material(name,color):
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    node=mat.node_tree.nodes.get('Principled BSDF');node.inputs['Base Color'].default_value=(*color,1);node.inputs['Roughness'].default_value=.85
    return mat
grass=material('Existing_Grass_Preview',(.12,.48,.20))
rock=material('Existing_Cliff_Preview',(.40,.44,.51))
labelmat=material('Labels',(.70,.79,.84))
background=material('Backdrop',(.023,.034,.05))
exports=bpy.data.collections.new('EXPORT_MODULES');scene.collection.children.link(exports)
reference=bpy.data.collections.new('ORIGINAL_LAND_REFERENCE');scene.collection.children.link(reference)
preview=bpy.data.collections.new('ASSEMBLY_PREVIEW_NOT_EXPORTED');scene.collection.children.link(preview)

def from_source(name,m,verts,collection,mat):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([coord(v) for v in verts],[],[m['triangles'][i:i+3] for i in range(0,len(m['triangles']),3)])
    mesh.update();obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);mesh.materials.append(mat)
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        item=m['uv'][loop.vertex_index];uv.data[loop.index].uv=(item['x'],item['y'])
    # Unity and Blender use opposite handedness. Preserve the exact source topology/UVs.
    for face in mesh.polygons:face.flip()
    mesh.update()
    for face in mesh.polygons:face.use_smooth=True
    normals=[]
    source_points=canonical(m)
    for i,v in enumerate(mesh.vertices):
        n=m['normals'][i]
        unchanged=(Vector(verts[i])-Vector(source_points[i])).length<1e-7
        if 'Slope_' in name:unchanged=unchanged and weight(name.rsplit('_',1)[-1],source_points[i][0])==0
        normals.append(coord((-n['x'],n['y'],-n['z'])) if unchanged else tuple(v.normal))
    mesh.normals_split_custom_set_from_vertices(normals)
    obj['source_mesh']=m['name'];obj['source_material']=m['material']
    return obj

# The source grass has a rounded, downturned lip. Unfold its complete cross section
# instead of flattening the overhang onto itself. Its open front boundary provides
# an exact per-X parameter range; the resulting central sheet has no flipped faces.
original=canonical(edge_grass)
unique=sorted(set(tuple(round(c,5) for c in p) for p in original));lookup={p:i for i,p in enumerate(unique)}
tri=[lookup[tuple(round(c,5) for c in original[i])] for i in edge_grass['triangles']]
edges=Counter(tuple(sorted((tri[k+a],tri[k+b]))) for k in range(0,len(tri),3) for a,b in [(0,1),(1,2),(2,0)])
front=set()
for (a,b),count in edges.items():
    p,q=unique[a],unique[b]
    if count==1 and abs(p[0]-q[0])>.002 and min(p[2],q[2])<.49:front.update([p,q])
front=sorted(front)
def raw(p):return p[2]+1.6*(p[1]-.5)
def bottom(x):
    for a,b in zip(front,front[1:]):
        if a[0]<=x<=b[0]:return mix(raw(a),raw(b),(x-a[0])/(b[0]-a[0]))
    return raw(front[0] if x<front[0][0] else front[-1])
def target(p):
    lo=bottom(p[0]);t=-.5+(raw(p)-lo)/(.5-lo)
    return max(-.5,min(.5,t))
def weight(role,x):
    if role=='Middle':return 1
    t=max(0,min(1,((x if role=='Left' else -x)+.25)/.55))
    return t*t*(3-2*t)
def surface(role,p):
    w=weight(role,p[0]);t=target(p)
    snapped=math.copysign(.5,p[0]) if abs(abs(p[0])-.5)<.002 else max(-.5,min(.5,p[0]))
    x=mix(p[0],snapped,w)
    return (x,mix(p[1],t,w),mix(p[2],t,w))
def cliff(role,p):
    w=weight(role,p[0])
    return (mix(p[0],max(-.5,min(.5,p[0])),w),mix(p[1],-.53,w),p[2])

modules={};checks=[]
for role in ['Left','Middle','Right']:
    if role=='Middle':
        points=[(x,z,z) for x,y,z in canonical(center)]
        parts=[from_source('Grass_Slope_'+role,center,points,exports,grass)]
    else:
        points=[surface(role,p) for p in original]
        grass_part=from_source('Grass_Slope_'+role,edge_grass,points,exports,grass)
        canopy=BVHTree.FromPolygons([v.co for v in grass_part.data.vertices], [list(p.vertices) for p in grass_part.data.polygons])
        rock_points=[]
        for p in canonical(edge_rock):
            q=cliff(role,p);w=weight(role,p[0])
            if w>0:
                hit,normal,index,distance=canopy.ray_cast(Vector((q[0],q[2],5)),Vector((0,0,-1)))
                if hit is not None:
                    q=(q[0],min(q[1],hit.z-.05*min(1,w*10)),q[2])
            rock_points.append(q)
        parts=[grass_part,from_source('Cliff_Slope_'+role,edge_rock,rock_points,exports,rock)]
        for m,part in zip([edge_grass,edge_rock],parts):
            src=canonical(m)
            retained=[i for i,p in enumerate(src) if weight(role,p[0])==0]
            error=max((Vector(coord(src[i]))-part.data.vertices[i].co).length for i in retained)
            assert error<1e-6,(role,m['name'],error)
            checks.append(dict(role=role,source=m['name'],unchanged_outer_vertices=len(retained),max_error=error))
        # Validate the full-blend grass strip remains a forward-facing height field.
        for i in range(0,len(edge_grass['triangles']),3):
            ids=edge_grass['triangles'][i:i+3]
            if all(weight(role,original[k][0])==1 for k in ids):
                a,b,c=[points[k] for k in ids]
                area=(b[0]-a[0])*(c[2]-a[2])-(b[2]-a[2])*(c[0]-a[0])
                assert area<=1e-6,'Unfolded grass face inverted'
    modules[role]=parts
    for ob in parts:
        ob['width_cells']=1;ob['depth_cells']=1;ob['rise']=1;ob['layer_anchor']='upper Layer n'
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.hide_set(False);ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/('Slope_'+role+'.fbx')),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',global_scale=1,
        axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',
        bake_anim=False,add_leaf_bones=False,bake_space_transform=True)
    for ob in parts:ob.hide_render=True;ob.hide_set(True)

refs={}
for key,m,mat in [('Grass',edge_grass,grass),('Cliff',edge_rock,rock),('Fill',center,grass)]:
    refs[key]=from_source('Original_'+key,m,canonical(m),reference,mat)
    refs[key].hide_render=True;refs[key].hide_set(True)
def instance(src,pos,name):
    obj=bpy.data.objects.new(name,src.data);preview.objects.link(obj);obj.location=coord(pos);return obj
def label(body,pos,size=.3):
    font=bpy.data.curves.new('Label','FONT');font.body=body;font.size=size;font.align_x='CENTER';font.extrude=.001
    ob=bpy.data.objects.new('Label',font);preview.objects.link(ob);ob.location=coord(pos);font.materials.append(labelmat)

# Actual source edge and center tiles, not proxy cubes. Both original seams are visible.
for width,ox,span in [(3,-4.8,7),(6,5.8,10)]:
    for i in range(span):
        x=ox+i-(span-1)/2
        for z in [-2,-1]:instance(refs['Fill'],(x,0,z),'Lower_original_fill')
        for z in [1,2]:instance(refs['Fill'],(x,1,z),'Upper_original_fill')
    start=(span-width)//2
    for i in range(span):
        x=ox+i-(span-1)/2
        if start<=i<start+width:
            j=i-start;role='Left' if j==0 else 'Right' if j==width-1 else 'Middle'
            for ob in modules[role]:instance(ob,(x,1,0),'Preview_'+ob.name)
        else:
            for key in ['Grass','Cliff']:instance(refs[key],(x,1,0),'Neighbor_original_'+key)
            # Low fill under the original overhanging edge and the tapered end rocks.
        instance(refs['Fill'],(x,0,0),'Ground_below_edge')
    label(str(width)+' CELLS / ADAPTED FROM YOUR LAND MESH',(ox,-.48,-3.5),.26)
    label('ORIGINAL CLIFF | LEFT + MIDDLE x '+str(width-2)+' + RIGHT | ORIGINAL CLIFF',(ox,-.48,-4),.17)

bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.51))
bpy.context.object.name='Backdrop';bpy.context.object.data.materials.append(background)
world=bpy.data.worlds.new('Studio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.22,.26,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.55
for name,loc,energy,size in [('Key',(-5,-4,12),1900,9),('Fill',(8,3,8),1600,8)]:
    light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.shape='DISK';light.size=size
    ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((0,0,0))-ob.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.cameras.new('Camera');camera=bpy.data.objects.new('Camera',cam);scene.collection.objects.link(camera)
camera.location=(10,-18,17);camera.rotation_euler=(Vector((.8,-.2,.4))-camera.location).to_track_quat('-Z','Y').to_euler()
cam.type='ORTHO';cam.ortho_scale=23;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=1800;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(HERE/'slope_assembly_preview.png')
scene.view_settings.view_transform='AgX'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Slope_Modules.blend'))
bpy.ops.render.render(write_still=True)
(HERE/'source_mesh_verification.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print('SOURCE_MESH_SLOPES_COMPLETE',checks)
