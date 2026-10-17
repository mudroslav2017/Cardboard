using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryWorkstationVerification
    {
        static readonly List<string> report = new List<string>();
        static bool running, oldBackground;
        static float start;
        static int inputStage,jumps,lastInputFrame,feeds;
        static float inputStarted,idleUntil;
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior oldInputBackground;
        static FactoryWorkstationVerification() { EditorApplication.update += Update; }
        [MenuItem("Cardboard/Verify factory gameplay")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != CardboardFactorySceneBuilder.ScenePath) EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);
            report.Clear(); running = true; start = 0; inputStage=0; feeds=0; oldBackground = Application.runInBackground;
            oldInputBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived+=OnLog;
            Application.runInBackground = true; EditorApplication.isPlaying = true;
        }
        static void Check(bool condition, string detail) { report.Add((condition ? "PASS " : "FAIL ") + detail); }
        static void OnLog(string message,string trace,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) report.Add("FAIL Unity log: "+message+"\n"+trace); }
        static void Feed(FactoryWorkstation station)
        {
            Quaternion home=station.feedLever.localRotation;
            Check(station.Operate(StationPart.Feed), "Feed lever starts stroke");
            Check(station.Stage==StationStage.Empty && station.LeverTravel==0 && station.operatorHand.Grip==station.feedGrip, "Feed waits for downstroke and hand attaches to handle");
            station.Tick(.175f);
            Check(station.Stage==StationStage.Empty && station.LeverTravel>.4f && station.LeverTravel<.6f && Quaternion.Angle(home,station.feedLever.localRotation)>20, "Lever smoothly moves through midpoint");
            var cam=Camera.main.transform;
            cam.position=station.transform.TransformPoint(new Vector3(-2.7f,1.6f,-2.5f));
            cam.rotation=Quaternion.LookRotation(station.feedGrip.position-cam.position);
            station.operatorHand.SyncPose();
            if(feeds++==0) CardboardFactorySceneBuilder.Capture("LeverHand");
            Check(Mathf.Abs(station.operatorHand.palm.lossyScale.x-1)<.01f,"Hand keeps physical scale under imported FBX handle");
            Check(!station.Operate(StationPart.Feed),"Repeated input cannot overlap a lever stroke");
            station.Tick(.2f);
            Check(station.Stage==StationStage.Dropping && station.sheet.gameObject.activeSelf, "Downstroke drops visible blank");
            station.Tick(.45f);
            Check(station.Stage==StationStage.Conveying,"Cardboard falls onto static conveyor");
            station.Tick(.2f);
            Check(!station.LeverAnimating && station.operatorHand.Grip==null && Quaternion.Angle(home,station.feedLever.localRotation)<.01f,"Lever smoothly returns and releases hand");
            station.Tick(2.3f);
            Check(station.Stage == StationStage.Loaded && Vector3.Distance(station.sheet.localPosition, station.cuttingPosition) < .01f, "Conveyor delivers cardboard to table center");
        }
        static void Arm(FactoryWorkstation station)
        {
            Vector3 openPress=station.press.localPosition;
            Check(station.Operate(StationPart.Press), "Second lever starts clamping stroke");
            station.Tick(.2f);
            Check(station.operatorHand.Grip==station.pressGrip && !station.PressClosed && station.LeverTravel>0,"Hand pulls second lever before press engages");
            station.Tick(.9f);
            Check(Vector3.Distance(openPress,station.press.localPosition)>.64f,"Imported press travels down to blank");
            Vector3 before = station.guardModel.position;
            Check(station.Operate(StationPart.Guard), "Separate tabletop button raises grille"); station.Tick(.7f);
            Check(station.GuardBlend == 1 && station.guardClip && Vector3.Distance(before, station.guardModel.position) > .1f, "Imported Zabralo animation changes pose");
            Quaternion wheelBefore=station.wheel.localRotation,baseBefore=station.wheelBase.localRotation;
            Check(!station.TurnWheel(45) && station.CutProgress == 0, "Cutting requires pressure");
            Check(Quaternion.Angle(wheelBefore,station.wheel.localRotation)>44 && Quaternion.Angle(baseBefore,station.wheelBase.localRotation)<.01f,"Round child rotates while wheel base stays still");
            Check(!station.TryPump(station.transform.TransformPoint(new Vector3(2, 0, -1.5f))), "Landing beside plate does not pump");
            for (int j = 0; j < 4; j++) Check(station.TryPump(station.pumpPlate.position), "Landing pumps step " + (j + 1));
            Check(station.PressureReady && !station.TryPump(station.pumpPlate.position), "Four landings fill pressure without overflow");
        }
        static void Wheel(FactoryWorkstation station)
        {
            var cam = Camera.main.transform;
            cam.position = station.transform.TransformPoint(new Vector3(2.049f, .85f, -2.4f));
            cam.rotation = Quaternion.LookRotation(station.WheelCenter - cam.position);
            Vector2 center = Camera.main.WorldToScreenPoint(station.WheelCenter);
            station.BeginWheel(center + Vector2.right * 90);
            Check(station.Manipulating && CardboardFactoryGame.Instance.PlayerBlocked && station.operatorHand.Grip==station.wheelGrip,"Wheel grip attaches hand and blocks camera movement");
            Vector3 handBefore=station.wheelGrip.position;
            for (int step = 1; step <= 72; step++)
            {
                float angle = -step * 15 * Mathf.Deg2Rad;
                station.DragWheel(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 90);
                if(step==6)
                {
                    Check(Vector3.Distance(handBefore,station.wheelGrip.position)>.5f,"Hand follows round rim through quarter turn");
                    if(CardboardFactoryGame.Instance.CurrentOrder==1) CardboardFactorySceneBuilder.Capture("WheelHand");
                }
            }
            station.EndGrip(false);
            Check(station.Stage == StationStage.Cut && station.CutProgress > .999f, "Three clockwise mouse revolutions cut gear");
            Check(!station.GuardClosed && station.operatorHand.Grip==null,"Finished cut automatically lowers grille and releases hand");
        }
        static void Take(FactoryWorkstation station)
        {
            Check(!station.Operate(StationPart.Output), "Closed press blocks pickup");
            station.Tick(.7f);
            Check(station.GuardBlend==0,"Grille automatically completes descent");
            Check(station.Operate(StationPart.Press), "Second lever releases gear"); station.Tick(1.1f);
            Check(station.Operate(StationPart.Output) && CardboardFactoryGame.Instance.HasPart, "Player takes uncoated gear");
        }
        static void CheckNewScene(CardboardFactoryGame game)
        {
            var station=game.station;
            Check(game.stations.Length==4 && game.stations[1]==station && station.isPlayerStation,"Four workstations; player is second from left");
            for(int i=0;i<4;i++) Check(game.stations[i].isPlayerStation==(i==1),"Only player station accepts controls: "+i);
            Check(GameObject.Find("Shipping carton")==null,"Old shipping cartons removed");
            Check(game.captureStage.position.x>900 && game.victim.IsChildOf(game.captureStage) && game.pressHead.IsChildOf(game.captureStage),"Old capture press and board outside workshop");
            Check(GameObject.Find("Empty canteen") && GameObject.Find("Canteen doorway floor"),"Empty canteen has connected doorway");
            Check(!Physics.Linecast(new Vector3(-13,1,-12),new Vector3(-13,1,-20),1<<8),"Canteen passage is physically open");
            foreach(float z in new[]{-13f,-15f,-17f,-24f}) Check(Physics.Raycast(new Vector3(-13,1,z),Vector3.down,2,1<<8),"Canteen route has solid floor at "+z);
            Check(station.sheet.localScale.y==.07f,"Cardboard blank thickness doubled");
            Vector3 gauge=station.transform.InverseTransformPoint(station.pressureNeedle.parent.position);
            Check(gauge.z>.70f && gauge.y>2,"Pressure gauge mounted on wall behind table");
            var guardControl=Array.Find(station.GetComponentsInChildren<FactoryStationControl>(),c=>c.part==StationPart.Guard);
            Vector3 button=station.transform.InverseTransformPoint(guardControl.transform.position);
            Check(button.x<-.5f && button.y>1 && button.y<1.2f && guardControl.GetComponent<BoxCollider>()!=station.guardHit,"Separate grille button sits left of press");
            for(int i=0;i<3;i++)
            {
                var prefab=Resources.Load<GameObject>("FactoryGears/Gear"+(i+1));
                Check(prefab && prefab.GetComponent<MeshFilter>().sharedMesh.uv.Length>0,"Imported gear with UV present: "+(i+1));
                if(prefab)
                {
                    var material=prefab.GetComponent<Renderer>().sharedMaterial;
                    Check(material.GetTexture("_BaseMap") && material.GetFloat("_Cull")==0 && material.GetFloat("_Smoothness")==0,"Gear original texture, double sided, fully rough: "+(i+1));
                }
                Check(station.baths[i].Find("Imported Box vessel"),"Bath uses imported Box model: "+(i+1));
            }
        }
        static void Update()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var game = CardboardFactoryGame.Instance;
            if (!game || !CardboardRunner.Player) return;
            if(inputStage>0) { TestJumpInput(game);return; }
            if (start == 0) { start = Time.time; return; }
            if (Time.time < start + .15f) return;
            try
            {
                var station = game.station;
                if (!station) throw new InvalidOperationException("Rebuild FactoryShift before verification.");
                game.ResetShift();
                ImportedCharacterInstaller.VerifyRuntime(Check);
                Check(game.workbench == null && UnityEngine.Object.FindObjectsByType<FactoryWorkbench>(FindObjectsSortMode.None).Length == 0, "Legacy machine absent from active scene");
                Check(UnityEngine.Object.FindObjectsByType<FactoryHUD>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<CardboardHUD>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0, "No screen HUD or Canvas");
                Check(GameObject.Find("Spare cardboard disguise locker") == null, "Disguise replacement removed");
                Check(station.baths.Length == 3 && station.guardClip, "Animated guard and three finishing baths present");
                CheckNewScene(game);
                Check(game.CurrentOrder==1 && station.QueuedOrder==1,"First order queues automatically on game start");
                station.Tick(.7f);
                var visualAudit=new List<string>();
                foreach(var renderer in station.orderCard.GetComponentsInChildren<Renderer>()) visualAudit.Add(renderer.name+" position="+renderer.transform.position+" bounds="+renderer.bounds+" scale="+renderer.transform.lossyScale);
                File.WriteAllLines("Library/StationVisualAudit.txt",visualAudit);
                Check(station.OrderNumber==1 && station.OrderTravel==1 && station.orderCard.gameObject.activeSelf && station.orderNumber.text=="1","First numbered punchcard emerges at workstation");
                CardboardFactorySceneBuilder.Capture("AutomaticOrder");
                foreach (var control in station.GetComponentsInChildren<FactoryStationControl>())
                {
                    var collider = control.GetComponent<BoxCollider>(); Vector3 target = control.transform.TransformPoint(collider.center);
                    Vector3 origin = target + station.transform.TransformDirection(Vector3.back) * 1.8f;
                    var hit = game.TargetForRay(new Ray(origin, target - origin));
                    Check(hit && hit.GetComponent<FactoryStationControl>() == control, "E ray reaches " + control.part + " " + control.number);
                }
                Check(!station.Operate(StationPart.Press) && !station.Operate(StationPart.Guard), "Empty machine cannot close");
                Feed(station); Check(!station.Operate(StationPart.Feed), "Occupied machine blocks overlapping feed"); Arm(station);
                Check(!station.Operate(StationPart.Selector) && !station.Operate(StationPart.Press), "Closed machine locks selector and press");
                station.TurnWheel(360); Check(station.CutProgress > .33f && station.CutProgress < .34f, "One turn cuts one third");
                Camera.main.transform.position = station.transform.TransformPoint(new Vector3(0, 2.6f, -5.6f));
                Camera.main.transform.rotation = Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(0,1.3f,0))-Camera.main.transform.position);
                CardboardFactorySceneBuilder.Capture("NewWorkbenchCutting");
                station.TurnWheel(-180); Check(station.CutProgress > .16f && station.CutProgress < .17f, "Reverse motion reverses cutting progress");
                station.Operate(StationPart.Guard); station.Tick(.7f);
                Check(station.Pressure == 0 && !station.TurnWheel(360), "Opening guard releases pressure"); game.ResetShift();
                for (int type = 1; type <= 3; type++)
                {
                    Check(game.CurrentOrder==type && game.Memorized==game.gears[type-1],"Sequential order "+type+" selected automatically");
                    station.Tick(.5f);station.Tick(.7f);
                    Check(station.OrderNumber==type && station.OrderTravel==1 && station.orderNumber.text==type.ToString(),"Printed card matches order "+type);
                    for (int step = 0; step < 3 && station.SelectedType != type; step++) station.Operate(StationPart.Selector);
                    Feed(station); Arm(station); Wheel(station); Take(station);
                    Check(game.HeldRecipe.Type == type && game.HeldRecipe.finish == 0, "Type " + type + " still needs finishing");
                    if (type == 1) Check(!game.TryInstall(game.gears[0]), "Raw gear cannot repair socket");
                    Check(station.Operate(StationPart.Bath, type) && game.DippingPart, "Bath " + type + " starts visible immersion");
                    Check(!station.Operate(StationPart.Feed), "Immersion blocks feeding another sheet"); station.Tick(1.2f);
                    Check(!game.DippingPart && game.HeldRecipe.finish == type, "Bath applies finish " + type);
                    Check(!station.Operate(StationPart.Bath, type % 3 + 1), "Finished part cannot be silently recoated");
                    Check(game.TryInstall(game.gears[type - 1]) && game.Repairs == type, "Correct part repairs socket " + type);
                    Check(game.ReadyToLeave == (type == 3), "Exit requires all three repairs");
                }
                Check(game.TryExit() && game.State == FactoryState.Escaped, "Full punchcard-to-exit loop succeeds");
                game.ResetShift(); game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1, false)); station.Operate(StationPart.Bath, 2); station.Tick(1.2f);
                Check(!game.TryInstall(game.gears[0]) && game.Mistakes == 1, "Wrong bath finish is rejected");
                game.DropPart(); Check(!game.HasPart, "Wrong part can be discarded");
                game.ResetShift(); game.AddSuspicion(100); Check(game.State == FactoryState.Chased, "Suspicion still triggers chase");
                foreach (FactoryDeath ending in Enum.GetValues(typeof(FactoryDeath)))
                { game.ResetShift(); game.Capture(ending); Check(game.State == FactoryState.Captured && game.CameraOverride, "Capture retained: " + ending); }
                game.ResetShift();station.Tick(.7f);
                Camera.main.transform.position = station.transform.TransformPoint(new Vector3(0,2.9f,-6.7f));
                Camera.main.transform.rotation = Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(.4f,1.4f,0)) - Camera.main.transform.position);
                CardboardFactorySceneBuilder.Capture("NewWorkbenchVerified");
                Feed(station);station.Operate(StationPart.Press);station.Tick(1.1f);station.Operate(StationPart.Guard);station.Tick(.7f);
                var body=CardboardRunner.Player.GetComponent<CharacterController>();body.enabled=false;
                CardboardRunner.Player.transform.position=new Vector3(station.pumpPlate.position.x,0,station.pumpPlate.position.z);body.enabled=true;
                keyboard=InputSystem.AddDevice<Keyboard>();jumps=0;inputStage=1;inputStarted=Time.realtimeSinceStartup;lastInputFrame=Time.frameCount;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
            }
            catch (Exception error) { report.Add("FAIL " + error); }
            finally
            {
                if(inputStage==0) Finish();
            }
        }
        static void TestJumpInput(CardboardFactoryGame game)
        {
            if(Time.frameCount==lastInputFrame) return;lastInputFrame=Time.frameCount;
            if(Time.realtimeSinceStartup-inputStarted>12) {Check(false,"Physical jump input timed out at stage "+inputStage);Finish();return;}
            try
            {
                float jump=CardboardRunner.Player.JumpHeight;
                if(inputStage==1 && jump>.15f)
                {InputSystem.QueueStateEvent(keyboard,new KeyboardState());inputStage=2;}
                else if(inputStage==2 && jump==0)
                {
                    jumps++;Check(Mathf.Abs(game.station.Pressure-jumps*.25f)<.001f,"Real Space jump pumps exactly once at landing "+jumps);
                    idleUntil=Time.time+.2f;inputStage=3;
                }
                else if(inputStage==3 && Time.time>=idleUntil)
                {
                    Check(Mathf.Abs(game.station.Pressure-jumps*.25f)<.001f,"Standing on panel adds no pressure");
                    if(jumps<4) {InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));inputStage=1;}
                    else
                    {
                        Check(game.Suspicion<.1f,"Legitimate pump jumps do not raise suspicion");
                        game.ReadBlueprint(game.gears[0]);game.station.TurnWheel(360);inputStage=4;
                    }
                }
                else if(inputStage==4)
                {
                    Check(game.station.progressFill.localScale.x>.36f,"Cutting progress retains visible world-space width after scene reload");
                    CardboardFactorySceneBuilder.Capture("NewWorkbenchFirstPerson");
                    Camera.main.transform.position=game.station.transform.TransformPoint(new Vector3(0,2.6f,-5.6f));
                    Camera.main.transform.rotation=Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.3f,0))-Camera.main.transform.position);
                    CardboardFactorySceneBuilder.Capture("NewWorkbenchCutting");
                    var station=game.station;var body=CardboardRunner.Player.GetComponent<CharacterController>();
                    body.enabled=false;CardboardRunner.Player.transform.position=station.transform.TransformPoint(new Vector3(2.049f,0,-2.4f));body.enabled=true;
                    Camera.main.transform.position=CardboardRunner.Player.transform.position+Vector3.up*1.62f;
                    Camera.main.transform.rotation=Quaternion.LookRotation(station.WheelCenter-Camera.main.transform.position);
                    station.Operate(StationPart.Wheel);idleUntil=Time.time+.18f;inputStage=5;
                }
                else if(inputStage==5 && Time.time>=idleUntil)
                {
                    var hand=game.station.operatorHand;
                    Check(hand.Grip==game.station.wheelGrip && hand.palm.gameObject.activeInHierarchy,"Hand remains attached during a real animated wheel turn");
                    Check(Mathf.Abs(hand.palm.lossyScale.x-1)<.01f && Mathf.Abs(hand.palm.lossyScale.y-1)<.01f,"Wheel FBX preserves human hand proportions");
                    CardboardFactorySceneBuilder.Capture("WheelHandLive");inputStage=6;
                }
                else if(inputStage==6 && !game.station.Manipulating)
                {
                    game.ResetShift();var station=game.station;var body=CardboardRunner.Player.GetComponent<CharacterController>();
                    body.enabled=false;CardboardRunner.Player.transform.position=station.transform.TransformPoint(new Vector3(-2.7f,0,-2.5f));body.enabled=true;
                    Camera.main.transform.position=CardboardRunner.Player.transform.position+Vector3.up*1.62f;
                    Camera.main.transform.rotation=Quaternion.LookRotation(station.feedGrip.position-Camera.main.transform.position);
                    station.Operate(StationPart.Feed);idleUntil=Time.time+.18f;inputStage=7;
                }
                else if(inputStage==7 && Time.time>=idleUntil)
                {
                    Check(game.station.operatorHand.Grip==game.station.feedGrip && game.station.LeverTravel>0,"Live lever downstroke retains hand grip");
                    CardboardFactorySceneBuilder.Capture("LeverHandLive");Finish();
                }
            }
            catch(Exception error) {report.Add("FAIL "+error);Finish();}
        }
        static void Finish()
        {
            Application.logMessageReceived-=OnLog;
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);keyboard=null;
            File.WriteAllLines("Library/FactoryVerification.txt",report);running=false;inputStage=0;
            InputSystem.settings.backgroundBehavior=oldInputBackground;
            Application.runInBackground=oldBackground;EditorApplication.isPlaying=false;
            if(Application.isBatchMode) EditorApplication.Exit(report.Exists(line=>line.StartsWith("FAIL "))?1:0);
        }
    }
}
