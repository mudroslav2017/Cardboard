using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryFeelVerification
    {
        static bool running,background;
        static readonly List<string> report=new List<string>();
        static double started;
        static int stage;
        static FactoryFeelVerification(){EditorApplication.update+=Tick;}
        static void Check(bool condition,string description){report.Add((condition?"PASS ":"FAIL ")+description);}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();running=true;stage=0;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static Color32[] Frame(Camera camera)
        {
            var rt=new RenderTexture(640,400,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(640,400,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                VolumeManager.instance.Update(camera.transform,camera.GetUniversalAdditionalCameraData().volumeLayerMask);
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,400),0,0);image.Apply();return image.GetPixels32();
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static float Difference(Color32[] a,Color32[] b)
        {
            double value=0;for(int i=0;i<a.Length;i++)value+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
            return (float)(value/(a.Length*765.0));
        }
        static void Atmosphere(CardboardFactoryGame game)
        {
            var root=game.transform.Find(FactoryAtmosphereInstaller.RootName);
            var systems=root.GetComponentsInChildren<ParticleSystem>().Where(p=>p.transform.parent==root).ToArray();
            Check(systems.Length==2 && systems.Sum(p=>p.particleCount)>2500,"Both air volumes are prewarmed at roughly triple particle density");
            Check(systems[0].emission.rateOverTime.constant==95 && systems[1].emission.rateOverTime.constant==28,"Workshop and canteen dust use bounded higher emission rates");
            var floor=root.Find("Floor • settled dust and offcuts");
            Check(floor.childCount>=225 && !floor.GetComponentInChildren<Collider>(),"More than 225 floor dust patches, with no movement collision");
            float floorY=FactoryEnvironmentInstaller.FloorY(game.transform);
            Check(floor.GetComponentsInChildren<Renderer>().All(r=>Mathf.Abs(r.bounds.min.y-floorY)<.005f),"Additional debris stays on the floor");
            Check(RenderSettings.fog && RenderSettings.fogMode==FogMode.Exponential && Mathf.Abs(RenderSettings.fogDensity-.0065f)<.00001f,"Global haze remains subtle at 0.0065 density");
            var shafts=root.Find("Light • subtle warm scattering");
            Check(shafts.childCount==22 && !shafts.GetComponentInChildren<Collider>(),"22 depth-clipped light scattering volumes are decorative");
            Check(shafts.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode==ShadowCastingMode.Off && !ShaderUtil.ShaderHasError(r.sharedMaterial.shader)),"Scattering shader compiles and never creates solid box shadows");
            var camera=Camera.main;
            var eye=game.station.transform.TransformPoint(new Vector3(3.8f,1.8f,-7));
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.5f,0))-eye));
            foreach(var system in systems)system.Pause();
            Frame(camera);
            shafts.gameObject.SetActive(false);RenderSettings.fog=false;Frame(camera);var clear=Frame(camera);
            RenderSettings.fog=true;Frame(camera);var fogOnly=Frame(camera);
            shafts.gameObject.SetActive(true);Frame(camera);var hazy=Frame(camera);
            float scatter=Difference(fogOnly,hazy);Check(scatter>.0001f && scatter<.1f,"GPU: warm lamp scattering is visible independently of global fog (delta="+scatter.ToString("F5")+")");
            float delta=Difference(clear,hazy);Check(delta>.0004f && delta<.18f,"GPU: subtle haze changes the image without hiding the workshop (delta="+delta.ToString("F5")+")");
            CardboardFactorySceneBuilder.Capture("FactoryDenserDustAndHaze");
            eye=game.station.transform.TransformPoint(new Vector3(3,.8f,-4.8f));
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(1.5f,0,-1))-eye));
            CardboardFactorySceneBuilder.Capture("FactoryExtraFloorDebris");
            foreach(var system in systems)system.Play();
        }
        static void CameraMotion(CardboardFactoryGame game)
        {
            var camera=Camera.main;var motion=camera.GetComponent<CardboardCamera>();
            Check(camera.fieldOfView==80,"Gameplay starts with field of view 80");
            motion.ResetView(0);float walk=0,run=0;
            for(int i=0;i<180;i++){motion.TickWalkingMotion(1f/60,3.2f,true);walk=Mathf.Max(walk,motion.WalkingOffset.magnitude);}
            motion.ResetView(0);
            for(int i=0;i<180;i++){motion.TickWalkingMotion(1f/60,6,true);run=Mathf.Max(run,motion.WalkingOffset.magnitude);}
            Check(walk>.009f && walk<.02f && run>walk*2 && run<.04f,"Walking sway is small; running increases it about 2.6 times");
            for(int i=0;i<90;i++)motion.TickWalkingMotion(1f/60,0,true);
            Check(motion.WalkingOffset.magnitude<.0001f && Quaternion.Angle(motion.WalkingRotation,Quaternion.identity)<.02f,"Sway smoothly settles after stopping");
            for(int i=0;i<90;i++)motion.TickWalkingMotion(1f/60,6,false);
            Check(motion.WalkingOffset.magnitude<.0001f,"Footstep sway stays off in the air");
            motion.ResetView(0);motion.enabled=false;
        }
        static void Until(FactoryCaptureSequence sequence,CapturePhase phase)
        {
            for(int i=0;i<1800 && sequence.Phase!=phase && sequence.Phase!=CapturePhase.Restarting;i++)sequence.Tick(.05f);
            if(sequence.Phase!=phase)throw new InvalidOperationException("Capture expected "+phase+", got "+sequence.Phase);
        }
        static void Capture(FactoryCaptureSequence sequence,string name)
        {
            var inventory=Camera.main.GetComponent<FactoryInventorySlot>();if(inventory)inventory.Pose(1,false,false);
            Camera.main.transform.SetPositionAndRotation(sequence.CameraPosition,sequence.CameraRotation);
            CardboardFactorySceneBuilder.Capture(name);
        }
        static void Death(CardboardFactoryGame game)
        {
            foreach(var worker in game.captureSequence.workers)
                foreach(var renderer in worker.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Synchronous preview ticks must not reuse GPU instance transforms from the previous frame.
                    var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                    properties.SetColor("_BaseColor",renderer.sharedMaterial.GetColor("_BaseColor"));renderer.SetPropertyBlock(properties);
                }
            var sequence=game.captureSequence;sequence.AutomaticRestart=false;sequence.enabled=false;
            game.ResetShift();game.RegisterMistake("Debug 1");game.RegisterMistake("Debug 2");game.RegisterMistake("Debug 3");
            var hunter=sequence.workers[0];var body=hunter.GetComponent<CharacterController>();body.enabled=false;
            hunter.transform.position=CardboardRunner.Player.transform.position+Vector3.right*.8f;body.enabled=true;Physics.SyncTransforms();
            sequence.Tick(.01f);Check(sequence.Phase==CapturePhase.Lifting,"Third mistake still starts first-person capture");
            Until(sequence,CapturePhase.Lever);sequence.Tick(.95f);
            Check(sequence.workers.All(worker=>
            {
                var direction=sequence.BedPosition-worker.transform.position;direction.y=0;
                return direction.sqrMagnitude>.001f && Vector3.Dot(worker.transform.forward,direction.normalized)>.98f;
            }),"Every worker faces the player's head while the lever is pulled");
            Capture(sequence,"PressWorkersFacingPlayer");
            Until(sequence,CapturePhase.Crushing);sequence.Tick(.35f);
            Check(sequence.workers.All(worker=>
            {
                var direction=sequence.BedPosition-worker.transform.position;direction.y=0;
                return Vector3.Dot(worker.transform.forward,direction.normalized)>.98f;
            }),"Workers keep facing the player during crushing");
            Capture(sequence,"CrushingFacingWorkers");
            foreach(int index in new[]{sequence.CarrierLeft,sequence.CarrierRight})
            {
                var mesh=sequence.workers[index].GetComponentInChildren<MeshFilter>();var bounds=mesh.sharedMesh.bounds;
                var head=mesh.transform.TransformPoint(new Vector3(bounds.center.x,bounds.max.y-.35f,bounds.center.z));
                var screen=Camera.main.WorldToViewportPoint(head);
                Check(screen.z>0 && screen.x>.02f && screen.x<.98f && screen.y>.02f && screen.y<.98f,"Carrier face stays in frame beneath the press: "+index+" ("+screen+")");
            }
            Until(sequence,CapturePhase.Grade);
            var grade=sequence.grade;var fx=grade.presentation;
            Check(grade.gameObject.activeSelf && grade.wordOpacity.alpha==0 && fx.overlay.enabled,"Crush contact blacks out and starts the new presentation at time zero");
            Check(fx.gears.Length==12 && fx.gearRoot.GetComponentsInChildren<Collider>().Length==0,"Four corners contain three real gear models each, with no colliders");
            for(int corner=0;corner<4;corner++)for(int kind=0;kind<3;kind++)
                Check(fx.gears[corner*3+kind].GetComponentInChildren<MeshFilter>().sharedMesh==Resources.Load<GameObject>("FactoryGears/Gear"+(kind+1)).GetComponent<MeshFilter>().sharedMesh,"Corner "+(corner+1)+" contains authored gear type "+(kind+1));
            var first=fx.gears[0].localPosition;sequence.Tick(.20f);
            Check(Vector3.Distance(first,fx.gears[0].localPosition)>.2f && fx.wave.enabled && !ShaderUtil.ShaderHasError(fx.wave.material.shader),"Red wave and radial gear flight animate together at impact");
            Capture(sequence,"CrushRedWaveAndGears");
            sequence.Tick(.70f);fx.Layout();
            Check(grade.pinOpacity.alpha==1 && grade.lettering.text=="ЛОХ" && grade.lettering.font==CardboardTypography.DefaultFont,"Original red grade, shared font and cardboard pin are preserved");
            bool corners=true;
            for(int i=0;i<12;i++)
            {
                var p=fx.overlay.WorldToViewportPoint(fx.gears[i].position);
                corners &= p.x>0 && p.x<1 && p.y>0 && p.y<1 && Math.Abs(p.x-.5f)>.23f && Math.Abs(p.y-.5f)>.15f;
            }
            Check(corners && !fx.wave.enabled,"All gear centers settle in screen corners and the wave fades");
            Check(fx.gears.All(gear=>Vector3.Angle(gear.forward,(gear.position-fx.lens.transform.position).normalized)>12),"Corner gears remain tilted to show their thickness");
            Capture(sequence,"PinnedGradeWithGearCorners");
            sequence.Tick(1.85f);Check(!sequence.RestartRequested,"Pinned grade still holds for two full seconds");
            sequence.Tick(.1f);Check(sequence.RestartRequested,"Full restart is requested after the original pin hold");
            game.ResetShift();
            Check(!fx.overlay.enabled && !fx.gearRoot.gameObject.activeSelf && !grade.gameObject.activeSelf,"New shift clears wave, dimensional gears and black screen");
        }
        static void Tick()
        {
            if(!running)return;
            if(EditorApplication.timeSinceStartup-started>45){running=false;report.Add("FAIL Verification timed out");File.WriteAllLines("Library/FactoryFeelVerification.txt",report);EditorApplication.isPlaying=false;return;}
            if(!EditorApplication.isPlaying || !CardboardFactoryGame.Instance || !CardboardRunner.Player || Time.time<.4f)return;
            var game=CardboardFactoryGame.Instance;if(game.MenuActive)return;
            if(stage==0)
            {
                try
                {
                    game.enabled=false;CameraMotion(game);Atmosphere(game);
                    foreach(var worker in game.captureSequence.workers)
                        foreach(var renderer in worker.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                            properties.SetColor("_BaseColor",renderer.sharedMaterial.GetColor("_BaseColor"));renderer.SetPropertyBlock(properties);
                        }
                    stage=1;return;
                }
                catch(Exception error){report.Add("FAIL "+error);stage=1;}
            }
            running=false;
            try {Death(game);}
            catch(Exception error){report.Add("FAIL "+error);Debug.LogException(error);}
            finally
            {
                game.ResetShift();Application.runInBackground=background;
                File.WriteAllLines("Library/FactoryFeelVerification.txt",new[]{"RESULT: "+report.Count(s=>s.StartsWith("PASS"))+" PASS, "+report.Count(s=>s.StartsWith("FAIL"))+" FAIL"}.Concat(report));
                EditorApplication.isPlaying=false;
            }
        }
    }
}
