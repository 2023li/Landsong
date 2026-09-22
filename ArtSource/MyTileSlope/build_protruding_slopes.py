"""Adapt the live Unity prefab meshes exported by ExportSlopeReferences.
The source JSON includes prefab transforms; do not import raw FBX with guessed scale.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

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
        if 'Slope_' in name:unchanged=False
        normals.append(coord((-n['x'],n['y'],-n['z'])) if unchanged else tuple(v.normal))
    mesh.normals_split_custom_set_from_vertices(normals)
    obj['source_mesh']=m['name'];obj['source_material']=m['material']
    return obj

original=canonical(edge_grass)

modules={};checks=[]
for role in ['Left','Middle','Right']:
    if role=='Middle':
        points=[(x,z,z) for x,y,z in canonical(center)]
        parts=[from_source('Grass_Slope_'+role,center,points,exports,grass)]
    else:
        def protrude(p,grass_surface=False):
            x,y,z=p
            sx,sz=(z,-x) if role=='Left' else (-z,x)
            sz=max(-.5,min(.5,sz))
            if grass_surface:
                t=max(0,min(1,(sz+.1)/.6));blend=t*t*(3-2*t)
                sign=1 if role=='Left' else -1
                lo=max(v['z'] for v in edge_grass['vertices'] if abs(v['x']+.5)<.0001)
                hi=max(v['z'] for v in edge_grass['vertices'] if abs(v['x']-.5)<.0001)
                lip=lo+(hi-lo)*(.5-x)
                outer=(.5-sign*sx)/(.5+lip)
                sx-=sign*(.53-lip)*outer*blend
                # Open the rounded side lip into a grass shoulder at the top.
                # The inner seam stays on Y=Z; the rear edge meets the plateau.
                y=.5+(y-.5)*(1-blend)
            return (sx,y-.5+sz,sz)
        parts=[from_source('Grass_Slope_'+role,edge_grass,[protrude(p,True) for p in original],exports,grass),
               from_source('Cliff_Slope_'+role,edge_rock,[protrude(p) for p in canonical(edge_rock)],exports,rock)]
    modules[role]=parts
    for ob in parts:
        ob['width_cells']=1;ob['depth_cells']=1;ob['rise']=1;ob['layer_anchor']='upper Layer n; footprint outside platform'
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
        for z in [1.5,2.5]:instance(refs['Fill'],(x,1,z),'Upper_original_fill')
        for key in ['Grass','Cliff']:
            obj=instance(refs[key],(x,1,.5),'Platform_original_'+key)
            obj.data=obj.data.copy()
            # Match the generated TWC landing: flatten its rounded grass lip and
            # lower the obstructing cliff only within the exterior slope footprint.
            for v in obj.data.vertices:
                across=x+v.co.x-(ox+((span-width)//2)-(span-1)/2)
                along=.5+v.co.y; height=1+v.co.z
                if -.5<=across<=width-.5 and -.5<=along<=1:
                    if key=='Grass' and .6<height<=1.501:
                        v.co.y+=max(0,.5-along);v.co.z=.5
                    elif key=='Cliff' and along<=.5:
                        side=1-max(0,min(1,min(across+.5,width-.5-across)))
                        t=max(0,min(1,(along+.1)/.6));blend=t*t*(3-2*t)
                        ceiling=along-.015-.4*side*(1-blend)
                        if v.co.z>ceiling:v.co.z=ceiling
            obj.data.update()
    start=(span-width)//2
    for i in range(span):
        x=ox+i-(span-1)/2
        if start<=i<start+width:
            j=i-start;role='Left' if j==0 else 'Right' if j==width-1 else 'Middle'
            for ob in modules[role]:instance(ob,(x,1,0),'Preview_'+ob.name)
        # Low fill under the original overhanging edge and the tapered end rocks.
        instance(refs['Fill'],(x,0,0),'Ground_below_edge')
    label(str(width)+' CELLS / PROTRUDING / PLATFORM UNCHANGED',(ox,-.48,-3.5),.26)
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
print('PROTRUDING_SLOPES_COMPLETE')
