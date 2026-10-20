using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactorySessionVerification
    {
        const string Active="Cardboard.SessionVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;
        static Keyboard keyboard,previousKeyboard;
        static Mouse mouse,previousMouse;
        static bool background;
        static InputSettings.BackgroundBehavior inputBackground;
        static SimulationMode simulation;
        static double started,pausedAt;
        static float clock,pausedTime,leverTravel;
        static Vector3 view,bodyPosition;
        static Quaternion rotation;
        static int stage,repairs,order;
        static FactorySessionVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactorySessionVerification.txt",new[]{"RESULT: "+report.Count(x=>x.StartsWith("PASS"))+" PASS, "+report.Count(x=>x.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        [MenuItem("Cardboard/Verify session, bath selection and lunch handling")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            RestoreControls();report.Clear();stage=0;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            inputBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            simulation=Physics.simulationMode;
            FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Active,true);
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void SetEscape(bool escape)
        {
            InputSystem.QueueStateEvent(keyboard,escape?new KeyboardState(Key.Escape):new KeyboardState());
            InputSystem.Update();
        }
        static void Escape(){SetEscape(true);game.SendMessage("Update");SetEscape(false);}
        static void Play()
        {
            game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);
            game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
        }
        static void RestoreControls()
        {
            if(!game)game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
            if(!game)return;
            game.enabled=game.mainMenu.enabled=game.intro.enabled=game.ending.enabled=game.captureSequence.enabled=game.station.enabled=true;
            if(Camera.main)Camera.main.GetComponent<CardboardCamera>().enabled=true;
            foreach(var worker in game.crowd.runners)worker.enabled=true;
        }
        static void Finish()
        {
            if(game){game.ResetShift();game.mainMenu.SendMessage("Close");}
            RestoreControls();Time.timeScale=1;Physics.simulationMode=simulation;
            Application.runInBackground=background;InputSystem.settings.backgroundBehavior=inputBackground;
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
            if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
            Save();SessionState.SetBool(Active,false);EditorApplication.isPlaying=false;
        }
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;
            game.enabled=false;game.mainMenu.enabled=false;game.intro.enabled=false;
            game.ending.enabled=false;game.captureSequence.enabled=false;game.station.enabled=false;
            Camera.main.GetComponent<CardboardCamera>().enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            keyboard=InputSystem.AddDevice<Keyboard>("Session verification keyboard");
            mouse=InputSystem.AddDevice<Mouse>("Session verification mouse");
            game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);
            Check(Cursor.lockState==CursorLockMode.Locked && !Cursor.visible,"Play captures and hides the cursor immediately");
            game.mainMenu.SendMessage("Update");
            Check(Cursor.lockState==CursorLockMode.Locked,"Menu Update keeps mouse capture during the Play transition");
            game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
            Check(game.intro.Phase==IntroPhase.Escort && !game.MenuActive,"First Play starts the introduction");
            Camera.main.transform.SetPositionAndRotation(game.intro.CameraPosition,game.intro.CameraRotation);
            game.intro.Tick(2);var introView=game.intro.CameraPosition;
            Escape();Check(game.MenuActive && Time.timeScale==0,"Escape opens the menu during the introduction and pauses time");
            game.intro.SendMessage("Update");Play();
            Check(game.intro.Phase==IntroPhase.Escort && game.intro.CameraPosition==introView && Time.timeScale==1,"Play resumes the same introduction without resetting its progress");
            game.intro.Tick(7);game.intro.Tick(1.1f);
            Check(game.intro.Phase==IntroPhase.Tutorial && !game.PlayerBlocked,"Tutorial releases player controls");
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(90,-20)});InputSystem.Update();
            var before=Camera.main.transform.rotation;Camera.main.GetComponent<CardboardCamera>().SendMessage("Update");
            Check(Cursor.lockState==CursorLockMode.Locked && Quaternion.Angle(before,Camera.main.transform.rotation)>1,"Mouse turns the tutorial camera without an extra click");
            var target=game.intro.TutorialTarget;
            Check(!game.instruction.Highlighted,"Instruction stays unlit before the tutorial repair");
            Check(game.intro.FloorGear.Take(game) && game.TryInstall(target),"Tutorial gear can be collected and installed");
            Check(game.instruction.Highlighted,"Tutorial repair highlights the floor instruction");
            game.intro.Tick(2.1f);game.intro.Tick(3.3f);
            Check(!game.IntroActive,"Printer cinematic returns control");
            var repair=game.Memorized;game.ReceiveWorkbenchPart(repair.recipe);Check(game.TryInstall(repair),"A normal repair records progress");
            game.TickFaults(8);game.TickShiftClock(13);game.RegisterMistake("Session verification",.5f);
            Check(game.station.Operate(StationPart.Feed),"Feed lever starts before the pause");game.station.Tick(.2f);
            repairs=game.Repairs;order=game.CurrentOrder;clock=game.ShiftRemaining;leverTravel=game.station.LeverTravel;
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,1.62f,200),Quaternion.Euler(12,35,0));
            view=Camera.main.transform.position;rotation=Camera.main.transform.rotation;
            game.instruction.physical.Body.position=view+Camera.main.transform.forward;
            game.instruction.physical.Body.rotation=rotation;
            Physics.SyncTransforms();bodyPosition=game.instruction.physical.Body.position;
            Escape();pausedTime=Time.time;pausedAt=EditorApplication.timeSinceStartup;
            game.station.SendMessage("Update");game.TickShiftClock(8);
            Check(game.station.LeverAnimating && game.station.LeverTravel==leverTravel,"Opening the menu preserves an unfinished lever animation");
            stage=1;
        }
        static void ResumeAndInstruction()
        {
            Check(Time.time==pausedTime && game.ShiftRemaining==clock && game.instruction.physical.Body.position==bodyPosition,"Menu freezes game time, the shift clock and physical props");
            Play();
            Check(game.Repairs==repairs && game.CurrentOrder==order && game.Mistakes==.5f && game.ShiftRemaining==clock,"Play preserves repairs, order, mistakes and shift time");
            Check(Vector3.Distance(Camera.main.transform.position,view)<.001f && Quaternion.Angle(Camera.main.transform.rotation,rotation)<.01f,"Play restores the first-person camera position and direction");
            Check(game.station.LeverAnimating && game.station.LeverTravel==leverTravel,"Play resumes the same in-progress machine operation");
            game.station.Tick(1.1f);Check(!game.station.LeverAnimating && game.station.Stage==StationStage.Conveying,"Paused feed animation completes after resuming");
            stage=2;
        }
        static void Instruction()
        {
            Physics.SyncTransforms();
            Check(game.instruction.Take(),"Highlighted instruction can be picked up");
            Check(game.instruction.HasBeenPickedUp && !game.instruction.Highlighted && Mathf.Approximately(game.objectCarry.HoldDistance,.72f),"First pickup removes the glow and moves the grip to 0.72 metres");
            Physics.simulationMode=SimulationMode.Script;
            for(int i=0;i<150;i++){game.objectCarry.TickGrip();Physics.Simulate(.02f);}
            Check(game.instruction.Held && Vector3.Distance(game.instruction.physical.Body.position,game.objectCarry.TargetPosition)<.08f,"Instruction physically settles at the closer grip");
            var hint=game.instruction.flipHint;var bounds=game.instruction.sheet.GetComponent<MeshFilter>().sharedMesh.bounds;
            Check(hint && Mathf.Approximately(hint.localPosition.x,bounds.max.x-.10f) && Mathf.Approximately(hint.localPosition.y,bounds.min.y+.08f) &&
                Mathf.Approximately(hint.GetComponent<TextMesh>().characterSize,game.instruction.controlHint.GetComponent<TextMesh>().characterSize*2),"E is inset left and up, and enlarged twofold");
            game.instruction.RefreshHints();
            Escape();Play();
            Check(game.instruction.Held && game.objectCarry.Held==game.instruction.physical && !game.instruction.Highlighted,"Escape and Play preserve the held instruction and its cleared glow");
            Check(game.instruction.Flip(),"Instruction still flips at the closer grip");
            game.objectCarry.Release(false);game.instruction.HighlightForTutorial(game.intro.glow);
            Check(!game.instruction.Highlighted,"Putting down the instruction cannot restore the one-time glow");
            Check(game.instruction.Take() && !game.instruction.Highlighted,"Repeated pickup leaves the glow off");
            game.objectCarry.Release(false);game.ResetShift();
            Check(game.Repairs==0 && game.Mistakes==0 && game.ShiftRemaining==240 && !game.instruction.HasBeenPickedUp && !game.instruction.Highlighted,"Fresh shift initialization resets progress and the instruction discovery state");
            game.instruction.HighlightForTutorial(game.intro.glow);
            Check(game.instruction.Highlighted,"A fresh session can highlight the instruction again");
            BathsAndLunch();Finish();
        }
        static void AimAtBath(FactoryStationControl control)
        {
            Physics.SyncTransforms();Vector3 point=control.GetComponent<Collider>().bounds.center;
            Vector3 eye=point+Vector3.up*.5f-control.transform.parent.forward*1.1f;
            Camera.main.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(point-eye));
            game.SendMessage("Update");game.GetComponent<FactoryGearPlacementPreview>().Refresh();
        }
        static void SimulateLunch(int steps,bool left,bool right)
        {
            for(int i=0;i<steps;i++)
            {
                game.lunch.TickBalance(.02f,left,right);game.lunch.physicsTray.TickSupport(.02f);Physics.Simulate(.02f);
            }
        }
        static void ClickBalance(bool left)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)(left?1:2)});InputSystem.Update();
            game.lunch.TickBalance(.02f,mouse.leftButton.isPressed,mouse.rightButton.isPressed,
                mouse.leftButton.wasPressedThisFrame,mouse.rightButton.wasPressedThisFrame);
            // Release before any fixed step: this is the tap the old input path lost.
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
            game.lunch.TickBalance(.02f,false,false);
        }
        static void BathsAndLunch()
        {
            game.ResetShift();var preview=game.GetComponent<FactoryGearPlacementPreview>();
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));FactoryStationControl last=null;
            foreach(var bath in game.station.baths)
            {
                var control=bath.GetComponentsInChildren<FactoryStationControl>().First(c=>c.part==StationPart.Bath);
                AimAtBath(control);last=control;
                Check(preview.BathTarget==control,"Player ray highlights bath "+control.number+" while holding a gear");
                Check(game.station.baths.Count(b=>b.Find("Bath selection outline") && b.Find("Bath selection outline").gameObject.activeSelf)==1,"Only the selected bath is outlined");
                var line=bath.Find("Bath selection outline");
                Check(line && line.GetComponent<LineRenderer>().loop && line.GetComponent<LineRenderer>().positionCount==4 && line.GetComponent<Collider>()==null,"Bath rim outline is closed and cannot intercept interactions");
            }
            game.mainMenu.ReturnToMenu();preview.Refresh();
            Check(!preview.BathTarget,"Bath outline hides in the menu");Play();AimAtBath(last);
            Check(preview.BathTarget==last,"Bath selection returns after resuming");
            Check(game.BeginDip(),"Held gear can start dipping");preview.Refresh();
            Check(!preview.BathTarget,"Bath outline hides while dipping");game.FinishDip(1);game.DropPart();preview.Refresh();
            Check(!preview.BathTarget,"Bath outline hides when the gear leaves the hand");
            game.ResetShift();preview.Refresh();
            var gear=game.Memorized;game.ReceiveWorkbenchPart(gear.recipe);game.TryInstall(gear);
            var lunch=game.lunch;var tray=lunch.physicsTray;
            var player=CardboardRunner.Player;var body=player.GetComponent<CharacterController>();
            bool enabled=body.enabled;body.enabled=false;player.transform.position=new Vector3(CardboardFactoryWorld.WorkshopCenterX,0,0);body.enabled=enabled;
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.identity);
            lunch.TickSchedule(61);var hud=game.GetComponentInChildren<FactoryLunchHUD>(true);hud.SendMessage("LateUpdate");
            Check(hud.title.text=="LUNCH TIME! REACH THE CANTEEN IN 00:45" && !hud.panel.GetComponent<UnityEngine.UI.Image>().enabled,"Lunch bell shows the 45-second arrival countdown without a backdrop");
            lunch.TickSchedule(5);hud.SendMessage("LateUpdate");
            Check(lunch.ArrivalCountdown=="00:40" && hud.title.text.EndsWith("00:40"),"Lunch countdown decreases with the arrival grace period");
            body.enabled=false;player.transform.position=lunch.pickup.position+new Vector3(0,-1,-1);body.enabled=enabled;
            lunch.TickSchedule(0);hud.SendMessage("LateUpdate");
            Check(lunch.Attended && !hud.title.text.Contains("CANTEEN IN"),"Entering the canteen clears the arrival countdown");
            Check(!lunch.counterSign && !lunch.tableSign && !lunch.GetComponentsInChildren<TextMesh>(true).Any(t=>t.gameObject.name=="Lunch sign"),"Floating canteen signs are removed from the scene");
            Check(lunch.State==LunchState.Ready && hud.title.gameObject.activeSelf && hud.title.text=="CARRY THE FOOD TO THE TABLE." && !hud.panel.GetComponent<UnityEngine.UI.Image>().enabled,"Lunch shows a plain English food-carry objective before pickup");
            Check(lunch.TryTake(),"Lunch tray can be collected after the bell");
            Check(!tray.body.isKinematic && tray.body.useGravity && tray.body.constraints==RigidbodyConstraints.None,"Carried tray is a gravity-driven Rigidbody with every rotation and translation axis unlocked");
            lunch.RefreshTrayLight();
            Check(lunch.traySpotlight && lunch.traySpotlight.enabled && lunch.traySpotlight.type==LightType.Spot && lunch.traySpotlight.intensity>=8,"A separate bright spotlight illuminates the lunch tray");
            Check(game.transform.Find("Canteen lunch lighting") && game.transform.Find("Canteen lunch lighting").GetComponentsInChildren<Light>().Count(l=>l.enabled && l.intensity>=2.8f)==4,"Four extra warm lights brighten the canteen");
            Check(Mathf.Abs(Camera.main.transform.position.y-tray.body.position.y-.30f)<.001f,"Tray is held 20 cm higher at 0.30 m below the camera");
            hud.SendMessage("LateUpdate");
            Check(hud.panel.activeSelf && hud.title.gameObject.activeSelf && hud.title.text=="CARRY THE FOOD TO THE TABLE." && !hud.hint.gameObject.activeSelf && !hud.panel.GetComponent<UnityEngine.UI.Image>().enabled,"Carrying shows the short English objective without a black background");
            Check(hud.balance.activeSelf && hud.aim.activeSelf,"Balance indicator and interaction reticle remain available");
            float originalHeight=tray.body.position.y,maxHeight=originalHeight;
            tray.body.AddForce(Vector3.up*1.5f,ForceMode.VelocityChange);tray.body.AddTorque(Vector3.forward*.6f,ForceMode.VelocityChange);
            for(int i=0;i<10;i++){SimulateLunch(1,false,false);maxHeight=Mathf.Max(maxHeight,tray.body.position.y);}
            Check(maxHeight>originalHeight+.015f,"An upward impulse moves the carried tray out of its holding plane");
            Check(Mathf.Abs(tray.RollDegrees)>2 && Mathf.Abs(tray.RollSpeed)>5,"The tray keeps physical roll and angular momentum without a scripted roll pose");
            lunch.ResetLunch();lunch.TickSchedule(61);Check(lunch.TryTake(),"Physical tray can be restored for steering checks");
            SimulateLunch(40,true,false);float leftTilt=lunch.Tilt;
            report.Add("DETAIL left state="+lunch.State+" tilt="+lunch.Tilt+" roll="+tray.RollDegrees+" pitch="+tray.PitchDegrees+" velocity="+tray.body.angularVelocity);
            Check(lunch.Carrying && lunch.Tilt<-.14f && tray.RollDegrees>6 && tray.RollDegrees<30,"Holding LMB visibly tilts the physical tray to the left");
            SimulateLunch(30,false,true);
            report.Add("DETAIL counter state="+lunch.State+" tilt="+lunch.Tilt+" speed="+lunch.AngularSpeed);
            Check(lunch.Carrying && Mathf.Abs(tray.RollDegrees)<6 && Mathf.Abs(lunch.Tilt)<Mathf.Abs(leftTilt),"Opposite button returns the physical tray near level and brakes its roll");
            lunch.ResetLunch();lunch.TickSchedule(61);Check(lunch.TryTake(),"Tray can be restored for rightward balance check");
            SimulateLunch(40,false,true);
            report.Add("DETAIL right state="+lunch.State+" tilt="+lunch.Tilt+" roll="+tray.RollDegrees+" pitch="+tray.PitchDegrees);
            Check(lunch.Carrying && lunch.Tilt>.14f && tray.RollDegrees<-6 && tray.RollDegrees>-30,"Holding RMB visibly tilts the physical tray to the right");
            SimulateLunch(160,false,true);
            Check(lunch.State==LunchState.Spilled && Mathf.Approximately(game.Mistakes,1),"Excessive sustained tilt physically spills the food and awards one penalty");
            hud.SendMessage("LateUpdate");
            Check(hud.title.gameObject.activeSelf && !hud.panel.GetComponent<UnityEngine.UI.Image>().enabled,"Lunch result remains plain text without a background");
            foreach(float roll in new[]{14f,-14f})
            {
                lunch.ResetLunch();lunch.TickSchedule(61);Check(lunch.TryTake(),"Fresh tray can be picked up for a short correction click");
                Vector3 position=tray.body.position;tray.ResetContents();tray.Begin(position,Quaternion.Euler(2,0,roll));
                tray.body.angularVelocity=Vector3.forward*Mathf.Sign(roll)*.35f;
                ClickBalance(roll<0);SimulateLunch(35,false,false);
                report.Add("DETAIL tap roll="+roll+" final="+tray.RollDegrees+" speed="+tray.RollSpeed+" state="+lunch.State);
                Check(lunch.Carrying && Mathf.Abs(tray.RollDegrees)<5 && Mathf.Abs(tray.RollSpeed)<20,
                    "One short "+(roll<0?"left":"right")+" click levels the tilted tray and brakes momentum even when released before physics");
                Check(!tray.body.isKinematic && tray.body.useGravity && tray.body.constraints==RigidbodyConstraints.None,"Tap correction preserves free dynamic physics");
            }
            lunch.ResetLunch();lunch.TickSchedule(61);Check(lunch.TryTake(),"Fresh tray can be picked up for an unattended balance check");
            SimulateLunch(240,false,false);
            Check(lunch.State==LunchState.Spilled || Mathf.Abs(tray.RollDegrees)>6,"An unattended tray develops free roll instead of remaining locked level");
            lunch.ResetLunch();lunch.TickSchedule(61);Check(lunch.TryTake(),"Fresh tray can be picked up for delivery");
            SimulateLunch(10,false,false);body.enabled=false;player.transform.position=lunch.destination.position+new Vector3(0,-.8f,-1);body.enabled=enabled;
            report.Add("DETAIL serve state="+lunch.State+" tilt="+lunch.Tilt+" pitch="+lunch.PitchTilt+" canServe="+lunch.CanServe);
            Check(lunch.TryServe() && lunch.State==LunchState.Served && !game.HandsBusy,"A balanced physical tray can be delivered");
            hud.SendMessage("LateUpdate");
            Check(hud.title.text=="ENJOY YOUR MEAL! GET BACK TO WORK." && !hud.panel.GetComponent<UnityEngine.UI.Image>().enabled,"Delivery displays the English meal-and-work message without a black background");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>60){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0)Setup();
                else if(stage==1 && EditorApplication.timeSinceStartup-pausedAt>.35)ResumeAndInstruction();
                else if(stage==2){stage=3;Instruction();}
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
