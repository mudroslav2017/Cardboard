using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryLeverInstaller
    {
        static Transform Handle(Transform assembly,Transform grip,StationPart part)
        {
            var existing=assembly.Find("Moving handle only");if(existing)return existing;
            var moving=assembly.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.StartsWith("Sphere",StringComparison.OrdinalIgnoreCase));
            var fixedPart=assembly.GetComponentsInChildren<MeshFilter>(true).First(f=>f!=moving);
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(moving.gameObject);
            if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var pivot=new GameObject("Moving handle only").transform;pivot.SetParent(assembly,false);
            pivot.position=fixedPart.transform.TransformPoint(fixedPart.sharedMesh.bounds.center);pivot.rotation=assembly.rotation;
            var before=moving.transform.localToWorldMatrix;
            moving.transform.SetParent(pivot,true);grip.SetParent(pivot,true);
            foreach(var control in assembly.GetComponentsInChildren<FactoryStationControl>(true).Where(c=>c.part==part).ToArray())control.transform.SetParent(pivot,true);
            for(int v=0;v<16;v++)if(Mathf.Abs(before[v]-moving.transform.localToWorldMatrix[v])>.0001f)throw new InvalidOperationException("Lever rest pose changed.");
            EditorUtility.SetDirty(assembly);EditorUtility.SetDirty(moving.transform);EditorUtility.SetDirty(grip);return pivot;
        }
        public static void Install(FactoryWorkstation station)
        {
            station.feedHandle=Handle(station.feedLever,station.feedGrip,StationPart.Feed);
            station.pressHandle=Handle(station.pressLever,station.pressGrip,StationPart.Press);EditorUtility.SetDirty(station);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Adjust lever models in Edit Mode.");
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();string apple=FactoryAudioInstaller.ApplePose(game.lunch);
                var stations=game.GetComponentsInChildren<FactoryWorkstation>(true);foreach(var station in stations)Install(station);
                if(FactoryAudioInstaller.ApplePose(game.lunch)!=apple)throw new InvalidOperationException("Apple pose changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                File.WriteAllText("Library/FactoryLeverUpdate.txt","SUCCESS: "+(stations.Length*2)+" moving handles separated from their fixed bases; all authored rest positions and progress tracks preserved.");
            }
            catch(Exception error){File.WriteAllText("Library/FactoryLeverUpdate.txt",error.ToString());Debug.LogException(error);}
        }
        public static List<List<int>> Components(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var parent=Enumerable.Range(0,vertices.Length).ToArray();
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var key=Vector3Int.RoundToInt(vertices[i]*10000);
                if(welded.TryGetValue(key,out int other))Join(i,other);else welded[key]=i;
            }
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var result=new Dictionary<int,List<int>>();
            for(int i=0;i<vertices.Length;i++){int key=Find(i);if(!result.TryGetValue(key,out var component))result[key]=component=new List<int>();component.Add(i);}
            return result.Values.OrderByDescending(c=>c.Count).ToList();
        }
        public static void Audit()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var text=new StringBuilder();
            foreach(var pivot in new[]{game.station.feedLever,game.station.pressLever})
            foreach(var filter in pivot.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;text.AppendLine(pivot.name+" / "+filter.name+" vertices="+mesh.vertexCount+" submeshes="+mesh.subMeshCount+" bounds="+mesh.bounds);
                foreach(var component in Components(mesh))
                {
                    var points=component.Select(i=>game.station.transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.vertices[i]))).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                    text.AppendLine("Connected vertices="+component.Count+" station bounds="+bounds);
                }
            }
            File.WriteAllText("Library/FactoryLeverAudit.txt",text.ToString());
        }
    }
}
