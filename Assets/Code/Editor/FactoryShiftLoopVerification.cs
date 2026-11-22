using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryShiftLoopVerification
    {
        const string Active="Cardboard.ShiftLoopVerification",Report="Library/FactoryShiftLoopVerification.txt";
        static readonly List<string> report=new List<string>();
        static readonly Dictionary<Behaviour,bool> controls=new Dictionary<Behaviour,bool>();
        static CardboardFactoryGame game;
        static CardboardRunner player;
        static double started;
        static float timeScale;
        static bool background;
        static SimulationMode simulation;
        static FactoryShiftLoopVerification(){EditorApplication.update+=Tick;}
        static void Check(bool condition,string text){report.Add((condition?"PASS ":"FAIL ")+text);}
        static void Disable(Behaviour control){if(control && !controls.ContainsKey(control)){controls.Add(control,control.enabled);control.enabled=false;}}
        [MenuItem("Cardboard/Verify repair counter, single lunch and new shift")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();controls.Clear();game=null;started=EditorApplication.timeSinceStartup;
            timeScale=Time.timeScale;background=Application.runInBackground;simulation=Physics.simulationMode;
            Application.runInBackground=true;FactoryMainMenu.SkipNextMenu=true;
            File.WriteAllText(Report,"RUNNING\n");SessionState.SetBool(Active,true);
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Place(CardboardRunner actor,Vector3 position)
        {
            var body=actor.GetComponent<CharacterController>();bool enabled=body && body.enabled;
            if(body)body.enabled=false;actor.transform.position=position;if(body)body.enabled=enabled;
            if(actor.isPlayer && Camera.main)Camera.main.transform.position=position+Vector3.up*1.62f;
        }
        static void Play()
        {
            var menu=game.mainMenu;
            menu.Tick(1);menu.Choose(FactoryMenuAction.Play);
            menu.Tick(.3f);menu.Tick(1.2f);menu.Tick(.3f);menu.Tick(.6f);
        }
        static void Repair()
        {
            var gear=game.Memorized;game.ReceiveWorkbenchPart(gear.recipe);
            Check(game.TryInstall(gear),"Normal repair installs: "+(game.Repairs));
        }
        static void Reset()
        {
            game.ResetShift();Place(player,new Vector3(1,0,-1));Physics.SyncTransforms();
        }
        static void Counter()
        {
            Reset();var counter=game.GetComponentInChildren<FactoryRepairCounter>(true);
            for(int count=0;count<=6;count++)
            {
                if(count>0)Repair();
                counter.Refresh();Canvas.ForceUpdateCanvases();
                var label=counter.label;var generator=new TextGenerator();
                generator.Populate(label.text,label.GetGenerationSettings(label.rectTransform.rect.size));
                report.Add("DETAIL counter="+label.text+" width="+label.rectTransform.rect.width+" preferred="+label.preferredWidth+
                    " lines="+generator.lineCount+" visible="+generator.characterCountVisible+" vertices="+generator.vertexCount);
                Check(counter.Visible && label.text==count+" / 6" && generator.lineCount==1 &&
                    generator.characterCountVisible>=label.text.Length && label.preferredWidth<=label.rectTransform.rect.width+.1f,
                    "Complete repair count and target fit on one visible line at "+count+" / 6");

            }
            Reset();for(int i=0;i<4;i++)Repair();
            var position=player.transform.position;game.mainMenu.Show();Play();
            Check(game.Repairs==4 && !game.intro.Active && Vector3.Distance(position,player.transform.position)<.001f,
                "Escape-menu resume keeps four repairs and the player position");
        }
        static void StartLunch()
        {
            Reset();Repair();game.lunch.TickSchedule(game.lunch.lunchAfter);
            Check(game.lunch.State==LunchState.Ready,"Ordinary lunch begins after its first delay and repair");
        }
        static void Serve()
        {
            var lunch=game.lunch;Place(player,lunch.pickup.position+Vector3.back);
            Check(lunch.TryTake(),"Fresh lunch tray can be picked up");
            Place(player,lunch.destination.position+Vector3.back);Check(lunch.TryServe(),"Lunch can be delivered");
            Place(player,new Vector3(1,0,-1));
        }
        static void Crowd()
        {
            StartLunch();Serve();var crowd=game.lunch.diners;
            // One diner never arrives: a hard deadline must still let the others go back to work.
            for(int i=0;i<crowd.workers.Length-1;i++)Place(crowd.workers[i],crowd.places[i].position);
            Physics.SyncTransforms();crowd.Tick(.02f);crowd.Tick(crowd.minimumLunch+45.1f);
            Check(crowd.Returning,"An absent diner cannot hold the entire lunch indefinitely");
            StartLunch();Serve();crowd=game.lunch.diners;
            for(int i=0;i<crowd.workers.Length;i++)Place(crowd.workers[i],crowd.places[i].position);
            Physics.SyncTransforms();crowd.Tick(.02f);crowd.Tick(crowd.minimumLunch+.1f);
            Check(crowd.Returning,"Workers finish an ordinary meal and begin returning");
            var doors=game.GetComponentsInChildren<FactoryDoor>(true);
            for(int i=0;i<3000 && crowd.Active;i++)
            {
                crowd.Tick(.05f);
                foreach(var worker in crowd.workers)worker.TickMovement(.05f);
                foreach(var door in doors)door.Tick(.05f);
                Physics.SyncTransforms();
            }
            int returned=crowd.workers.Count(w=>w.transform.position.z>CardboardFactoryWorld.WorkshopSouth+1);
            Check(!crowd.Active && returned==crowd.workers.Length,"All workers physically return through the passage: "+returned+"/"+crowd.workers.Length);
            foreach(var worker in crowd.workers)
                if(worker.transform.position.z<=CardboardFactoryWorld.WorkshopSouth+1)report.Add("DETAIL stuck "+worker.name+" "+crowd.Describe(worker));
        }
        static void SingleLunch()
        {
            StartLunch();Serve();var lunch=game.lunch;var crowd=lunch.diners;
            crowd.Cancel();Vector3 trayPosition=lunch.tray.position;
            lunch.TickSchedule(89.9f);lunch.TickSchedule(90.1f);lunch.TickSchedule(600);
            Check(lunch.State==LunchState.Served && lunch.Attended && !crowd.Active,
                "Delivered lunch stays complete after multiple former repeat intervals");
            Check(Vector3.Distance(trayPosition,lunch.tray.position)<.001f,
                "Elapsed time never restores a delivered tray to the counter");
            game.mainMenu.Show();float elapsed=lunch.Elapsed;lunch.TickSchedule(500);
            Check(Mathf.Approximately(elapsed,lunch.Elapsed),"Pause-menu time does not advance the lunch schedule");Play();
            lunch.TickSchedule(1000);
            Check(lunch.State==LunchState.Served && !crowd.Active,"Resuming from the menu does not create another lunch");
            Place(player,lunch.pickup.position+Vector3.back);
            Check(!lunch.TryTake(),"The completed lunch tray cannot be collected a second time");
            StartLunch();lunch=game.lunch;crowd=lunch.diners;
            Place(player,lunch.pickup.position+Vector3.back);Check(lunch.TryTake(),"The shift's only tray can be picked up");
            lunch.Spill();crowd.Cancel();lunch.TickSchedule(1000);
            Check(lunch.State==LunchState.Spilled && Mathf.Approximately(game.Mistakes,1) && !crowd.Active,
                "Spilling lunch does not start another lunch or reset the one spill penalty");
            StartLunch();lunch=game.lunch;crowd=lunch.diners;
            lunch.TickSchedule(lunch.attendanceGrace+.1f);crowd.Cancel();lunch.TickSchedule(1000);
            Check(lunch.Missed && Mathf.Approximately(game.Mistakes,1.5f) && !game.CaptureActive && !crowd.Active,
                "Missing the only lunch charges attendance once without another bell or penalty");
            Reset();Repair();lunch.TickSchedule(lunch.lunchAfter);
            Check(lunch.State==LunchState.Ready && !lunch.Missed && !lunch.Attended && game.Mistakes==0,
                "A fresh shift restores its one ordinary lunch and attendance window");
            Reset();Repair();lunch.BeginFinalLunch();lunch.TickSchedule(1000);crowd.Tick(1000);
            Check(lunch.FinalLunch && crowd.Active && !crowd.Returning,
                "Final lunch remains available for the escape and never sends workers back");
        }
        static void NaturalLunchCycle()
        {
            StartLunch();Serve();var lunch=game.lunch;var crowd=lunch.diners;
            var doors=game.GetComponentsInChildren<FactoryDoor>(true);
            float clock=0;
            for(int frame=0;frame<3000 && crowd.Active;frame++)
            {
                lunch.TickSchedule(.05f);crowd.Tick(.05f);
                foreach(var worker in crowd.workers)worker.TickMovement(.05f);
                foreach(var door in doors)door.Tick(.05f);
                Physics.SyncTransforms();clock+=.05f;
            }
            int back=crowd.workers.Count(w=>w.transform.position.z>CardboardFactoryWorld.WorkshopSouth+1);
            Check(!crowd.Active && back==crowd.workers.Length,
                "All twelve workers physically return from the single shortened lunch");
            lunch.TickSchedule(1000);
            Check(lunch.State==LunchState.Served && !crowd.Active && game.Mistakes==0,
                "Workers remain at work after the only meal, without later lunch calls");
            report.Add("DETAIL one ordinary lunch completed in "+clock+" seconds");
        }
        static void AdvanceStation(float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)game.station.Tick(.02f);
        }
        static void ClearReleaseFrame()
        {
            // Timed machinery is advanced within one editor frame in these deterministic checks.
            typeof(FactoryWorkstation).GetField("releasedFrame",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game.station,-1);
        }
        static FactoryStationControl Aim(StationPart part,int number=0)
        {
            var station=game.station;
            var control=station.GetComponentsInChildren<FactoryStationControl>(true).First(c=>c.part==part && (number==0 || c.number==number));
            var point=control.GetComponent<Collider>().bounds.center;
            foreach(var offset in new[]{-station.transform.forward*1.25f+Vector3.up*.35f,-station.transform.forward*.7f+Vector3.up*.6f,
                -station.transform.forward*2+Vector3.up*.1f,Vector3.up*1.5f,station.transform.right*.7f-station.transform.forward*.7f})
            {
                Camera.main.transform.SetPositionAndRotation(point+offset,Quaternion.LookRotation(-offset));
                Physics.SyncTransforms();game.SendMessage("UpdateFocus");
                if(game.Focus==control.GetComponent<FactoryInteractable>())break;
            }
            game.station.GetComponent<FactoryStationHoverOutline>().Refresh();return control;
        }
        static void StationHover()
        {
            Reset();var station=game.station;var hover=station.GetComponent<FactoryStationHoverOutline>();
            Check(hover,"Player station installs the hover outline automatically");
            if(!hover)return;
            foreach(var part in new[]{StationPart.Feed,StationPart.Selector,StationPart.Wheel})
            {
                var control=Aim(part);
                Check(hover.Target==control && hover.VisibleMeshCount>0,"Aiming the camera outlines the available "+part+" model");
            }
            var source=hover.ModelRoot(hover.Target);var originalMaterials=source.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var shells=source.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Station hover shell").ToArray();
            Check(shells.Length>0 && shells.All(r=>!r.GetComponent<Collider>() && r.sharedMaterial.shader.name=="Cardboard/StationHoverOutline" &&
                r.GetComponent<MeshFilter>().sharedMesh==r.transform.parent.GetComponent<MeshFilter>().sharedMesh),
                "Outline follows the actual model meshes and adds no interaction colliders");
            game.mainMenu.Show();hover.Refresh();Check(!hover.Target && hover.VisibleMeshCount==0,"Station outlines hide in the menu");Play();ClearReleaseFrame();
            Aim(StationPart.Feed);int cached=station.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.name=="Station hover shell");
            Aim(StationPart.Selector);Aim(StationPart.Feed);
            Check(station.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.name=="Station hover shell")==cached,
                "Changing focus reuses outline renderers without duplicate geometry");
            Camera.main.transform.SetPositionAndRotation(new Vector3(100,2,100),Quaternion.identity);game.SendMessage("UpdateFocus");hover.Refresh();
            Check(!hover.Target && hover.VisibleMeshCount==0,"Looking away hides the previous outline");
            Aim(StationPart.Press);Check(!hover.Target,"An unavailable press control is not highlighted");
            Check(station.Operate(StationPart.Feed),"Feed starts the highlighted machine cycle");AdvanceStation(4);ClearReleaseFrame();
            Aim(StationPart.Press);Check(hover.Target && hover.Target.part==StationPart.Press && hover.VisibleMeshCount>0,"Press lever highlights when the sheet is loaded");
            while(station.SelectedType!=game.OrderedType)station.Operate(StationPart.Selector);
            station.Operate(StationPart.Press);AdvanceStation(1.1f);ClearReleaseFrame();
            Aim(StationPart.Guard);Check(hover.Target && hover.VisibleMeshCount>0 && hover.ModelRoot(hover.Target).name=="Grille lift button left of press",
                "Grille interaction outlines its physical lift button");
            station.Operate(StationPart.Guard);AdvanceStation(.7f);
            for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);
            station.TurnWheel(1080);station.Operate(StationPart.Guard);AdvanceStation(.7f);
            while(station.SelectedType!=game.OrderedType)station.Operate(StationPart.Selector);
            station.Operate(StationPart.Press);AdvanceStation(1.1f);ClearReleaseFrame();
            Aim(StationPart.Output);Check(hover.Target && hover.VisibleMeshCount>0,"Finished gear highlights when it can be collected");
            station.Operate(StationPart.Output);hover.Refresh();Check(!hover.Target,"Picking up the gear clears its output outline");
            Aim(StationPart.Bath,1);Check(hover.Target && hover.VisibleMeshCount>0,"An untreated carried gear enables the bath model outline");
            game.BeginDip();game.FinishDip(1);hover.Refresh();Check(!hover.Target,"A treated gear does not highlight an unavailable finishing bath");
            Check(originalMaterials.SequenceEqual(source.GetComponentInChildren<MeshRenderer>().sharedMaterials),"Highlighting preserves the source model materials");
            Reset();
        }
        static Color32[] RenderPixels(Camera camera,RenderTexture target)
        {
            camera.targetTexture=target;
            var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target};
            if(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request))UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            else camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();return image.GetPixels32();}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
        static void OutlineRendering()
        {
            var shader=Resources.Load<Shader>("StationHoverOutline");
            Check(shader && shader.isSupported,"Outline shader is available through Resources for player builds");
            if(!shader)return;
            var root=new GameObject("Outline rendering verification");root.transform.position=new Vector3(1000,1000,1000);
            var target=new RenderTexture(256,256,24);Material surface=null,outline=null;
            try
            {
                var camera=new GameObject("Verification camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
                camera.transform.localPosition=Vector3.back*3;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<14;
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(root.transform,false);sphere.layer=14;
                surface=new Material(Shader.Find("Universal Render Pipeline/Unlit"));surface.SetColor("_BaseColor",new Color(.15f,.2f,.15f));sphere.GetComponent<Renderer>().sharedMaterial=surface;
                var shell=new GameObject("Outline shell",typeof(MeshFilter),typeof(MeshRenderer));shell.transform.SetParent(sphere.transform,false);shell.layer=14;
                shell.GetComponent<MeshFilter>().sharedMesh=sphere.GetComponent<MeshFilter>().sharedMesh;
                outline=new Material(shader);var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=outline;
                renderer.enabled=false;var normal=RenderPixels(camera,target);renderer.enabled=true;var highlighted=RenderPixels(camera,target);
                int pixels=normal.Zip(highlighted,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>40?1:0).Sum();
                Check(pixels>100 && pixels<4000,"Rendered outline is a thin visible contour rather than a solid fill: "+pixels+" pixels");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.layer=14;
                wall.transform.localPosition=Vector3.back;wall.transform.localScale=new Vector3(3,3,.1f);wall.GetComponent<Renderer>().sharedMaterial=surface;
                renderer.enabled=false;normal=RenderPixels(camera,target);renderer.enabled=true;highlighted=RenderPixels(camera,target);
                pixels=normal.Zip(highlighted,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>40?1:0).Sum();
                Check(pixels==0,"Outline respects scene depth and stays hidden behind a solid obstruction");
                Check(!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"Outline shader compiles without errors");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);target.Release();UnityEngine.Object.DestroyImmediate(target);
                if(surface)UnityEngine.Object.DestroyImmediate(surface);if(outline)UnityEngine.Object.DestroyImmediate(outline);
            }
        }
        static void Victory()
        {
            Reset();for(int i=0;i<6;i++)Repair();var ending=game.ending;
            for(int i=0;i<110;i++)ending.Tick(.02f);
            var crowd=game.lunch.diners;
            for(int i=0;i<crowd.workers.Length;i++)Place(crowd.workers[i],crowd.places[i].position);
            Physics.SyncTransforms();
            float largestTurn=0;
            for(int i=0;i<1500 && ending.Phase!=FactoryEndingPhase.MenuReveal;i++)
            {
                bool running=ending.Running;float yaw=ending.CameraRotation.eulerAngles.y;ending.Tick(.02f);
                if(running && ending.Running)largestTurn=Mathf.Max(largestTurn,Mathf.Abs(Mathf.DeltaAngle(yaw,ending.CameraRotation.eulerAngles.y)));
            }
            Check(largestTurn>0 && largestTurn<=2.1f,"Escape camera smoothly follows route bends with a bounded turn rate: "+largestTurn+" degrees/frame");
            for(int i=0;i<45;i++)ending.Tick(.02f);
            Check(game.MenuActive && player.transform.position.x>CardboardFactoryWorld.WorkshopEast+14,
                "Victory reaches the corridor exit and returns to the menu");
            Play();
            Check(game.Repairs==0 && game.Mistakes==0 && !game.lunch.FinalLunch && !game.ReadyToLeave,
                "Play after victory starts a fresh shift instead of resuming the completed one");
            Check(game.intro.Phase==IntroPhase.Escort && ending.Phase==FactoryEndingPhase.None && game.lunch.State==LunchState.Waiting,
                "A new shift restarts the introduction and clears all ending and lunch state");
            for(int i=0;i<500;i++)game.intro.Tick(.02f);
            Check(game.intro.Phase==IntroPhase.Tutorial && player.transform.position.x<CardboardFactoryWorld.WorkshopEast &&
                !game.exitDoor.IsOpen && !ending.Active,"The new introduction brings the player into the workshop and closes the exit");
        }
        static void Finish()
        {
            SessionState.SetBool(Active,false);
            if(game){game.ResetShift();game.mainMenu.SendMessage("Close");}
            foreach(var entry in controls)if(entry.Key)entry.Key.enabled=entry.Value;
            controls.Clear();Time.timeScale=timeScale;Physics.simulationMode=simulation;Application.runInBackground=background;
            File.WriteAllLines(Report,new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>120){report.Add("FAIL verification timeout");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                game=CardboardFactoryGame.Instance;player=CardboardRunner.Player;
                Disable(game);Disable(game.mainMenu);Disable(game.intro);Disable(game.ending);Disable(game.captureSequence);
                Disable(game.lunch);Disable(game.lunch.diners);Disable(game.station);Disable(game.workbench);
                Disable(Camera.main.GetComponent<CardboardCamera>());
                foreach(var runner in game.crowd.runners)Disable(runner);

                Time.timeScale=0;Physics.simulationMode=SimulationMode.Script;
                Check(Mathf.Approximately(game.lunch.diners.minimumLunch,27.5f) && Mathf.Approximately(game.lunch.diners.afterPlayer,7.5f) &&
                    Mathf.Approximately(game.lunch.diners.maximumLunch,35),"Saved scene halves all ordinary crowd lunch timers");
                Counter();Crowd();SingleLunch();NaturalLunchCycle();StationHover();OutlineRendering();Victory();Finish();
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}