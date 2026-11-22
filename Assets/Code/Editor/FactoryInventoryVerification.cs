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
            inventory.lens.ResetAspect();inventory.Layout();Motion(inventory);Palette(inventory,inventory.lens.GetComponent<CardboardMarker>());
        }
        static void Motion(FactoryInventorySlot inventory)
        {
            inventory.ResetInertia();
            for(int i=0;i<12;i++)inventory.TickInertia(1f/60,Vector3.zero,new Vector3(0,180,0),Vector3.zero);
            Check(inventory.InertialOffset.x<-.001f && inventory.InertialTilt.y<-.2f,"Right turn gently drags the slot left with rotational lag");
            inventory.TickInertia(1f/60,Vector3.zero,Vector3.zero,Vector3.zero);
            Check(inventory.InertialOffset.x<-.001f,"Slot retains inertia briefly when the turn stops");
            for(int i=0;i<120;i++)inventory.TickInertia(1f/60,Vector3.zero,Vector3.zero,Vector3.zero);
            Check(inventory.InertialOffset.magnitude<.00001f && inventory.InertialTilt.magnitude<.001f,"Slot smoothly returns to its default point after motion");
            for(int i=0;i<60;i++)inventory.TickInertia(1f/60,new Vector3(0,0,6),Vector3.zero,Vector3.zero);
            Check(inventory.InertialOffset.z<-.011f && inventory.InertialOffset.z>-.0125f,"Walking and running produce the stronger positional lag");
            inventory.ResetInertia();
            for(int i=0;i<60;i++)inventory.TickInertia(1f/60,new Vector3(3.2f,0,0),new Vector3(0,120,0),Vector3.zero);
            var at60=inventory.InertialOffset;
            inventory.ResetInertia();
            for(int i=0;i<144;i++)inventory.TickInertia(1f/144,new Vector3(3.2f,0,0),new Vector3(0,120,0),Vector3.zero);
            Check(Vector3.Distance(at60,inventory.InertialOffset)<.00005f,"Inertia remains consistent at 60 and 144 FPS");
            for(int i=0;i<60;i++)inventory.TickInertia(.05f,new Vector3(-100,100,100),new Vector3(0,-10000,0),Vector3.zero);
            Check(inventory.InertialOffset.magnitude<.018f && inventory.InertialTilt.magnitude<3,"Rapid motion remains bounded and subtle");
            foreach(float aspect in new[]{4f/3,16f/9,21f/9})
            {
                inventory.lens.aspect=aspect;inventory.Layout();
                var part=inventory.visuals.Find("Inventory slot • supplied model and UV");bool inside=true;
                foreach(var vertex in part.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var p=inventory.overlay.WorldToViewportPoint(part.TransformPoint(vertex));
                    inside &= p.x>8f/inventory.lens.pixelWidth && p.x<1-8f/inventory.lens.pixelWidth &&
                        p.y>8f/inventory.lens.pixelHeight && p.y<1-8f/inventory.lens.pixelHeight;
                }
                Check(inside,"Extreme inertia keeps the slot and eight-pixel outline on screen at aspect "+aspect.ToString("F3"));
            }
            inventory.lens.ResetAspect();inventory.Pose(1,false,false);
            Check(inventory.InertialOffset==Vector3.zero && inventory.InertialTilt==Vector3.zero,"Hiding inventory clears motion before menus, capture and restart");
            inventory.Pose(1,true,false);
            Check(inventory.outline.sharedMaterial.GetFloat("_OutlinePixels")==8,"Black slot outline is eight screen pixels");
        }
        static void Palette(FactoryInventorySlot inventory,CardboardMarker marker)
        {
            var palette=inventory.palette;
            Check(palette && palette.cards.Length==5 && palette.numbers.Length==5,"Palette contains all five marker colors");
            if(!palette)return;
            Check(palette.numbers.Select((text,i)=>text.text==(i+1).ToString() && text.color==Color.white &&
                text.font==CardboardTypography.DefaultFont).All(value=>value),"White 1–5 labels use the project font");
            Check(palette.shortcut.text=="F" && palette.shortcut.color==Color.black &&
                palette.shortcut.font==CardboardTypography.DefaultFont &&
                palette.shortcut.transform.localPosition.x<0 && palette.shortcut.transform.localPosition.y<0,
                "Black F shortcut sits at the bottom left of the slot in the project font");
            Check(palette.cards.Select((card,i)=>card.GetComponentInChildren<MeshRenderer>(true).sharedMaterial.GetColor("_BaseColor")==marker.Colors[i]).All(value=>value),
                "Visible palette colors match the existing 1–5 drawing input exactly");
            var material=palette.cards[0].GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            var fibers=material.GetTexture("_BaseMap") as Texture2D;
            Check(fibers && fibers.GetPixel(96,96)!=fibers.GetPixel(99,96) &&
                palette.cards[0].GetComponentInChildren<MeshFilter>(true).sharedMesh.bounds.size.z>.02f,
                "Color cards have star-like paper grain and physical cardboard thickness");
            palette.Pose(0,false,false);palette.Pose(.04f,true,true);
            Check(palette.Reveal(0)>0 && Enumerable.Range(1,4).All(i=>palette.Reveal(i)==0),"First color begins alone when the marker is selected");
            palette.Pose(.12f,true,true);
            Check(palette.Reveal(0)>palette.Reveal(1) && palette.Reveal(1)>0 && palette.Reveal(2)==0,"Cardboard colors emerge in order with a stagger");
            palette.Pose(.20f,true,true);
            Check(palette.cards[0].localPosition.x<FactoryMarkerPalette.OpenPosition(0).x-.02f,"Sliding cards overshoot and bounce before settling");
            palette.Pose(1,true,true);
            Check(palette.Expanded && Enumerable.Range(0,5).All(i=>Vector3.Distance(palette.cards[i].localPosition,FactoryMarkerPalette.OpenPosition(i))<.001f),
                "All five cards settle at separate readable positions");
            foreach(float aspect in new[]{4f/3,16f/9,21f/9})
            {
                inventory.lens.aspect=aspect;inventory.Layout();bool inside=true;
                foreach(var renderer in palette.GetComponentsInChildren<MeshRenderer>())
                {
                    var bounds=renderer.localBounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        var p=inventory.overlay.WorldToViewportPoint(renderer.transform.TransformPoint(point));
                        inside &= p.x>.01f && p.x<.99f && p.y>.01f && p.y<.99f;
                    }
                }
                Check(inside,"All five cards and their white labels remain on screen at aspect "+aspect.ToString("F3"));
            }
            inventory.lens.ResetAspect();inventory.Layout();
            palette.Pose(.1f,true,false);var partial=palette.cards[0].localPosition;
            palette.Pose(.01f,true,true);
            Check(Vector3.Distance(palette.cards[0].localPosition,partial)<.12f,"Rapid F toggle reopens the palette from its current position");
            palette.Pose(1,true,false);
            Check(!palette.Expanded && palette.cards.All(card=>!card.gameObject.activeSelf),"Deselecting the marker retracts and hides all colors");
            palette.Pose(1,true,true);palette.Pose(0,false,true);
            Check(palette.cards.All(card=>!card.gameObject.activeSelf),"Menu and capture immediately hide and reset the palette");
            inventory.Pose(1,true,false);
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
                else if(step==1 && EditorApplication.timeSinceStartup-since>1.0)
                {
                    Check(CardboardMarker.DrawingMode && inventory.Visible && inventory.Selection>.98f,"Existing marker selection drives live inventory highlight");
                    Check(inventory.marker.localPosition.y>inventory.markerHome.y+.12f && inventory.marker.localPosition.z<inventory.markerHome.z-.14f && inventory.glow.enabled,"Selected marker rises above slot with beige silhouette glow");
                    Check(inventory.palette.Expanded,"Live F selection expands all five cardboard colors");
                    CardboardFactorySceneBuilder.Capture("InventoryMarkerSelected");
                    marker.SetDrawingMode(false);Next(2);
                }
                else if(step==2 && EditorApplication.timeSinceStartup-since>1.0)
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
