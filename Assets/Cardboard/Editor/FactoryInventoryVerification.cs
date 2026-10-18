using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryInventoryVerification
    {
        static readonly List<string> report=new List<string>();
        static bool running,background;
        static int step,frame;
        static double since,started;
        static FactoryInventoryVerification(){EditorApplication.update+=Update;}
        static void Check(bool value,string description){report.Add((value?"PASS ":"FAIL ")+description);}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();step=frame=0;running=true;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Next(int value){step=value;since=EditorApplication.timeSinceStartup;}
        static void Finish()
        {
            running=false;Application.runInBackground=background;
            File.WriteAllLines("Library/FactoryInventoryVerification.txt",new[]{"RESULT: "+report.Count(line=>line.StartsWith("PASS"))+" PASS, "+report.Count(line=>line.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
        static void Configuration(CardboardFactoryGame game,FactoryInventorySlot inventory)
        {
            Check(inventory && inventory.lens==Camera.main && inventory.overlay.transform.parent==Camera.main.transform,"Inventory is attached to first-person camera");
            Check(inventory.overlay.GetUniversalAdditionalCameraData().renderType==CameraRenderType.Overlay &&
                Camera.main.GetUniversalAdditionalCameraData().cameraStack.Contains(inventory.overlay),"Inventory uses an overlay camera and cannot be hidden by world geometry");
            Check(!inventory.visuals.GetComponentInChildren<Collider>(true),"Inventory does not intercept drawing or E raycasts");
            Check(inventory.visuals.GetComponentsInChildren<Renderer>(true).All(r=>r.gameObject.layer==FactoryInventorySlot.Layer),"All inventory geometry uses its own overlay layer");
            Check(inventory.visuals.GetComponentsInChildren<Renderer>(true).All(r=>!ShaderUtil.ShaderHasError(r.sharedMaterial.shader)),"Slot, marker and soft glow shaders compile");
            var mesh=inventory.visuals.Find("Inventory slot • supplied model and UV").GetComponent<MeshFilter>().sharedMesh;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(FactoryInventoryInstaller.Root+"Inventory slot.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(mesh.uv.SequenceEqual(original.uv),"Inventory slot keeps supplied model UV unchanged");
            Check(inventory.marker.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(FactoryInventoryInstaller.Root+"Marker.png"),"Marker uses the supplied transparent artwork");
            Check(inventory.marker.GetComponent<MeshFilter>().sharedMesh.uv.SequenceEqual(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right}),"Marker image uses full original UV without cropping");
            var materials=game.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
            Check(materials.All(m=>!m.HasProperty("_Smoothness") || m.GetFloat("_Smoothness")==0),"All scene materials have maximum roughness");
            Check(materials.All(m=>m.doubleSidedGI && (!m.HasProperty("_Cull") || m.GetFloat("_Cull")==0)),"All scene materials are two-sided");
            foreach(var station in game.stations)
            {
                var board=station.transform.Find("Workstation backboard").GetComponent<Renderer>();
                var table=station.transform.Find("Table").GetComponentsInChildren<Renderer>();
                var bounds=table[0].bounds;foreach(var renderer in table)bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(board.bounds.size.x-6.03f)<.001f && Mathf.Abs(board.bounds.size.y-3)<.001f,"Backboard is 1.5 times original width and height: "+station.name);
                Check(Mathf.Abs(board.bounds.center.x-bounds.center.x)<.001f && Mathf.Abs(board.bounds.min.y-(station.transform.position.y+.75f))<.001f,"Backboard stays centered on table and keeps original lower edge: "+station.name);
                Check(Mathf.Abs(board.bounds.min.z-bounds.max.z-.003f)<.001f,"Backboard sits just behind the table edge: "+station.name);
                Check(AssetDatabase.GetAssetPath(board.sharedMaterial.mainTexture)==FactoryWallsBoxInstaller.BackboardTexture,"Reserved backboard texture retained: "+station.name);
            }
            inventory.Pose(2,true,false);
            foreach(float aspect in new[]{4f/3,16f/9,21f/9})
            {
                inventory.lens.aspect=aspect;inventory.Layout();
                var part=inventory.visuals.Find("Inventory slot • supplied model and UV");
                bool inside=true;Vector2 min=Vector2.one*100,max=-Vector2.one*100;
                foreach(var vertex in part.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var p=inventory.overlay.WorldToViewportPoint(part.TransformPoint(vertex));
                    min=Vector2.Min(min,p);max=Vector2.Max(max,p);
                    inside &= p.x>.65f && p.x<1 && p.y>0 && p.y<.4f;
                }
                Check(inside,"Slot fits bottom-right corner at aspect "+aspect.ToString("F3")+" (min="+min+", max="+max+")");
            }
            inventory.lens.ResetAspect();inventory.Layout();
        }
        static void Settings(CardboardFactoryGame game,FactoryInventorySlot inventory)
        {
            var menu=game.mainMenu;menu.enabled=false;menu.Show();menu.Tick(1);
            inventory.Pose(.5f,false,false);
            Check(!inventory.Visible && !inventory.overlay.enabled,"Inventory hides while menu owns the camera");
            menu.Choose(FactoryMenuAction.Settings);menu.Tick(.3f);menu.Tick(1.1f);
            Check(menu.Phase==FactoryMenuPhase.Settings && menu.settingsPanel.gameObject.activeSelf,"Settings animation still opens the volume panel");
            foreach(var point in new[]{new Vector2(1.8f,0),new Vector2(-1.8f,0),new Vector2(0,1.4f),new Vector2(0,-1.4f)})
            {
                menu.SetSettingsPointer(point);menu.Tick(.5f);
                var direction=new Vector3(Mathf.Sign(point.x)*Mathf.Abs(point.x/1.8f),Mathf.Sign(point.y)*Mathf.Abs(point.y/1.4f),0);
                float depth=menu.lens.transform.InverseTransformPoint(menu.settingsPanel.TransformPoint(direction)).z-
                    menu.lens.transform.InverseTransformPoint(menu.settingsPanel.position).z;
                Check(depth<-.1f,"Mouse side tilts toward the player: "+point);
            }
            menu.SetSettingsPointer(new Vector2(1.2f,.7f));menu.Tick(.5f);Physics.SyncTransforms();
            var target=menu.volumeTrack.transform.TransformPoint(new Vector3(.65f,0,0));
            var ray=new Ray(menu.lens.transform.position,(target-menu.lens.transform.position).normalized);
            Check(menu.volumeTrack.Raycast(ray,out var contact,15),"Volume track remains hittable on a tilted panel");
            var plane=new Plane(menu.volumeTrack.transform.forward,menu.volumeTrack.transform.position);
            bool intersects=plane.Raycast(ray,out float distance);
            float value=Mathf.InverseLerp(-1.05f,1.05f,menu.volumeTrack.transform.InverseTransformPoint(ray.GetPoint(distance)).x);
            Check(intersects && Mathf.Abs(value-.80952f)<.001f,"Tilted volume drag retains accurate local coordinates");
            target=menu.back.hit.bounds.center;ray=new Ray(menu.lens.transform.position,(target-menu.lens.transform.position).normalized);
            Check(menu.back.hit.Raycast(ray,out contact,15),"Back button remains hittable on a tilted panel");
            CardboardFactorySceneBuilder.Capture("SettingsMouseTilt");
            menu.SetSettingsPointer(new Vector2(10,10));menu.Tick(1);
            Check(Quaternion.Angle(menu.settingsPanel.localRotation,Quaternion.identity)<.1f,"Settings smoothly return to rest after pointer leaves");
            menu.Choose(FactoryMenuAction.Back);menu.Tick(.3f);menu.Tick(1);
            Check(menu.Phase==FactoryMenuPhase.Main && !menu.settingsPanel.gameObject.activeSelf,"Back returns to the main menu after tilting");
            menu.Choose(FactoryMenuAction.Play);menu.Tick(.3f);menu.Tick(1.2f);menu.Tick(.3f);menu.Tick(.6f);
            Check(menu.Phase==FactoryMenuPhase.Closed && !game.MenuActive,"Play transition still returns control to the player");
        }
        static void Update()
        {
            if(!running)return;
            try
            {
                if(EditorApplication.timeSinceStartup-started>35){Check(false,"Timed out at stage "+step);Finish();return;}
                if(!EditorApplication.isPlaying || Time.frameCount==frame)return;frame=Time.frameCount;
                var game=CardboardFactoryGame.Instance;if(!game || game.CurrentOrder!=1)return;
                var marker=Camera.main.GetComponent<CardboardMarker>();var inventory=Camera.main.GetComponent<FactoryInventorySlot>();
                if(step==0)
                {
                    Configuration(game,inventory);
                    Camera.main.GetComponent<CardboardCamera>().enabled=false;game.enabled=false;
                    var eye=game.station.transform.TransformPoint(new Vector3(-1,1.9f,-5));
                    Camera.main.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.35f,0))-eye));
                    marker.SetDrawingMode(true);Next(1);
                }
                else if(step==1 && EditorApplication.timeSinceStartup-since>.6)
                {
                    Check(CardboardMarker.DrawingMode && inventory.Visible && inventory.Selection>.98f,"Existing marker selection drives live inventory highlight");
                    Check(inventory.marker.localPosition.y>inventory.markerHome.y+.12f && inventory.marker.localPosition.z<inventory.markerHome.z-.14f && inventory.glow.enabled,"Selected marker rises above slot with beige silhouette glow");
                    CardboardFactorySceneBuilder.Capture("InventoryMarkerSelected");
                    marker.SetDrawingMode(false);Next(2);
                }
                else if(step==2 && EditorApplication.timeSinceStartup-since>.6)
                {
                    Check(!CardboardMarker.DrawingMode && inventory.Selection<.02f && Vector3.Distance(inventory.marker.localPosition,inventory.markerHome)<.005f,"Deselection smoothly returns marker to slot");
                    CardboardFactorySceneBuilder.Capture("InventoryMarkerIdle");
                    Settings(game,inventory);Next(3);
                }
                else if(step==3)
                {
                    Check(inventory.Visible,"Inventory returns automatically after Play");
                    Finish();
                }
            }
            catch(Exception error){Check(false,error.ToString());Finish();Debug.LogException(error);}
        }
    }
}
