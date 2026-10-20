using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryOpeningVerification
    {
        const string Active="Cardboard.OpeningVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;static FactoryIntro intro;static int stage;static float at;static double started;static bool background;static SimulationMode simulation;
        static Vector3 playerHome,cameraHome;static Quaternion playerRotation,cameraRotation;
        static FactoryOpeningVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactoryOpeningVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=false;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Place(Vector3 position)
        {var controller=CardboardRunner.Player.GetComponent<CharacterController>();controller.enabled=false;CardboardRunner.Player.transform.position=position;controller.enabled=true;}
        static void Finish()
        {
            Physics.simulationMode=simulation;
            if(game)
            {
                CardboardMarker.Instance.SetDrawingMode(false,false);game.ResetShift();intro.enabled=true;game.enabled=true;game.mainMenu.enabled=true;game.station.enabled=true;
                foreach(var worker in game.crowd.runners)worker.enabled=true;
                Place(playerHome);CardboardRunner.Player.transform.rotation=playerRotation;Camera.main.transform.SetPositionAndRotation(cameraHome,cameraRotation);Camera.main.GetComponent<CardboardCamera>().AdoptCurrentView();Camera.main.GetComponent<CardboardCamera>().enabled=true;
            }
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void ViewIntro(){Camera.main.transform.SetPositionAndRotation(intro.CameraPosition,intro.CameraRotation);game.expressions.Refresh();at=Time.time;}
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;intro=game.intro;simulation=Physics.simulationMode;
            playerHome=CardboardRunner.Player.transform.position;playerRotation=CardboardRunner.Player.transform.rotation;cameraHome=Camera.main.transform.position;cameraRotation=Camera.main.transform.rotation;
            game.enabled=false;intro.enabled=false;game.mainMenu.enabled=false;game.station.enabled=false;Camera.main.GetComponent<CardboardCamera>().enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            var people=game.crowd.runners.Where(w=>!w.isPlayer).Select(w=>w.puppet.GetComponentInChildren<FactoryWorkerAppearance>()).ToArray();
            Check(people.Length==12 && people.GroupBy(p=>p.name).Count()==4 && people.GroupBy(p=>p.name).All(g=>g.Count()==3),"Exactly twelve workers: three each of Lera, Kolya, Nastya and Masha");
            Check(people.All(p=>new[]{p.normal,p.suspicious,p.angry}.All(m=>m.GetFloat("_Cull")==0 && m.doubleSidedGI)),"All twelve character expression materials are double sided");
            ImportedCharacterInstaller.VerifyRuntime(Check);
            Check(game.lunch.diners.workers.Length==12 && game.lunch.diners.meals.Length==12 && game.lunch.diners.places.Length==12,"All twelve workers have dining places and meals");
            var untranslated=game.GetComponentsInChildren<TextMesh>(true).Where(t=>Regex.IsMatch(t.text,"[А-Яа-яЁё]")).Select(t=>t.text).ToArray();
            Check(untranslated.Length==0,"All saved world signs and menu text are English"+string.Join(" | ",untranslated));
            Check(game.transform.Find("Factory doorways/Canteen door").Cast<Transform>().Count(t=>t.name=="Doorway infill" && t.localScale.x<.5f)==0,"Duplicate canteen passage side surfaces are removed");
            var apple=game.lunch.tray.Find("Lunch apple").GetComponentInChildren<MeshRenderer>();Check(apple.sharedMaterial.mainTexture.name=="Apple" && apple.sharedMaterial.GetFloat("_Cull")==0,"The newly supplied Apple model and double-sided texture are on the tray");
            game.mainMenu.Show();game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
            Check(intro.Phase==IntroPhase.Escort && game.PlayerBlocked && game.CameraOverride,"Play starts the escorted introduction and takes camera/input control");
            Check(CardboardRunner.Player.transform.position.x>CardboardFactoryWorld.WorkshopEast+10,"Introduction starts inside the new escape corridor");
            Check(game.expressions.people.All(p=>{p.SetState(FactoryFaceState.Normal);return true;}),"Expression state prepared for escort check");game.expressions.Refresh();
            Check(game.expressions.people.All(p=>p.State==FactoryFaceState.Angry),"Every worker is angry during the escort");intro.Tick(3);ViewIntro();
        }
        static void Machine()
        {
            game.ResetShift();var station=game.station;
            Check(!station.Operate(StationPart.Press) && !station.Operate(StationPart.Guard) && station.BeginWheel(Vector2.zero),"Press and guard require a sheet, while the physical wheel can be held before setup");station.EndGrip(false);
            int needed=game.OrderedType;while(station.SelectedType!=needed)station.Operate(StationPart.Selector);
            Check(station.Operate(StationPart.Feed),"Feed lever starts the required machine sequence");for(int i=0;i<200;i++)station.Tick(.02f);
            Check(station.Stage==StationStage.Loaded && station.Operate(StationPart.Selector),"Selector works after feeding while the press is open");while(station.SelectedType!=needed)station.Operate(StationPart.Selector);
            Check(station.Operate(StationPart.Press),"Red lever closes the press after feed");for(int i=0;i<60;i++)station.Tick(.02f);
            Check(!station.Operate(StationPart.Selector),"Closed press locks the selector");
            var guard=station.GetComponentsInChildren<FactoryStationControl>().First(c=>c.part==StationPart.Guard);
            var point=guard.transform.position;var eye=point-station.transform.forward*1.4f+Vector3.up*.3f;Physics.SyncTransforms();var target=game.TargetForRay(new Ray(eye,point-eye));
            Check(target && target.GetComponent<FactoryStationControl>()==guard,"Raised press geometry does not block the guard lift button");
            Check(station.Operate(StationPart.Guard),"Guard lift button works after the red lever");for(int i=0;i<40;i++)station.Tick(.02f);
            for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);
            Check(station.CanCut && station.TurnWheel(1080) && station.Stage==StationStage.Cut,"Full pressure permits cutting and creates the selected gear");
            Check(station.Operate(StationPart.Guard),"Completed cut can lower the guard");for(int i=0;i<40;i++)station.Tick(.02f);
            Check(station.Operate(StationPart.Press),"Press can reopen after the guard is lowered");for(int i=0;i<60;i++)station.Tick(.02f);
            Check(station.Operate(StationPart.Output) && game.HasPart,"Finished cut can be collected after the complete safe sequence");
            for(int b=0;b<3;b++)
            {
                var bath=station.baths[b];var control=bath.GetComponentsInChildren<FactoryStationControl>().First(c=>c.part==StationPart.Bath);var shape=control.GetComponent<BoxCollider>();int hits=0;
                foreach(float x in new[]{-.25f,0,.25f})foreach(float z in new[]{-.25f,0,.25f})
                {point=shape.transform.TransformPoint(shape.center+new Vector3(shape.size.x*x,0,shape.size.z*z));eye=point+Vector3.up*.7f-station.transform.forward*1.1f;target=game.TargetForRay(new Ray(eye,point-eye));if(target && target.GetComponent<FactoryStationControl>()==control)hits++;}
                Check(hits>=7,"Bath "+(b+1)+" is selectable across a broad 3x3 area, hits="+hits);
            }
            Check(station.Operate(StationPart.Bath,needed),"A collected gear enters its finishing bath");for(int i=0;i<60;i++)station.Tick(.02f);
            Check(game.HeldRecipe.finish==needed && !game.DippingPart,"Dipping completes and returns the processed gear");game.ResetShift();
        }
        static void TrayAndHand()
        {
            game.ResetShift();game.lunch.TickSchedule(61);
            Check(game.lunch.State==LunchState.Waiting,"Lunch waits for the first manufactured gear repair even after its timer expires");
            game.ReceiveWorkbenchPart(game.Memorized.recipe);Check(game.TryInstall(game.Memorized),"First normal repair unlocks the lunch schedule");game.lunch.TickSchedule(0);
            Place(game.lunch.pickup.position+new Vector3(0,-1,-1));
            Camera.main.transform.SetPositionAndRotation(CardboardRunner.Player.transform.position+Vector3.up*1.62f,Quaternion.identity);
            Check(game.lunch.TryTake(),"Player can collect the physical tray");var tray=game.lunch.physicsTray;Physics.simulationMode=SimulationMode.Script;
            for(int i=0;i<180;i++){Camera.main.transform.rotation=Quaternion.Euler(Mathf.Lerp(0,40,i/179f),Mathf.Sin(i*.02f)*8,0);game.lunch.TickBalance(.02f,false,false);tray.TickSupport(.02f);Physics.Simulate(.02f);}
            Check(game.lunch.Carrying && tray.contents.All(item=>tray.transform.InverseTransformPoint(item.worldCenterOfMass).y>-.08f),"Three dishes remain supported through gentle yaw and a 40-degree downward look");
            Check(game.lunch.PitchTilt<.22f,"Forward/back tray angle is reduced while lateral tilt remains available");
            game.ResetShift();Physics.simulationMode=simulation;
            var worker=game.crowd.runners.First(w=>!w.isPlayer);var position=worker.transform.position;Camera.main.transform.SetPositionAndRotation(position+new Vector3(0,1.4f,2),Quaternion.LookRotation(Vector3.back));
            var marker=CardboardMarker.Instance;marker.SetDrawingMode(true);marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),false);var hand=Camera.main.GetComponent<CardboardMarkerHand>();hand.RefreshPose(.02f);
            
            Check(hand.Visible && hand.Palm.gameObject.activeInHierarchy && (Camera.main.cullingMask & (1<<CardboardMarkerHand.HandLayer))!=0 && hand.paperTemplate.shader.name==CardboardMarkerHand.ShaderName,"Marker hand and sleeve use the dedicated lit first-person pass in the base camera");
            report.Add("DETAIL hand palm="+Camera.main.WorldToScreenPoint(hand.Palm.position)+" pointer="+marker.Pointer);at=Time.time;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>65){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;Save();return;}
                if(Time.time-at<.25f)return;
                if(stage==1){CardboardFactorySceneBuilder.Capture("OpeningAngryEscort");intro.Tick(7.05f);intro.Tick(1.1f);intro.Tick(.01f);Check(intro.Phase==IntroPhase.Tutorial && !game.PlayerBlocked && intro.FloorGear,"Escort pushes the player into the workshop and releases control beside a highlighted gear");at=Time.time;stage=2;return;}
                if(stage==2)
                {
                    report.Add("DETAIL tutorial camera="+Camera.main.transform.position+" player="+CardboardRunner.Player.transform.position+" overlaps="+string.Join(",",Physics.OverlapSphere(Camera.main.transform.position,.4f).Select(c=>c.name)));CardboardFactorySceneBuilder.Capture("OpeningTutorialGear");var gear=intro.TutorialTarget;Check(intro.FloorGear.recipe.Equals(gear.recipe) && gear.broken && game.CurrentOrder==gear.index+1,"Floor gear exactly matches the one damaged tutorial socket");
                    Check(intro.FloorGear.Take(game) && game.TryInstall(gear),"Tutorial gear can be picked up and installed");Check(intro.Phase==IntroPhase.PrinterDelay && game.Repairs==0,"Tutorial repair does not consume one of the six normal orders");intro.Tick(1.9f);Check(intro.Phase==IntroPhase.PrinterDelay,"Printer reveal waits two seconds after repair");intro.Tick(.2f);game.station.Tick(.2f);
                    Check(game.IntroBusy && game.station.IsPrinting && game.station.printerSignal.speaker.isPlaying,"Next fault rings the printer while camera control is taken");intro.Tick(1.5f);ViewIntro();Check(Camera.main.fieldOfView<45,"Printer reveal zooms in on the workstation");stage=3;return;
                }
                if(stage==3){CardboardFactorySceneBuilder.Capture("OpeningPrinterFocus");intro.Tick(2);Check(!intro.Active && !game.PlayerBlocked && Camera.main.fieldOfView==CardboardCamera.GameplayFov,"Cinematic ends with normal camera/input and a full-length shift");Machine();
                    game.ResetShift();game.ending.enabled=false;
                    for(int i=0;i<game.RepairGoal;i++){var broken=game.Memorized;game.ReceiveWorkbenchPart(broken.recipe);Check(game.TryInstall(broken),"Normal repair "+(i+1)+" succeeds after the introduction");if(i<game.RepairGoal-1)game.TickFaults(8);}
                    Check(game.ReadyToLeave && game.ending.Phase==FactoryEndingPhase.BellDelay && !game.Ended,"Six normal repairs queue the final lunch and escape cinematic");
                    for(int i=0;i<2500 && !game.MenuActive;i++)
                    {
                        game.ending.Tick(.02f);game.exitDoor.Tick(.02f);
                        if(game.ending.Phase==FactoryEndingPhase.WaitingForLunch)
                            for(int w=0;w<game.lunch.diners.workers.Length;w++)
                            {var actor=game.lunch.diners.workers[w];var body=actor.GetComponent<CharacterController>();body.enabled=false;actor.transform.position=game.lunch.diners.places[w].position;body.enabled=true;}
                    }
                    Check(game.MenuActive && CardboardRunner.Player.transform.position.x>CardboardFactoryWorld.WorkshopEast+14,"Automatic corridor escape returns to the main menu");
                    for(int i=0;i<50;i++)game.ending.Tick(.02f);game.ending.enabled=true;
                    game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
                    stage=4;at=Time.time;Save();return;}
                if(stage==4){TrayAndHand();stage=5;Save();return;}
                if(stage==5){CardboardMarker.Instance.SetDrawingMode(true);CardboardMarker.Instance.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),false);Camera.main.GetComponent<CardboardMarkerHand>().RefreshPose(1);report.Add("DETAIL hand layers="+string.Join(" | ",Camera.main.GetComponent<CardboardMarkerHand>().Tip.GetComponentsInChildren<MeshRenderer>().Select(r=>r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" screen="+Camera.main.WorldToScreenPoint(r.bounds.center)+" bounds="+r.bounds.size)));CardboardFactorySceneBuilder.Capture("RestoredMarkerHand");Finish();}
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}