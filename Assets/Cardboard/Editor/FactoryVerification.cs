using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryVerification
    {
        static readonly List<string> report=new List<string>();
        static bool running,oldBackground;
        static InputSettings.BackgroundBehavior oldInputBackground;
        static Keyboard keyboard;
        static CardboardFactoryGame game;
        static int stage,repairPass,death;
        static float started;
        static FactoryGear assembling;
        static Ray ControlRay(FactoryWorkbench bench,WorkbenchPart part)
        {
            Physics.SyncTransforms();
            foreach(var control in bench.GetComponentsInChildren<WorkbenchControl>())
                if(control.part==part)
                { var p=control.GetComponent<Collider>().bounds.center;return new Ray(Camera.main.transform.position,p-Camera.main.transform.position); }
            throw new Exception("Missing machine control "+part);
        }
        static Vector2 WheelPoint(FactoryWorkbench bench,float degrees)
        {
            Vector2 center=Camera.main.WorldToScreenPoint(bench.flywheel.position);
            float a=degrees*Mathf.Deg2Rad;return center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Screen.height*.1f;
        }
        static void Crank(FactoryWorkbench bench,float degrees)
        {
            Check(bench.TryGrip(ControlRay(bench,WorkbenchPart.Crank),WheelPoint(bench,0)),"First-person ray grips the physical flywheel");
            int samples=Mathf.CeilToInt(Mathf.Abs(degrees)/15);
            for(int i=1;i<=samples;i++) bench.DragTo(WheelPoint(bench,-degrees*i/samples));
            bench.EndGrip();
        }
        static void Cut(FactoryWorkbench bench,float travel=1)
        {
            Vector2 start=new Vector2(Screen.width*.5f,Screen.height*.75f);
            Check(bench.TryGrip(ControlRay(bench,WorkbenchPart.Cutter),start),"First-person ray grips the cutting lever");
            bench.DragTo(start-Vector2.up*(Screen.height*.26f*travel));bench.EndGrip();
        }
        static void MachineSafety(FactoryWorkbench bench)
        {
            Check(!bench.Operate(WorkbenchPart.Clamp) && !bench.Operate(WorkbenchPart.Guard),"Empty packet cannot be clamped or connected to the drive");
            Crank(bench,360);Check(bench.Winding==0,"Unclamped flywheel freewheels without producing a gear");
            Cut(bench);Check(!bench.Finished,"Cutting lever stops without winding and guarding");
            for(int i=0;i<4;i++) { bench.Operate(WorkbenchPart.Feed);bench.Operate(WorkbenchPart.Feed); }
            Check(bench.Jammed && bench.Layers==3,"Fourth feed jams the machine instead of silently clamping the layer count");
            Check(!bench.Operate(WorkbenchPart.Clamp),"Jammed packet cannot be clamped");
            Check(bench.Operate(WorkbenchPart.Reject) && !bench.Jammed && bench.Layers==0,"Physical scrap ejector clears a feed jam");
        }
        static void Stroke(FactoryWorkbench bench,WorkbenchPart part,float direction,float travel=1)
        {
            Vector2 start=new Vector2(Screen.width*.5f,Screen.height*.5f);
            Check(bench.TryGrip(ControlRay(bench,part),start),"Mouse grabs physical "+part+" without an E shortcut");
            bench.DragTo(start+Vector2.up*Screen.height*.16f*direction*travel);bench.EndGrip();
        }
        static void Dial(FactoryWorkbench bench,WorkbenchPart part,float degrees)
        {
            Transform pivot=part==WorkbenchPart.Teeth?bench.toothDial:bench.hubDial;
            Vector2 center=Camera.main.WorldToScreenPoint(pivot.position);float radius=Screen.height*.07f;
            Check(bench.TryGrip(ControlRay(bench,part),center+Vector2.right*radius),"Mouse grips rotating "+part+" selector");
            int count=Mathf.CeilToInt(Mathf.Abs(degrees)/15);
            for(int i=1;i<=count;i++)
            { float a=-degrees*i/count*Mathf.Deg2Rad;bench.DragTo(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius); }
            bench.EndGrip();
        }
        static void MouseMachine(FactoryWorkbench bench)
        {
            Quaternion leverHome=bench.feedLever.localRotation;
            Vector2 start=new Vector2(Screen.width*.5f,Screen.height*.5f);
            Check(bench.TryGrip(ControlRay(bench,WorkbenchPart.Feed),start),"Free cursor can grab the feed control directly");
            bench.DragTo(start+Vector2.up*Screen.height*.08f);
            Check(!bench.FeedRaised && bench.Layers==0 && Quaternion.Angle(leverHome,bench.feedLever.localRotation)>20,"Partial hand stroke moves lever continuously without feeding");
            Check(bench.gripHand.gameObject.activeSelf && game.PlayerBlocked,"Visible hand grips machine and camera movement is blocked");
            bench.EndGrip(false);
            Check(!bench.Manipulating && Quaternion.Angle(leverHome,bench.feedLever.localRotation)<.1f && !bench.gripHand.gameObject.activeSelf,"Releasing an unfinished stroke restores the lever and hand");
            Stroke(bench,WorkbenchPart.Feed,1);
            Check(bench.FeedRaised && bench.feedRollerA.localRotation!=Quaternion.identity,"Full upward stroke raises the feed and turns its rollers");
            Stroke(bench,WorkbenchPart.Feed,-1);
            Check(bench.Layers==1,"Full downward return feeds exactly one sheet");
            Stroke(bench,WorkbenchPart.Reject,-1);
            Check(bench.Layers==0 && bench.scraps[0].gameObject.activeSelf,"Pulling reject handle visibly ejects the packet");
            Dial(bench,WorkbenchPart.Teeth,135);Check(bench.Teeth==12,"Clockwise hand rotation selects the next tooth die");
            Dial(bench,WorkbenchPart.Teeth,-135);Check(bench.Teeth==8,"Reverse hand rotation selects the previous tooth die");
            Dial(bench,WorkbenchPart.Teeth,60);Check(bench.Teeth==8,"Releasing between detents does not silently select another recipe");
            Dial(bench,WorkbenchPart.Hub,135);Check(bench.Hub==FactoryHub.Hexagon,"Axle selector physically rotates to the hexagonal die");
            Dial(bench,WorkbenchPart.Hub,-135);Check(bench.Hub==FactoryHub.Square,"Axle selector can be turned back");
            Stroke(bench,WorkbenchPart.Feed,1);Stroke(bench,WorkbenchPart.Feed,-1);
            float openClamp=bench.clampPlaten.localPosition.y;
            Stroke(bench,WorkbenchPart.Clamp,-1);
            Check(bench.Clamped && bench.clampPlaten.localPosition.y<openClamp,"Pulling clamp lever lowers the actual clamping platen");
            float openGuard=bench.guard.localPosition.y;
            Stroke(bench,WorkbenchPart.Guard,1);
            Check(bench.GuardClosed && bench.guard.localPosition.y>openGuard,"Dragging safety control raises the physical guard");
            Stroke(bench,WorkbenchPart.Feed,1);Check(!bench.FeedRaised && bench.Layers==1,"Mechanical interlock also blocks mouse-operated feed");
            Crank(bench,1110);
            Check(bench.Winding>=1079.9f && bench.driveGearA.localRotation!=Quaternion.identity,"Flywheel drives visible transmission and winds the spring");
            float bladeHome=bench.cuttingHead.localPosition.y;
            Cut(bench);
            Check(bench.Finished && bench.cuttingHead.localPosition.y<bladeHome,"Pulling cutter drives its head through the cardboard");
            CardboardFactorySceneBuilder.Capture("WorkbenchCutting");
            Stroke(bench,WorkbenchPart.Guard,-1);Stroke(bench,WorkbenchPart.Clamp,1);
            Stroke(bench,WorkbenchPart.Output,-1);
            Check(game.HasPart && !bench.Finished && game.HeldRecipe.Equals(new FactoryRecipe(8,FactoryHub.Square,1)),"Entire assembly works using mouse gestures, including pulling out the finished gear");
            game.DropPart();
        }
        static FactoryVerification() { EditorApplication.update+=Tick; }
        [MenuItem("Cardboard/Verify factory gameplay")]
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(SceneManager.GetActiveScene().path!=CardboardFactorySceneBuilder.ScenePath)
                EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);
            report.Clear();stage=0;repairPass=0;death=0;running=true;
            oldBackground=Application.runInBackground;oldInputBackground=InputSystem.settings.backgroundBehavior;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;
        }
        static void Check(bool valid,string detail) { report.Add((valid?"PASS ":"FAIL ")+detail); }
        static void Craft(FactoryGear target,bool wrong=false)
        {
            assembling=target;game.ReadBlueprint(target);var bench=game.workbench;
            var cam=Camera.main.transform;Vector3 position=cam.position;Quaternion rotation=cam.rotation;
            cam.position=bench.transform.position+new Vector3(0,1.85f,3.2f);
            cam.rotation=Quaternion.LookRotation(bench.transform.position+new Vector3(0,1.75f,.4f)-cam.position);
            int teeth=wrong?8:target.recipe.teeth;var hub=wrong?FactoryHub.Square:target.recipe.hub;int layers=wrong?1:target.recipe.layers;
            for(int i=0;i<3 && bench.Teeth!=teeth;i++) bench.Operate(WorkbenchPart.Teeth);
            for(int i=0;i<3 && bench.Hub!=hub;i++) bench.Operate(WorkbenchPart.Hub);
            for(int i=0;i<layers;i++) { bench.Operate(WorkbenchPart.Feed);bench.Operate(WorkbenchPart.Feed); }
            Check(bench.Layers==layers && bench.sheets[layers-1].gameObject.activeSelf,"Each return stroke physically feeds one visible cardboard layer");
            Check(bench.Operate(WorkbenchPart.Clamp) && bench.Operate(WorkbenchPart.Guard),"Clamp and guard mechanically engage the drive");
            Check(!bench.Operate(WorkbenchPart.Teeth) && !bench.Operate(WorkbenchPart.Feed),"Clamping physically locks selectors and feed");
            Crank(bench,1110);Check(bench.Winding>=1079.9f,"Three circular mouse revolutions wind the ratchet");
            if(stage==0)
            {
                Crank(bench,-90);Check(bench.Winding<1080,"Reverse hand rotation unwinds the spring");Crank(bench,120);
                Cut(bench,.5f);Check(!bench.Finished && bench.CutTravel==0,"Incomplete cutting stroke springs back without a part");
                bench.Operate(WorkbenchPart.Guard);Check(bench.Winding==0,"Opening the guard releases the spring");
                bench.Operate(WorkbenchPart.Guard);Crank(bench,1110);
            }
            Cut(bench);
            Check(bench.Finished && !game.HasPart,"Cut gear remains in the machine until manually removed");
            Check(!bench.Operate(WorkbenchPart.Output),"Closed guard blocks taking the finished gear");
            bench.Operate(WorkbenchPart.Guard);bench.Operate(WorkbenchPart.Clamp);
            Check(bench.Operate(WorkbenchPart.Output) && game.HasPart,"Opening guard and clamp allows physical gear pickup");
            cam.SetPositionAndRotation(position,rotation);started=Time.time;
        }
        static void Tick()
        {
            if(!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if(stage==0)
                {
                    game=CardboardFactoryGame.Instance;
                    if(!game || !CardboardRunner.Player || game.ShiftRemaining<=0) return;
                    Check(game.gears.Length==3 && game.WorkingGears==0,"Giant mechanism starts with 3 broken gear sockets");
                    int blueprints=0;
                    Physics.SyncTransforms();
                    foreach(var item in UnityEngine.Object.FindObjectsByType<FactoryInteractable>(FindObjectsSortMode.None))
                    {
                        if(item.action==FactoryAction.Blueprint)
                        { blueprints++;Check(item.gear && item.transform.position.y<item.gear.transform.position.y,"Instruction physically lies beneath its gear"); }
                        if(item.action==FactoryAction.Blueprint || item.action==FactoryAction.Disguise || item.action==FactoryAction.Exit)
                        {
                            Vector3 direction=item.action==FactoryAction.Exit?Vector3.forward:Vector3.back;
                            var target=game.TargetForRay(new Ray(item.transform.position-direction*2,direction));
                            Check(target && target.action==item.action,"First-person interaction ray reaches "+item.action);
                        }
                    }
                    Check(blueprints==3,"Every gear has an instruction sheet");
                    Check(game.Disguise==100 && game.Suspicion==0,"Player begins disguised as a cardboard employee");
                    Check(!game.TryExit(),"Shift exit is locked until work is complete");
                    Check(game.crowd.runners.Count==12,"Factory has a player and 11 cardboard coworkers");
                    Check(CardboardRunner.Player.GetComponent<CharacterController>(),"Factory movement collides with walls and workbenches");
                    Check(game.workbench && game.workbench.GetComponentsInChildren<WorkbenchControl>().Length==9,"Workbench has nine independent physical controls");
                    var view=Camera.main.transform;var savedPosition=view.position;var savedRotation=view.rotation;
                    view.position=game.workbench.transform.position+new Vector3(0,1.85f,3.2f);
                    view.rotation=Quaternion.LookRotation(game.workbench.transform.position+new Vector3(0,1.75f,.4f)-view.position);
                    foreach(var control in game.workbench.GetComponentsInChildren<WorkbenchControl>())
                    {
                        var hit=game.TargetForRay(ControlRay(game.workbench,control.part));
                        Check(hit && hit.GetComponent<WorkbenchControl>()==control,"Independent interaction ray reaches machine "+control.part);
                    }
                    MachineSafety(game.workbench);game.ResetShift();
                    MouseMachine(game.workbench);game.ResetShift();
                    view.SetPositionAndRotation(savedPosition,savedRotation);
                    keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
                    Craft(game.gears[0]);stage=1;
                }
                else if(stage==1 && Time.time-started>1.6f)
                {
                    Check(game.HasPart && game.HeldRecipe.Equals(assembling.recipe),"Manual machine produces the selected layered gear");
                    Check(game.TryInstall(assembling) && game.Repairs==1 && !assembling.broken,"Matching gear repairs and starts a mechanism");
                    Craft(game.gears[1],true);stage=2;
                }
                else if(stage==2 && Time.time-started>1.6f)
                {
                    Check(!game.TryInstall(assembling) && assembling.broken,"Wrong tooth, hub or layer recipe is rejected");
                    Check(game.Mistakes==1 && game.Suspicion>=27 && game.Disguise<100,"Mistakes increase coworker suspicion and damage the disguise");
                    game.DropPart();Check(!game.HasPart,"Incorrect part can be discarded");
                    Craft(game.gears[1]);stage=3;
                }
                else if(stage==3 && Time.time-started>1.6f)
                {
                    Check(game.TryInstall(assembling),"Second recipe installs successfully");
                    Craft(game.gears[2]);stage=4;
                }
                else if(stage==4 && Time.time-started>1.6f)
                {
                    Check(game.TryInstall(assembling) && game.WorkingGears==3,"All three gear recipes start the giant machine");
                    Check(game.BreakNextGear(),"A running machine can break another gear");
                    foreach(var gear in game.gears) if(gear.broken) { Craft(gear);break; }
                    stage=5;
                }
                else if(stage==5 && Time.time-started>1.6f)
                {
                    Check(game.TryInstall(assembling),"Replacement repairs a newly broken gear");repairPass++;
                    if(repairPass<3)
                    {
                        Check(game.BreakNextGear(),"Mechanical wear creates another repair job");
                        foreach(var gear in game.gears) if(gear.broken) { Craft(gear);break; }
                    }
                    else
                    {
                        Check(game.Repairs==6 && game.ReadyToLeave,"Six repairs and three healthy gears finish the work order");
                        Check(game.TryExit() && game.State==FactoryState.Escaped,"Completed shift can end with a successful escape");
                        game.ResetShift();game.AddSuspicion(20);game.StartInspection();
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));started=Time.time;stage=6;
                    }
                }
                else if(stage==6 && Time.time-started>2.3f)
                {
                    Check(!game.Inspection && game.Suspicion<7,"Holding still in cardboard disguise passes a coworker inspection");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    game.AddSuspicion(100);
                    Check(game.State==FactoryState.Chased,"Complete suspicion exposes the player and starts a chase");
                    Vector3 target=game.WorkerTarget(game.crowd.runners[1],out float speed);
                    Check(speed>4 && Vector3.Distance(target,CardboardRunner.Player.transform.position)<.1f,"Exposed workers hunt the player's actual position");
                    game.Capture(FactoryDeath.Buttons);started=Time.time;stage=7;
                }
                else if(stage==7 && Time.time-started>4.7f)
                {
                    Check(game.State==FactoryState.Dead && game.CameraOverride && game.victim.gameObject.activeSelf,"Capture plays a visible ending and locks first-person movement");
                    if(death==0)
                    {
                        Check(game.Death==FactoryDeath.Buttons && game.buttons.gameObject.activeSelf && game.buttons.GetChild(0).localPosition.z<0,"Button death drives actual button tacks into the cardboard victim");
                        CardboardFactorySceneBuilder.Capture("FactoryDeathButtons");game.ResetShift();game.Capture(FactoryDeath.Pins);death++;
                    }
                    else if(death==1)
                    {
                        Check(game.Death==FactoryDeath.Pins && game.pins.gameObject.activeSelf && game.pins.GetChild(0).localPosition.z<0,"Pin death drives the long pins into the cardboard victim");
                        CardboardFactorySceneBuilder.Capture("FactoryDeathPins");game.ResetShift();game.Capture(FactoryDeath.Press);death++;
                    }
                    else
                    {
                        Check(game.Death==FactoryDeath.Press && game.pressHead.localPosition.y<.7f && game.victim.localScale.z<.2f,"Press death lowers the piston and flattens the victim");
                        CardboardFactorySceneBuilder.Capture("FactoryDeathPress");Finish();return;
                    }
                    started=Time.time;
                }
            }
            catch(Exception error) { report.Add("FAIL "+error);Finish(); }
        }
        static void Finish()
        {
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);keyboard=null;
            File.WriteAllLines("Library/FactoryVerification.txt",report);running=false;
            InputSystem.settings.backgroundBehavior=oldInputBackground;Application.runInBackground=oldBackground;
            EditorApplication.isPlaying=false;
        }
    }
}
