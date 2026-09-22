using System.Collections.Generic;
using Landsong.EditorTools;
using UnityEngine;

namespace Landsong.GridSystem
{
    // Owns a copy of generated visual geometry. Source FBX assets are never changed.
    public sealed class SlopeVisualCut : MonoBehaviour
    {
        [SerializeField, Sirenix.OdinInspector.LabelText("原始网格")] Mesh original;
        [SerializeField, Sirenix.OdinInspector.LabelText("修整网格")] Mesh cut;
        public static void Apply(Transform root,IReadOnlyList<ProtrudingSlopeCompiler.Strip> strips,Material grassMaterial=null,Transform terrainRoot=null,IReadOnlyDictionary<string,float> visualOffsets=null)
        {
            foreach(var filter in (terrainRoot!=null?terrainRoot:root).GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponentInParent<ProtrudingSlopeVisualRoot>()!=null || filter.sharedMesh==null) continue;
                var state=filter.GetComponent<SlopeVisualCut>();
                var source=state!=null && filter.sharedMesh==state.cut ? state.original : filter.sharedMesh;
                if(!source.isReadable) continue;
                var vertices=source.vertices;bool changed=false;var modified=new bool[vertices.Length];
                var grass=new bool[vertices.Length];var landing=new bool[vertices.Length];
                var renderer=filter.GetComponent<MeshRenderer>();
                if(grassMaterial!=null && renderer!=null)
                {
                    var materials=renderer.sharedMaterials;
                    for(int s=0;s<source.subMeshCount && s<materials.Length;s++)
                        if(materials[s]==grassMaterial)foreach(int v in source.GetTriangles(s))grass[v]=true;
                }
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                    foreach(var strip in strips)
                    {
                        float height=strip.Height+(visualOffsets!=null && visualOffsets.TryGetValue(strip.BlueprintGuid,out var yOffset)?yOffset:0);
                        // Current Layer maps use one-unit cells; the caller supplies positions in logical units.
                        var start=strip.Cells[0];var d=strip.Direction;var lateral=new Vector2(d.y,-d.x);
                        var offset=new Vector2(p.x-start.x,p.z-start.y);
                        float across=Vector2.Dot(offset,lateral),along=Vector2.Dot(offset,d);
                        if(across<-.5f || across>strip.Cells.Length-.5f || along<-.5f || along>1f) continue;
                        // Unfold the upper tile's rounded grass lip into a flat landing.
                        // This fills the visible gap behind the slope without changing platform cells.
                        if(grass[i] && p.y>height-.4f && p.y<=height+.501f)
                        {
                            float shift=Mathf.Max(0,.5f-along);
                            p.x+=d.x*shift;p.z+=d.y*shift;p.y=height+.5f;landing[i]=true;changed=true;continue;
                        }
                        if(grass[i] || along>.5f)continue;
                        // Keep the original cliff below the end cap's curved grass lip,
                        // not just below the central planar ramp.
                        float side=1-Mathf.Clamp01(Mathf.Min(across+.5f,strip.Cells.Length-.5f-across));
                        float t=Mathf.Clamp01((along+.1f)/.6f),blend=t*t*(3-2*t);
                        float ceiling=height+along-.015f-.4f*side*(1-blend);
                        if(p.y>ceiling) { p.y=ceiling;changed=true; }
                    }
                    var next=filter.transform.InverseTransformPoint(root.TransformPoint(p));
                    modified[i]=(next-vertices[i]).sqrMagnitude>.00000001f;vertices[i]=next;
                }
                if(!changed)
                {
                    if(state!=null && filter.sharedMesh==state.cut)
                    {
                        filter.sharedMesh=source;
                        var oldCollider=filter.GetComponent<MeshCollider>();if(oldCollider!=null)oldCollider.sharedMesh=source;
                        state.ReleaseCut();
                    }
                    continue;
                }
                if(state==null) state=filter.gameObject.AddComponent<SlopeVisualCut>();
                var mesh=Instantiate(source);mesh.name=source.name+"_SlopeMouth";mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
                var originalNormals=source.normals;var normals=mesh.normals;
                if(originalNormals.Length==normals.Length)
                {
                    var up=filter.transform.InverseTransformDirection(root.up).normalized;
                    for(int i=0;i<normals.Length;i++)
                        if(landing[i])normals[i]=up;else if(!modified[i])normals[i]=originalNormals[i];
                    mesh.normals=normals;
                }
                state.ReleaseCut();state.original=source;state.cut=mesh;filter.sharedMesh=mesh;
                var collider=filter.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
            }
        }
        void ReleaseCut()
        {
            if(cut==null)return;
#if UNITY_EDITOR
            if(UnityEditor.AssetDatabase.Contains(cut)){cut=null;return;}
#endif
            if(Application.isPlaying)Destroy(cut);else DestroyImmediate(cut);
            cut=null;
        }
        void OnDestroy()=>ReleaseCut();
    }
}
