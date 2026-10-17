using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class WorkstationModelAudit
    {
        public static void AuditMarkerFaces()
        {
            var report=new StringBuilder();bool previous=Physics.queriesHitBackfaces;
            try
            {
                foreach(string name in new[]{"HumanLera","HumanNastya"})
                {
                    var prefab=Resources.Load<GameObject>("CardboardCharacters/"+name);
                    var instance=UnityEngine.Object.Instantiate(prefab,new Vector3(100,0,100),Quaternion.identity);
                    try
                    {
                        var mesh=instance.GetComponent<MeshFilter>().sharedMesh;
                        instance.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();
                        report.AppendLine(name+" mesh bounds="+mesh.bounds);
                        foreach(bool backfaces in new[]{false,true})
                        {
                            Physics.queriesHitBackfaces=backfaces;
                            for(int side=0;side<2;side++)
                            {
                                Vector3 origin=instance.transform.TransformPoint(new Vector3(0,1.15f,side==0?1:-1));
                                Vector3 direction=side==0?Vector3.back:Vector3.forward;
                                if(Physics.Raycast(origin,direction,out var hit,2))
                                    report.AppendLine($"  backfaces={backfaces} from={(side==0?"+z":"-z")} hitLocal={instance.transform.InverseTransformPoint(hit.point):F4} normal={hit.normal:F2} uv={hit.textureCoord:F4} triangle={hit.triangleIndex}");
                            }
                        }
                    }
                    finally {UnityEngine.Object.DestroyImmediate(instance);}
                }
            }
            finally {Physics.queriesHitBackfaces=previous;}
            File.WriteAllText("Library/MarkerRaycastAudit.txt",report.ToString());
        }
        [MenuItem("Cardboard/Inspect workstation models")]
        public static void Write()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] {"Assets/Game_Assets"}))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine(path);
                var instance = UnityEngine.Object.Instantiate(asset);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    report.AppendLine($"  {renderer.name}: pos={renderer.transform.position:F3} rotation={renderer.transform.eulerAngles:F1} bounds center={renderer.bounds.center:F3} size={renderer.bounds.size:F3}");
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (clip is AnimationClip animation && !animation.name.StartsWith("__preview__"))
                    {
                        report.AppendLine($"  CLIP {animation.name} length={animation.length} legacy={animation.legacy}");
                        foreach(var binding in AnimationUtility.GetCurveBindings(animation)) report.AppendLine($"    {binding.path}: {binding.propertyName}");
                        foreach(float time in new[]{0f,animation.length})
                        {
                            animation.SampleAnimation(instance,time);
                            foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) report.AppendLine($"    t={time:F2} {renderer.name} pos={renderer.transform.localPosition:F3} rot={renderer.transform.localEulerAngles:F1} bounds={renderer.bounds.center:F3}/{renderer.bounds.size:F3}");
                        }
                    }
                UnityEngine.Object.DestroyImmediate(instance);
            }
            File.WriteAllText("Library/WorkstationModelAudit.txt", report.ToString());
        }
    }
}
