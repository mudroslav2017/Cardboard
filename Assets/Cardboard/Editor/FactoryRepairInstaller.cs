using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryRepairInstaller
    {
        static string PathOf(Transform item,Transform root){return AnimationUtility.CalculateTransformPath(item,root);}
        static void CopyTree(Transform source,Transform target)
        {
            foreach(var part in source.GetComponentsInChildren<Transform>(true))
            {
                var other=part==source?target:target.Find(PathOf(part,source));if(!other)continue;
                other.localPosition=part.localPosition;other.localRotation=part.localRotation;other.localScale=part.localScale;
                EditorUtility.SetDirty(other);PrefabUtility.RecordPrefabInstancePropertyModifications(other);
            }
        }
        static void FitLever(Transform pivot,Transform grip,StationPart part)
        {
            var knob=pivot.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.StartsWith("Sphere",StringComparison.OrdinalIgnoreCase));
            grip.position=knob.transform.position;EditorUtility.SetDirty(grip);
            var control=pivot.GetComponentsInChildren<FactoryStationControl>(true).FirstOrDefault(c=>c.part==part);
            if(control)
            {
                var bounds=knob.bounds;
                control.transform.position=bounds.center;
                var hit=control.GetComponent<BoxCollider>();hit.center=Vector3.zero;hit.size=new Vector3(.38f,.40f,.40f);hit.isTrigger=true;
                EditorUtility.SetDirty(hit);EditorUtility.SetDirty(control.transform);
            }
        }
        public static void Layout(CardboardFactoryGame game)
        {
            var reference=game.station;var track=reference.transform.Find("Cut progress track");
            // The track is the authored reference; filling remains 2.5 cm in front of it.
            Vector3 origin=reference.progressFill.parent.InverseTransformPoint(track.TransformPoint(new Vector3(0,0,-.025f/track.localScale.z)));
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                if(station!=reference){CopyTree(reference.feedLever,station.feedLever);CopyTree(reference.pressLever,station.pressLever);CopyTree(track,station.transform.Find("Cut progress track"));}
                FitLever(station.feedLever,station.feedGrip,StationPart.Feed);FitLever(station.pressLever,station.pressGrip,StationPart.Press);
                station.progressOrigin=origin;
                station.progressFill.localRotation=track.localRotation;
                station.progressFill.localPosition=origin+station.progressFill.localRotation*Vector3.left*.55f;
                EditorUtility.SetDirty(station.progressFill);EditorUtility.SetDirty(station);
            }
        }
        public static void FixCanteenPassage(CardboardFactoryGame game)
        {
            var walls=game.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Canteen passage side wall").ToArray();
            float south=CardboardFactoryWorld.CanteenNorth+.2f,north=CardboardFactoryWorld.WorkshopSouth-.25f;
            int index=0;
            foreach(var wall in walls)
            {
                var t=wall.transform;float length=north-south;t.localScale=Vector3.Scale(t.localScale,new Vector3(1,1,length/wall.bounds.size.z));
                t.position=new Vector3(t.position.x,t.position.y,(north+south)*.5f);
                FactoryWallsBoxInstaller.Paint(t,"CanteenPassageFitted"+(++index),wall.sharedMaterials,0,CardboardFactoryWorld.WallHeight);EditorUtility.SetDirty(t);
            }
            var portal=game.transform.Find("Factory doorways/Canteen door");
            foreach(var duplicate in portal.Cast<Transform>().Where(t=>t.name=="Doorway infill" && t.localScale.x<.5f).ToArray())Object.DestroyImmediate(duplicate.gameObject);
            int meshIndex=0;
            foreach(var filter in portal.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.GetComponent<TextMesh>())continue;
                var source=filter.sharedMesh;var points=source.vertices;var clone=Object.Instantiate(source);bool changed=false;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var triangles=source.GetTriangles(sub);var kept=new System.Collections.Generic.List<int>();
                    for(int n=0;n<triangles.Length;n+=3)
                    {
                        Vector3 a=filter.transform.TransformPoint(points[triangles[n]]),b=filter.transform.TransformPoint(points[triangles[n+1]]),c=filter.transform.TransformPoint(points[triangles[n+2]]);
                        bool flush=new[]{-14.5f,-11.5f}.Any(x=>Mathf.Abs(a.x-x)<.001f && Mathf.Abs(b.x-x)<.001f && Mathf.Abs(c.x-x)<.001f);
                        if(flush){changed=true;continue;}kept.Add(triangles[n]);kept.Add(triangles[n+1]);kept.Add(triangles[n+2]);
                    }
                    clone.SetTriangles(kept,sub);
                }
                if(!changed){Object.DestroyImmediate(clone);continue;}
                string path="Assets/Cardboard/Generated/CanteenDoorFlushFaces"+(meshIndex++)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(clone,saved);Object.DestroyImmediate(clone);}else{AssetDatabase.CreateAsset(clone,path);saved=clone;}
                filter.sharedMesh=saved;EditorUtility.SetDirty(filter);
            }
        }
        static void Marker(CardboardFactoryGame game)
        {
            var lens=Camera.main;var hand=lens.GetComponent<CardboardMarkerHand>();
            hand.paperTemplate.shader=Shader.Find(CardboardSurface.ShaderName);CardboardSurface.Configure(hand.paperTemplate);hand.paperTemplate.SetFloat("_Cull",2);EditorUtility.SetDirty(hand.paperTemplate);
            var node=lens.transform.Find("Lit marker hand camera");if(!node){node=new GameObject("Lit marker hand camera").transform;node.SetParent(lens.transform,false);}
            var overlay=node.GetComponent<Camera>();if(!overlay)overlay=node.gameObject.AddComponent<Camera>();overlay.CopyFrom(lens);overlay.cullingMask=1<<CardboardMarkerHand.HandLayer;overlay.clearFlags=CameraClearFlags.Depth;overlay.enabled=false;
            var data=overlay.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;
            var stack=lens.GetUniversalAdditionalCameraData().cameraStack;if(!stack.Contains(overlay))stack.Insert(0,overlay);
            hand.viewCamera=overlay;lens.cullingMask &= ~(1<<CardboardMarkerHand.HandLayer);EditorUtility.SetDirty(hand);EditorUtility.SetDirty(lens);EditorUtility.SetDirty(overlay);EditorUtility.SetDirty(data);EditorUtility.SetDirty(lens.GetUniversalAdditionalCameraData());
            var sound=lens.GetComponent<FactoryMarkerSound>();if(!sound)sound=lens.gameObject.AddComponent<FactoryMarkerSound>();
            const string path="Assets/Game_Assets/Sounds/маркер2.mp3";var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            if(!importer.forceToMono){importer.forceToMono=true;importer.SaveAndReimport();}sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var audioNode=lens.transform.Find("Marker writing sound");if(!audioNode){audioNode=new GameObject("Marker writing sound").transform;audioNode.SetParent(lens.transform,false);}
            var speaker=audioNode.GetComponent<AudioSource>();if(!speaker)speaker=audioNode.gameObject.AddComponent<AudioSource>();
            speaker.clip=sound.clip;speaker.playOnAwake=false;speaker.loop=true;speaker.volume=0;speaker.spatialBlend=1;speaker.minDistance=1;speaker.maxDistance=7;speaker.dopplerLevel=0;speaker.rolloffMode=AudioRolloffMode.Linear;sound.speaker=speaker;EditorUtility.SetDirty(sound);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply repairs in Edit Mode.");
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();string apple=FactoryAudioInstaller.ApplePose(game.lunch);
                Audit();Layout(game);FixCanteenPassage(game);Marker(game);
                if(FactoryAudioInstaller.ApplePose(game.lunch)!=apple)throw new InvalidOperationException("Apple pose changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                File.WriteAllText("Library/FactoryRepairUpdate.txt","SUCCESS: hand lit/depth corrected, marker sound, corridor flush surfaces, lever layout and bar copied to all stations; apple preserved.");Audit();
            }
            catch(Exception error){File.WriteAllText("Library/FactoryRepairUpdate.txt",error.ToString());Debug.LogException(error);}
        }
        public static void Audit()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var text=new StringBuilder();
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                text.AppendLine(station.name+" player="+station.isPlayerStation);
                foreach(var part in new[]{station.feedLever,station.pressLever,station.wheel,station.wheelBase,station.progressFill,station.transform.Find("Cut progress track")})
                foreach(var t in part.GetComponentsInChildren<Transform>(true))
                    text.AppendLine(PathOf(t,station.transform)+" pos="+t.localPosition.ToString("F5")+" rot="+t.localEulerAngles.ToString("F3")+" scale="+t.localScale.ToString("F5"));
                text.AppendLine("progressOrigin="+station.progressOrigin);
            }
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.Contains("Canteen") || r.name=="Doorway infill" || r.name.Contains("South wall") || r.name=="South mechanism wall"))
                text.AppendLine(PathOf(renderer.transform,game.transform)+" bounds="+renderer.bounds+" mesh="+renderer.GetComponent<MeshFilter>()?.sharedMesh?.name);
            File.WriteAllText("Library/FactoryRepairAudit.txt",text.ToString());
        }
    }
}
