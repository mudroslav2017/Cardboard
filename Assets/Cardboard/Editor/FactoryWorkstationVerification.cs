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
        static int inputStage,jumps,lastInputFrame;
        static float inputStarted,idleUntil;
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior oldInputBackground;
        static FactoryWorkstationVerification() { EditorApplication.update += Update; }
        [MenuItem("Cardboard/Verify factory gameplay")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != CardboardFactorySceneBuilder.ScenePath) EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);
            report.Clear(); running = true; start = 0; inputStage=0; oldBackground = Application.runInBackground;
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
            Check(station.Operate(StationPart.Feed), "Feed lever releases a blank");
            Check(station.Stage == StationStage.Dropping && station.sheet.gameObject.activeSelf, "Blank is visible below pipe");
            station.Tick(.65f);
            Check(station.Stage == StationStage.Conveying && Vector3.Distance(station.sheet.localPosition, station.conveyorPosition) < .01f, "Cardboard falls onto static conveyor");
            station.Tick(2.3f);
            Check(station.Stage == StationStage.Loaded && Vector3.Distance(station.sheet.localPosition, station.cuttingPosition) < .01f, "Conveyor delivers cardboard to table center");
        }
        static void Arm(FactoryWorkstation station)
        {
            Vector3 openPress=station.press.localPosition;
            Check(station.Operate(StationPart.Press), "Second lever lowers press"); station.Tick(.6f);
            Check(Vector3.Distance(openPress,station.press.localPosition)>.64f,"Imported press physically travels down to the blank");
            Vector3 before = station.guardModel.position;
            Check(station.Operate(StationPart.Guard), "Guard E interaction engages"); station.Tick(.7f);
            Check(station.GuardBlend == 1 && station.guardClip && Vector3.Distance(before, station.guardModel.position) > .1f, "Imported Zabralo animation changes its pose");
            Check(!station.TurnWheel(360) && station.CutProgress == 0, "Cutting requires pressure");
            Check(!station.TryPump(station.transform.TransformPoint(new Vector3(2, 0, -1.5f))), "Landing beside plate does not pump");
            for (int j = 0; j < 4; j++) Check(station.TryPump(station.pumpPlate.position), "Landing pumps step " + (j + 1));
            Check(station.PressureReady && !station.TryPump(station.pumpPlate.position), "Four landings fill pressure without overflow");
        }
        static void Wheel(FactoryWorkstation station)
        {
            var cam = Camera.main.transform;
            cam.position = station.transform.TransformPoint(new Vector3(2.049f, .85f, -2.4f));
            cam.rotation = Quaternion.LookRotation(station.wheel.position - cam.position);
            Vector2 center = Camera.main.WorldToScreenPoint(station.wheel.position);
            station.BeginWheel(center + Vector2.right * 90);
            Check(station.Manipulating && CardboardFactoryGame.Instance.PlayerBlocked, "Wheel grip blocks movement and camera");
            for (int step = 1; step <= 72; step++)
            {
                float angle = -step * 15 * Mathf.Deg2Rad;
                station.DragWheel(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 90);
            }
            station.EndGrip(false);
            Check(station.Stage == StationStage.Cut && station.CutProgress > .999f, "Three clockwise mouse revolutions cut gear");
        }
        static void Take(FactoryWorkstation station)
        {
            Check(!station.Operate(StationPart.Output), "Closed press and guard block pickup");
            Check(station.Operate(StationPart.Guard), "Guard reopens after cutting"); station.Tick(.7f);
            Check(station.Operate(StationPart.Press), "Press releases gear"); station.Tick(.6f);
            Check(station.Operate(StationPart.Output) && CardboardFactoryGame.Instance.HasPart, "Player takes uncoated gear");
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
                foreach (var gear in game.gears)
                { game.ReadBlueprint(gear); Check(game.Memorized == gear && gear.punchcard.gameObject.activeSelf, "Socket " + gear.recipe.Type + " prints numbered card"); }
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
                Camera.main.transform.position = new Vector3(-6, 2.6f, 6.1f);
                Camera.main.transform.rotation = Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(0,1.3f,0))-Camera.main.transform.position);
                CardboardFactorySceneBuilder.Capture("NewWorkbenchCutting");
                station.TurnWheel(-180); Check(station.CutProgress > .16f && station.CutProgress < .17f, "Reverse motion reverses cutting progress");
                station.Operate(StationPart.Guard); station.Tick(.7f);
                Check(station.Pressure == 0 && !station.TurnWheel(360), "Opening guard releases pressure"); game.ResetShift();
                for (int type = 1; type <= 3; type++)
                {
                    game.ReadBlueprint(game.gears[type - 1]);
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
                game.ResetShift(); Camera.main.transform.position = new Vector3(-6, 2.9f, 7.2f);
                Camera.main.transform.rotation = Quaternion.LookRotation(new Vector3(-6.3f, 1.4f, .5f) - Camera.main.transform.position);
                CardboardFactorySceneBuilder.Capture("NewWorkbenchVerified");
                Feed(station);station.Operate(StationPart.Press);station.Tick(.6f);station.Operate(StationPart.Guard);station.Tick(.7f);
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
                    Camera.main.transform.position=new Vector3(-6,2.6f,6.1f);
                    Camera.main.transform.rotation=Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.3f,0))-Camera.main.transform.position);
                    CardboardFactorySceneBuilder.Capture("NewWorkbenchCutting");Finish();
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
