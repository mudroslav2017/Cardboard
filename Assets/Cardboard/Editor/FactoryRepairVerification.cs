using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryRepairVerification
    {
        const string Active="Cardboard.RepairVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;static FactoryWorkstation station;static Camera camera;static Vector3 eye;static Quaternion look;
        static GameObject sheet;static Material paper;static Texture2D texture;static float at;static int stage;static double started;static bool background;
        static FactoryRepairVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactoryRepairVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void TimeMachine(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)station.Tick(.02f);}
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;station=game.station;camera=Camera.main;eye=camera.transform.position;look=camera.transform.rotation;
            game.ResetShift();game.enabled=false;station.enabled=false;game.mainMenu.enabled=false;camera.GetComponent<CardboardCamera>().enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            foreach(var item in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                Check(item.feedLever.GetComponentInChildren<MeshFilter>().transform.localPosition==station.feedLever.GetComponentInChildren<MeshFilter>().transform.localPosition && item.pressLever.GetComponentInChildren<MeshFilter>().transform.localPosition==station.pressLever.GetComponentInChildren<MeshFilter>().transform.localPosition,"Saved authored lever mesh positions match on "+item.name);
                Check(Vector3.Distance(item.progressOrigin,station.progressOrigin)<.0001f && item.transform.Find("Cut progress track").localPosition==station.transform.Find("Cut progress track").localPosition,"Progress fill and track retain the new shared position on "+item.name);
            }
            Check(Resources.FindObjectsOfTypeAll<AudioClip>().All(c=>c.name!="dry ratchet" && c.name!="cardboard machine clunk"),"Legacy synthetic machine clunks are absent");
            var walls=game.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Canteen passage side wall").ToArray();
            Check(walls.Length==2 && walls.All(r=>r.bounds.min.z>=CardboardFactoryWorld.CanteenNorth+.199f && r.bounds.max.z<=CardboardFactoryWorld.WorkshopSouth-.249f),"Passage side walls stop at the adjoining walls without overlapping end surfaces");
            int duplicateFaces=0;
            foreach(var mesh in game.transform.Find("Factory doorways/Canteen door").GetComponentsInChildren<MeshFilter>())
            {
                if(mesh.GetComponent<TextMesh>())continue;var points=mesh.sharedMesh.vertices;var triangles=mesh.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=mesh.transform.TransformPoint(points[triangles[i]]);var b=mesh.transform.TransformPoint(points[triangles[i+1]]);var c=mesh.transform.TransformPoint(points[triangles[i+2]]);
                    if(new[]{-14.5f,-11.5f}.Any(x=>Mathf.Abs(a.x-x)<.001f && Mathf.Abs(b.x-x)<.001f && Mathf.Abs(c.x-x)<.001f))duplicateFaces++;
                }
            }
            Check(duplicateFaces==0,"Door header and jambs contain no faces coplanar with passage walls");
            camera.transform.position=station.WheelCenter-station.transform.forward*2;
            camera.transform.rotation=Quaternion.LookRotation(station.WheelCenter-camera.transform.position);
            Vector2 center=camera.WorldToScreenPoint(station.WheelCenter);var home=station.wheel.rotation;
            Check(station.BeginWheel(center+Vector2.right*120),"Wheel can be grabbed before any pressure is pumped");
            station.DragWheel(center+Vector2.down*120);
            Check(Quaternion.Angle(home,station.wheel.rotation)>20 && station.CutProgress==0 && station.Pressure==0,"Actual pointer dragging rotates the imported wheel before pressure without cutting a blank");station.EndGrip(false);
            game.ResetShift();while(station.SelectedType!=game.OrderedType)station.Operate(StationPart.Selector);
            station.Operate(StationPart.Feed);TimeMachine(4);station.Operate(StationPart.Press);TimeMachine(1.2f);
            Check(station.Operate(StationPart.Guard),"Guard can be raised before cutting");TimeMachine(.8f);
            Check(station.Operate(StationPart.Guard) && !station.GuardClosed,"Guard can be lowered again before any cutting starts");TimeMachine(.8f);
            station.Operate(StationPart.Guard);TimeMachine(.8f);
            Check(station.BeginWheel(center+Vector2.right*120),"Wheel remains available with the guard raised and pressure still empty");station.EndGrip(false);
            for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);station.TurnWheel(90);
            float progress=station.CutProgress;
            Check(progress>0 && station.Operate(StationPart.Guard),"Guard can be lowered during an unfinished cut");TimeMachine(.8f);
            Check(station.Pressure==0 && Mathf.Approximately(station.CutProgress,progress),"Lowering the guard releases pressure and preserves unfinished cut progress");
            station.Operate(StationPart.Guard);TimeMachine(.8f);for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);station.TurnWheel(1080);
            Check(station.Stage==StationStage.Cut,"Cut can finish after guard adjustment and refilling pressure");
            station.Operate(StationPart.Guard);TimeMachine(.8f);station.Operate(StationPart.Press);TimeMachine(1.2f);
            Check(station.Operate(StationPart.Output) && game.HasPart,"Full machine sequence still releases a finished part");game.ResetShift();
            camera.transform.SetPositionAndRotation(new Vector3(100,100,100),Quaternion.identity);
            sheet=GameObject.CreatePrimitive(PrimitiveType.Quad);sheet.name="Temporary marker verification sheet";sheet.transform.position=camera.transform.position+Vector3.forward*1.2f;
            texture=new Texture2D(64,64);texture.SetPixels(Enumerable.Repeat(Color.white,4096).ToArray());texture.Apply();paper=new Material(Shader.Find(CardboardSurface.ShaderName));paper.mainTexture=texture;sheet.GetComponent<Renderer>().sharedMaterial=paper;sheet.AddComponent<CardboardDrawingSurface>();
            at=Time.time;
        }
        static Color32[] Frame()
        {
            var rt=new RenderTexture(640,400,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(640,400,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,400),0,0);image.Apply();return image.GetPixels32();}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Lighting()
        {
            var hand=camera.GetComponent<CardboardMarkerHand>();var marker=CardboardMarker.Instance;
            marker.TracePointer(new Vector2(Screen.width*.52f,Screen.height*.5f),false);hand.RefreshPose(1);
            Check(hand.Visible && hand.viewCamera.enabled && hand.viewCamera.GetUniversalAdditionalCameraData().clearDepth && camera.GetUniversalAdditionalCameraData().cameraStack.Contains(hand.viewCamera),"Marker hand renders through the active depth-clearing URP overlay");
            Check(hand.Tip.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.shader.name==CardboardSurface.ShaderName && r.sharedMaterial.GetFloat("_Cull")==2 && r.sharedMaterial.GetFloat("_ZWrite")==1),"Hand primitives use lit materials, outward faces and opaque depth writes");
            int mask=camera.cullingMask;var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var intensities=lights.Select(l=>l.intensity).ToArray();Color ambient=RenderSettings.ambientLight;
            float difference=0;
            try
            {
                camera.cullingMask=0;Frame();var lit=Frame();foreach(var light in lights)light.intensity=0;RenderSettings.ambientLight=Color.black;Frame();var dark=Frame();
                double delta=0;for(int i=0;i<lit.Length;i++)delta+=Math.Abs(lit[i].r-dark[i].r)+Math.Abs(lit[i].g-dark[i].g)+Math.Abs(lit[i].b-dark[i].b);difference=(float)(delta/(lit.Length*765.0));
            }
            finally{camera.cullingMask=mask;for(int i=0;i<lights.Length;i++)lights[i].intensity=intensities[i];RenderSettings.ambientLight=ambient;}
            Check(difference>.0002f,"GPU: actual hand pixels respond to scene lighting; image delta="+difference.ToString("F5"));
            CardboardFactorySceneBuilder.Capture("LitMarkerHand");
        }
        static void Finish()
        {
            if(game){CardboardMarker.Instance.SetDrawingMode(false,false);game.ResetShift();camera.transform.SetPositionAndRotation(eye,look);camera.GetComponent<CardboardCamera>().AdoptCurrentView();camera.GetComponent<CardboardCamera>().enabled=true;game.enabled=true;station.enabled=true;game.mainMenu.enabled=true;foreach(var worker in game.crowd.runners)worker.enabled=true;}
            if(sheet)Object.DestroyImmediate(sheet);if(paper)Object.DestroyImmediate(paper);if(texture)Object.DestroyImmediate(texture);
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>45){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;return;}
                if(Time.time-at<.2f)return;
                if(stage==1)
                {
                    var marker=CardboardMarker.Instance;marker.SetDrawingMode(true,false);marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),true);marker.TracePointer(new Vector2(Screen.width*.52f,Screen.height*.5f),true);
                    var sound=camera.GetComponent<FactoryMarkerSound>();Check(marker.IsTouching && sound.speaker.isPlaying && sound.speaker.clip==sound.clip,"Actual marker contact and movement play the supplied writing clip");
                    marker.TracePointer(new Vector2(Screen.width*.52f,Screen.height*.5f),false);at=Time.time;stage=2;return;
                }
                if(stage==2)
                {
                    Check(!camera.GetComponent<FactoryMarkerSound>().speaker.isPlaying,"Marker sound fades out after the stroke is released");Lighting();at=Time.time;stage=3;return;
                }
                if(stage==3)
                {
                    CardboardMarker.Instance.SetDrawingMode(false,false);camera.transform.SetPositionAndRotation(new Vector3(-13,1.6f,-11),Quaternion.LookRotation(new Vector3(-.6f,.2f,-1)));
                    CardboardFactorySceneBuilder.Capture("CanteenPassageFixed");Finish();
                }
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
