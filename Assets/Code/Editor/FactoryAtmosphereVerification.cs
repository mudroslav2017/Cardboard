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
    public static class FactoryAtmosphereVerification
    {
        static bool running,oldBackground;
        static readonly List<string> report=new List<string>();
        static FactoryAtmosphereVerification(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();running=true;oldBackground=Application.runInBackground;Application.runInBackground=true;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string detail){report.Add((condition?"PASS ":"FAIL ")+detail);}
        static Color32[] Frame(Camera camera)
        {
            var rt=new RenderTexture(480,300,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(480,300,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                VolumeManager.instance.Update(camera.transform,camera.GetUniversalAdditionalCameraData().volumeLayerMask);
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,480,300),0,0);texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=active;
                UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        static float Difference(Color32[] a,Color32[] b)
        {
            double difference=0;
            for(int i=0;i<a.Length;i++)difference+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);
            return (float)(difference/(a.Length*765.0));
        }
        static void RenderCheck(CardboardFactoryGame game)
        {
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var volume=game.transform.Find(FactoryAtmosphereInstaller.RootName).GetComponentInChildren<Volume>();
            var features=AssetDatabase.FindAssets("t:UniversalRendererData",new[]{"Assets/Settings"})
                .Select(guid=>AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)))
                .SelectMany(data=>data.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>()).ToArray();
            try
            {
                var eye=game.station.transform.TransformPoint(new Vector3(2.8f,.75f,-3.8f));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(.8f,.4f,0))-eye));
                foreach(var feature in features)feature.SetActive(false);
                Frame(camera);var withoutAO=Frame(camera);
                foreach(var feature in features)feature.SetActive(true);
                Frame(camera);var withAO=Frame(camera);float ao=Difference(withoutAO,withAO);
                Check(ao>.0001f && ao<.15f,"GPU: ambient occlusion changes the rendered image (mean delta="+ao.ToString("F5")+")");
                volume.enabled=false;Frame(camera);var withoutGrade=Frame(camera);
                volume.enabled=true;Frame(camera);var withGrade=Frame(camera);float grade=Difference(withoutGrade,withGrade);
                Check(grade>.002f && grade<.4f,"GPU: Global Volume visibly grades the image (mean delta="+grade.ToString("F5")+")");
            }
            finally{foreach(var feature in features)feature.SetActive(true);volume.enabled=true;camera.transform.SetPositionAndRotation(position,rotation);}
        }
        static void Configuration(CardboardFactoryGame game)
        {
            var root=game.transform.Find(FactoryAtmosphereInstaller.RootName);
            var volume=root.GetComponentInChildren<Volume>();
            Check(volume && volume.isGlobal && volume.weight==1 && volume.sharedProfile,"Persistent global atmosphere volume is active");
            Check(volume.sharedProfile.TryGet<ColorAdjustments>(out var grade) && grade.contrast.overrideState && grade.contrast.value>0,"Warm contrast grade is stored in an editable profile");
            Check(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing && Camera.main.allowHDR,"Gameplay camera uses HDR post processing");
            Check(!game.mainMenu.uiCamera.GetUniversalAdditionalCameraData().renderPostProcessing,"Menu overlay avoids applying a second grade");
            foreach(string guid in AssetDatabase.FindAssets("t:UniversalRendererData",new[]{"Assets/Settings"}))
            {
                var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                var ao=data.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().ToArray();
                Check(ao.Length==1 && ao[0].isActive,"One active AO feature on "+data.name);
                var serialized=new SerializedObject(ao[0]);
                Check(serialized.FindProperty("m_Settings.Intensity").floatValue>=1 && serialized.FindProperty("m_Settings.Source").intValue==1,"AO uses stronger contact shading and actual surface normals");
            }
            var floor=root.Find("Floor • settled dust and offcuts");var renderers=floor.GetComponentsInChildren<MeshRenderer>();
            Check(renderers.Length>=100 && !floor.GetComponentInChildren<Collider>(),"Floor debris is decorative and never blocks movement or E interaction");
            Check(renderers.Select(r=>r.sharedMaterial.GetTexture("_BaseMap")).Distinct().Count()==5,"All five provided floor debris textures are used");
            float floorY=FactoryEnvironmentInstaller.FloorY(game.transform);
            Check(renderers.All(r=>Mathf.Abs(r.bounds.min.y-floorY)<.005f),"Every floor patch touches the floor without floating");
            Check(renderers.All(r=>r.sharedMaterial.GetFloat("_Cull")==0 && r.sharedMaterial.GetFloat("_Smoothness")==0 && !ShaderUtil.ShaderHasError(r.sharedMaterial.shader)),"Floor dust shaders compile, are two-sided and fully rough");
            foreach(var air in root.GetComponentsInChildren<ParticleSystem>())
            {
                Check(air.isPlaying && air.particleCount>20,"Beige airborne dust is prewarmed and alive: "+air.name);
                var before=new ParticleSystem.Particle[air.main.maxParticles];int count=air.GetParticles(before);
                air.Simulate(.3f,false,false);var after=new ParticleSystem.Particle[air.main.maxParticles];int next=air.GetParticles(after);
                Check(count>0 && next>0 && Vector3.Distance(before[0].position,after[0].position)>.001f,"Airborne dust drifts with time");
                air.Play(false);
            }
            foreach(var station in game.stations)
            {
                Check(station.machiningEffects && station.machiningEffects.shavings.Length==3,"All three shaving variants are connected to "+station.name);
                Check(station.machiningEffects.shavings.All(p=>!p.emission.enabled && p.particleCount==0),"Idle workstation does not emit shavings");
            }
        }
        static void Machining(CardboardFactoryGame game)
        {
            game.ResetShift();var station=game.station;var effects=station.machiningEffects;
            Check(!station.TurnWheel(20) && effects.EmittedCount==0,"Invalid wheel use does not create debris");game.ResetShift();
            station.Operate(StationPart.Feed);station.Tick(1.1f);station.Tick(2.3f);
            station.Operate(StationPart.Press);station.Tick(1.1f);station.Operate(StationPart.Guard);station.Tick(.7f);
            Check(!station.TurnWheel(20) && effects.EmittedCount==0,"Wheel before full pressure creates no debris");
            for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);
            Check(station.TurnWheel(540) && effects.EmittedCount>=89 && effects.EmittedCount<=90,"Half a successful cut emits a proportional amount of shavings");
            foreach(var system in effects.shavings)system.Simulate(.25f,false,false);
            Check(effects.shavings.All(p=>p.particleCount>0),"Authored 11, 22 and 33 shavings all appear during cutting");
            float floor=FactoryEnvironmentInstaller.FloorY(game.transform);bool grounded=true;
            foreach(var system in effects.shavings)
            {
                var particles=new ParticleSystem.Particle[80];int count=system.GetParticles(particles);
                Check(count>0 && Vector3.Distance(particles[0].position,system.transform.position)>.03f,"Chips fly away from cutter");
                system.Simulate(1.3f,false,false);count=system.GetParticles(particles);
                for(int i=0;i<count;i++)grounded &= particles[i].position.y>=floor-.02f;
            }
            Check(grounded,"Gravity and floor collision keep chips above the floor");
            foreach(var system in effects.shavings){system.Play(false);system.Emit(7);system.Simulate(.25f,false,false);}
            var camera=Camera.main;var eye=station.transform.TransformPoint(new Vector3(.1f,1.35f,-2.1f));
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(0,1,0))-eye));
            CardboardFactorySceneBuilder.Capture("CuttingCardboardShavings");
            Check(station.TurnWheel(540) && station.Stage==StationStage.Cut && effects.EmittedCount>=179 && effects.EmittedCount<=180,"Full cut emits a bounded total and finishes the gear");
            int emitted=effects.EmittedCount;station.Tick(1);
            Check(effects.EmittedCount==emitted,"Station stops producing shavings when wheel is not cutting");
            foreach(var system in effects.shavings)system.Simulate(6,false,false);
            Check(effects.shavings.All(p=>p.particleCount==0),"Old chips expire instead of accumulating forever");
            game.ResetShift();Check(effects.EmittedCount==0 && effects.shavings.All(p=>p.particleCount==0),"New shift clears machining debris");
        }
        static void Tick()
        {
            if(!running || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            var game=CardboardFactoryGame.Instance;if(!game || !CardboardRunner.Player || game.MenuActive || Time.time<.3f)return;
            running=false;
            try{Configuration(game);RenderCheck(game);Machining(game);}
            catch(Exception error){report.Add("FAIL "+error);}
            finally
            {
                game.ResetShift();Application.runInBackground=oldBackground;
                File.WriteAllLines("Library/FactoryAtmosphereVerification.txt",report);EditorApplication.isPlaying=false;
            }
        }
    }
}
