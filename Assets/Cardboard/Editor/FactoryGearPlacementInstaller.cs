using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryGearPlacementInstaller
    {
        static ulong Edge(int a,int b)=>((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
        static Vector3[] Contour(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var points=new List<Vector3>();
            var welded=new Dictionary<Vector2Int,int>();var edges=new Dictionary<ulong,int>();
            Func<Vector3,int> weld=point=>
            {
                var key=new Vector2Int(Mathf.RoundToInt(point.x*100000),Mathf.RoundToInt(point.y*100000));
                if(welded.TryGetValue(key,out int found))return found;
                int index=points.Count;welded.Add(key,index);points.Add(new Vector3(point.x,point.y,mesh.bounds.max.z+.018f));return index;
            };
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];var normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<1e-12f || Mathf.Abs(normal.z)<normal.magnitude*.98f || (a.z+b.z+c.z)/3<mesh.bounds.center.z)continue;
                var ids=new[]{weld(a),weld(b),weld(c)};
                for(int j=0;j<3;j++){ulong edge=Edge(ids[j],ids[(j+1)%3]);edges.TryGetValue(edge,out int count);edges[edge]=count+1;}
            }
            var remaining=new HashSet<ulong>(edges.Where(p=>p.Value==1).Select(p=>p.Key));
            var adjacent=new Dictionary<int,List<int>>();
            foreach(ulong edge in remaining)
            {
                int a=(int)(edge>>32),b=(int)(uint)edge;
                if(!adjacent.ContainsKey(a))adjacent[a]=new List<int>();if(!adjacent.ContainsKey(b))adjacent[b]=new List<int>();
                adjacent[a].Add(b);adjacent[b].Add(a);
            }
            List<int> outer=null;float largest=0;
            while(remaining.Count>0)
            {
                ulong first=remaining.First();int start=(int)(first>>32),next=(int)(uint)first;var loop=new List<int>{start};
                remaining.Remove(first);
                while(next!=start)
                {
                    loop.Add(next);int current=next;next=-1;
                    foreach(int neighbor in adjacent[current])if(remaining.Remove(Edge(current,neighbor))){next=neighbor;break;}
                    if(next<0)break;
                }
                if(next!=start || loop.Count<3)continue;
                float area=0;for(int i=0;i<loop.Count;i++){var a=points[loop[i]];var b=points[loop[(i+1)%loop.Count]];area+=a.x*b.y-b.x*a.y;}
                if(Mathf.Abs(area)>largest){largest=Mathf.Abs(area);outer=loop;}
            }
            if(outer==null)throw new InvalidOperationException("No closed gear silhouette in "+mesh.name);
            return outer.Select(i=>points[i]).ToArray();
        }
        public static void Apply(Transform root)
        {
            var game=root.GetComponent<CardboardFactoryGame>();var preview=root.GetComponent<FactoryGearPlacementPreview>();
            if(!preview)preview=root.gameObject.AddComponent<FactoryGearPlacementPreview>();preview.game=game;
            const string path="Assets/Cardboard/Generated/GearPlacementOutline.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",new Color(1,.87f,.36f));material.SetFloat("_Cull",0);material.doubleSidedGI=true;EditorUtility.SetDirty(material);
            var contours=new Dictionary<Mesh,Vector3[]>();
            foreach(var gear in game.gears)
            {
                if(gear.placementOutline)Object.DestroyImmediate(gear.placementOutline.gameObject);
                var source=gear.healthy.GetComponentInChildren<MeshFilter>(true);
                if(!contours.TryGetValue(source.sharedMesh,out var points)){points=Contour(source.sharedMesh);contours.Add(source.sharedMesh,points);}
                var outline=new GameObject("Placement preview • gear silhouette");outline.transform.SetParent(gear.damaged,false);outline.layer=9;
                var line=outline.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.alignment=LineAlignment.View;
                var pose=gear.damaged.worldToLocalMatrix*source.transform.localToWorldMatrix;
                line.positionCount=points.Length;line.SetPositions(points.Select(p=>pose.MultiplyPoint3x4(p)).ToArray());
                line.widthMultiplier=.024f;line.numCornerVertices=2;line.numCapVertices=0;
                line.sharedMaterial=material;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
                gear.placementOutline=outline.transform;outline.SetActive(false);EditorUtility.SetDirty(gear);
            }
            EditorUtility.SetDirty(preview);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update placement previews in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();Apply(game.transform);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllLines("Library/FactoryGearPlacement.txt",new[]{"SUCCESS: aimed broken socket has a warm gear-shaped outline while carrying a gear."}.Concat(game.gears.Select(g=>"Socket "+(g.index+1)+": "+g.placementOutline.GetComponent<LineRenderer>().positionCount+" contour vertices")));
        }
    }
}