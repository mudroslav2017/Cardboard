using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryFinaleVerification
    {
        const string Active="Cardboard.FinaleVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;
        static CardboardRunner player;
        static FactoryEnding ending;
        static FactoryRepairCounter counter;
        static double started;
        static bool background;
        static int stage;
        static float at,nextProgress;
        static Vector3[] workerHomes;
        static bool sawDeparture,sawRun,sawWait,ranEarly;
        static int stepCount;
        static FactoryFinaleVerification(){EditorApplication.update+=Tick;}
        static void Check(bool condition,string text){report.Add((condition?"PASS ":"FAIL ")+text);}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Place(Vector3 position)
        {
            var body=player.GetComponent<CharacterController>();bool was=body.enabled;body.enabled=false;player.transform.position=position;body.enabled=was;
            Camera.main.transform.position=position+Vector3.up*1.62f;
            Camera.main.GetComponent<CardboardCamera>().AdoptCurrentView();Physics.SyncTransforms();
        }
        static void EndingTime(float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)ending.Tick(.02f);}
        static void Repair()
        {
            if(game.CurrentOrder==0)game.BreakRandomGear();
            var gear=game.Memorized;game.ReceiveWorkbenchPart(gear.recipe);Check(game.TryInstall(gear),"Valid normal repair "+(game.Repairs)+" installs");
            Check(game.Repairs>=game.RepairGoal?game.CurrentOrder==0:game.CurrentOrder>0,"Next order is queued until the sixth repair: "+game.Repairs);
            for(int i=0;i<250;i++)game.station.Tick(.02f);
            Check(game.Repairs>=game.RepairGoal?game.station.QueuedOrder==0:game.station.OrderNumber==game.CurrentOrder && game.station.OrderTravel>=.999f,"Printer produces the next card after repair "+game.Repairs);
            counter.Refresh();
        }
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;player=CardboardRunner.Player;ending=game.ending;counter=game.GetComponentInChildren<FactoryRepairCounter>(true);
            game.enabled=false;game.station.enabled=false;ending.enabled=false;game.mainMenu.enabled=false;game.lunch.enabled=false;game.lunch.diners.enabled=false;
            game.intro.enabled=false;Camera.main.GetComponent<CardboardCamera>().enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            game.ResetShift();
            Check(CardboardFactoryGame.RequiredRepairs==6 && game.RepairGoal==6,"Six successful normal installations are required");
            counter.Refresh();Check(counter.label.text=="0 / 6" && counter.Visible,"Top-left repair counter starts at 0 / 6");
            Check(counter.icon.GetComponentsInChildren<MeshRenderer>().Length>0 && counter.label.font==CardboardTypography.DefaultFont,"Counter uses an actual 3D gear and the existing UI font");
            Check(!game.GetComponentsInChildren<AudioSource>(true).Any(s=>s.clip && s.clip.name.Replace(" ","")=="большоймеханизм2"),"Removed mechanism 2 clip has no gameplay audio source");
            Check(game.GetComponentInChildren<FactoryMechanismAudio>().speakers.Length==4,"Four other spatial mechanism emitters remain");
            var furniture=game.transform.Find("Canteen furniture");var chairs=furniture.Find("Dining chairs");
            Check(chairs.childCount==13 && chairs.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial.mainTexture && r.sharedMaterial.GetFloat("_Cull")==0 && r.sharedMaterial.doubleSidedGI),"Thirteen supplied chairs use the texture and double-sided material");
            Check(chairs.GetComponentsInChildren<MeshCollider>().Length==13,"All dining chairs have model-shaped collisions");
            Check(furniture.Cast<Transform>().Count(t=>t.name.Contains("workstation model"))==3,"Three dining tables use the workstation table model");
            var roof=furniture.Find("Canteen ceiling");Check(roof && roof.GetComponent<Collider>() && roof.GetComponent<Renderer>().bounds.size.x>=26 && roof.GetComponent<Renderer>().bounds.size.z>=18,"Textured ceiling covers the complete canteen");
            var nav=new FactoryCaptureNavigation(.38f);int accessible=0;
            for(int i=0;i<game.lunch.diners.workers.Length;i++)
            {
                var path=nav.Path(game.lunch.diners.workers[i].transform.position,game.lunch.diners.places[i].position);
                if(path.Count>0 && Vector3.Distance(path[path.Count-1],game.lunch.diners.places[i].position)<.5f)accessible++;
            }
            Check(accessible==12,"New furniture leaves routes to all twelve dining places accessible: "+accessible+" / 12");
        }
        static void Lunch()
        {
            var lunch=game.lunch;
            game.ResetShift();Place(new Vector3(1,0,-1));lunch.TickSchedule(lunch.lunchAfter+1);
            Check(lunch.State==LunchState.Waiting,"Ordinary lunch still waits for a first manufactured repair");
            Repair();lunch.TickSchedule(0);
            Check(lunch.State==LunchState.Ready && !lunch.Missed,"Ordinary lunch starts after first normal repair and an expired lunch timer");
            lunch.TickSchedule(lunch.attendanceGrace-.1f);Check(game.Mistakes==0,"Attendance grace allows time to walk to the canteen");
            lunch.TickSchedule(.2f);Check(lunch.Missed && Mathf.Approximately(game.Mistakes,1.5f),"Missing ordinary lunch adds exactly 1.5 suspicion marks");
            lunch.TickSchedule(100);Check(Mathf.Approximately(game.Mistakes,1.5f),"The missed-lunch penalty is applied only once");
            game.ResetShift();Place(new Vector3(1,0,-1));Repair();lunch.TickSchedule(lunch.lunchAfter+1);
            Place(new Vector3(-13,0,-18));lunch.TickSchedule(2);
            Place(new Vector3(1,0,-1));lunch.TickSchedule(100);
            Check(lunch.Attended && !lunch.Missed && game.Mistakes==0,"Reaching the canteen counts as attendance and prevents the missed-lunch penalty");
            game.ResetShift();Place(new Vector3(1,0,-1));Repair();lunch.TickSchedule(lunch.lunchAfter+1);
            game.mainMenu.Show();lunch.TickSchedule(100);Check(!lunch.Missed && game.Mistakes==0,"Menu time does not consume lunch attendance grace");
            game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
            game.ResetShift();
        }
        static void Shortcut()
        {
            game.ResetShift();Place(new Vector3(1,0,-1));
            var keyboard=InputSystem.AddDevice<Keyboard>();
            void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();game.SendMessage("Update");counter.Refresh();}
            try
            {
                Keys(Key.T,Key.U);Check(game.Repairs==0 && !ending.Active,"T + U alone cannot complete the repair counter");
                Keys(Key.T,Key.U,Key.O);
                Check(game.Repairs==6 && counter.label.text=="6 / 6","Pressing the third TUO key sets the counter to 6 / 6 through actual keyboard input");
                Check(game.WorkingGears==game.gears.Length && game.CurrentOrder==0 && ending.Phase==FactoryEndingPhase.BellDelay,"TUO clears outstanding faults and starts the normal final-lunch delay");
                game.ResetShift();Keys(Key.T,Key.U,Key.O);
                Check(game.Repairs==0 && !ending.Active,"Holding TUO does not repeatedly retrigger the shortcut after a reset");
                Keys(Key.T,Key.U);Keys(Key.T,Key.U,Key.O);
                Check(game.Repairs==6 && ending.Active,"Releasing and pressing the third key triggers TUO again");
                game.ResetShift();game.mainMenu.Show();Keys();Keys(Key.T,Key.U,Key.O);
                Check(game.Repairs==0 && !ending.Active,"TUO is ignored in the main menu");
                game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
            }
            finally{InputSystem.RemoveDevice(keyboard);game.ResetShift();}
        }
        static void StartFinale()
        {
            Check(!game.MenuActive,"Repairs and cinematic are tested during active gameplay, with the menu closed");
            Place(new Vector3(CardboardFactoryWorld.WorkshopEast-4,0,CardboardFactoryWorld.WorkshopSouth+2.7f));
            Camera.main.transform.rotation=Quaternion.Euler(0,180,0);
            for(int i=0;i<5;i++){Repair();Check(!ending.Active && !game.ReadyToLeave,"No early ending after "+game.Repairs+" repairs");}
            Repair();Check(counter.label.text=="6 / 6" && ending.Phase==FactoryEndingPhase.BellDelay && game.ReadyToLeave,"Sixth installation completes counter and queues the finale");
            Check(!ending.Begin(),"Duplicate ending requests are ignored");EndingTime(1.8f);
            Check(ending.Phase==FactoryEndingPhase.BellDelay && !game.lunch.FinalLunch,"Final lunch waits two seconds after the sixth installation");
            EndingTime(.3f);Check(ending.Phase==FactoryEndingPhase.WaitingForLunch && game.lunch.FinalLunch && game.lunch.diners.Active,"Final bell starts the crowd departure and waits before the escape");
            Check(game.PlayerBlocked && game.CameraOverride && !ending.glow.gameObject.activeSelf && !game.exitDoor.IsOpen,"Player waits under camera control while the escape door and light remain closed");
            float marks=game.Mistakes;game.lunch.TickSchedule(100);game.ObserveConduct(true,6,.6f,false,2);
            Check(game.Mistakes==marks && !game.lunch.Missed,"Final lunch and automatic running/jumping never add a missed-lunch or conduct penalty");
            var crowd=game.lunch.diners;var waitingPosition=player.transform.position;
            void MoveWorker(int index,Vector3 position)
            {var w=crowd.workers[index];var cc=w.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;w.transform.position=position;cc.enabled=enabled;}
            for(int i=0;i<crowd.workers.Length-1;i++)MoveWorker(i,crowd.places[i].position);
            EndingTime(10);
            Check(ending.Phase==FactoryEndingPhase.WaitingForLunch && !crowd.AllInCanteen && Vector3.Distance(player.transform.position,waitingPosition)<.001f,"Eleven departed workers are insufficient: the player waits for the last worker without a fixed timer");
            int last=crowd.workers.Length-1;MoveWorker(last,new Vector3(-13,0,CardboardFactoryWorld.CanteenNorth+.2f));EndingTime(1);
            Check(ending.Phase==FactoryEndingPhase.WaitingForLunch,"A worker still in the passage prevents the escape");
            MoveWorker(last,crowd.places[last].position);ending.Tick(.02f);
            Check(crowd.AllInCanteen && ending.Phase==FactoryEndingPhase.LookAround && ending.glow.gameObject.activeSelf && game.exitDoor.IsOpen,"Only after all twelve workers reach the canteen does the escape door open and the look-around animation start");
            EndingTime(.7f);var left=ending.CameraRotation;EndingTime(1.15f);
            Check(Quaternion.Angle(left,ending.CameraRotation)>55,"Player visibly looks from one side to the other before running");
            EndingTime(1.2f);Check(ending.Running,"Look-around animation transitions into automatic running");
            Check(ending.Route.Count>3 && ending.Route.Last().x>CardboardFactoryWorld.WorkshopEast+14,"Escape route reaches the distant end of the existing corridor");
            game.exitDoor.SetOpen(true,true);Physics.SyncTransforms();
            EndingTime(1);Camera.main.transform.SetPositionAndRotation(ending.CameraPosition,ending.CameraRotation);
            counter.inventory.Pose(0,true,false);counter.Refresh();CardboardFactorySceneBuilder.Capture("FinaleCounter");at=Time.time;
        }
        static void CompleteFinale()
        {
            bool captured=false;bool shake=false;var collisions=new HashSet<string>();int frames=0;
            while(ending.Running && frames++<2500)
            {
                ending.Tick(.02f);Vector3 p=player.transform.position;
                if(!captured && p.x>CardboardFactoryWorld.WorkshopEast+6)
                {captured=true;Camera.main.transform.SetPositionAndRotation(ending.CameraPosition,ending.CameraRotation);CardboardFactorySceneBuilder.Capture("FinaleCorridor");}
                shake|=Mathf.Abs(ending.CameraPosition.y-p.y-1.62f)>.015f;
                foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.55f,p+Vector3.up*1.45f,.22f,(1<<8)|1,QueryTriggerInteraction.Ignore))
                    if(!c.GetComponentInParent<CardboardRunner>() && !FactoryDoor.IsLeaf(c))collisions.Add(c.name);
            }
            Check(shake,"Automatic running includes visible camera bob/shake");
            Check(collisions.Count==0,"Escape route clears static scenery: "+string.Join(", ",collisions));
            Check(ending.Phase==FactoryEndingPhase.WhiteOut && player.transform.position.x>CardboardFactoryWorld.WorkshopEast+14,"Player physically reaches the white light at the corridor end");
            ending.Tick(.02f);Check(ending.white.color.a>=.65f,"White exit fade remains continuous on entering the glow");
            EndingTime(.8f);Check(game.MenuActive && ending.Phase==FactoryEndingPhase.MenuReveal,"White exit returns to the main menu");
            EndingTime(.9f);Check(ending.white.color.a==0 && !ending.Active && player.GetComponent<CharacterController>().enabled,"White screen clears and player controller is restored");
            counter.Refresh();Check(!counter.Visible,"Counter hides in the main menu");
            game.mainMenu.Tick(2);Camera.main.transform.SetPositionAndRotation(game.mainMenu.CameraPosition,game.mainMenu.CameraRotation);
            CardboardFactorySceneBuilder.Capture("FinaleMenu");
            game.mainMenu.Tick(1);game.mainMenu.Choose(FactoryMenuAction.Play);game.mainMenu.Tick(.3f);game.mainMenu.Tick(1.2f);game.mainMenu.Tick(.3f);game.mainMenu.Tick(.6f);
            Check(game.Repairs==0 && !game.lunch.FinalLunch && ending.Phase==FactoryEndingPhase.None && game.intro.Active,"Starting again resets progress, final lunch and cinematic, then begins the introduction");
            game.intro.Cancel();game.ResetShift();Place(new Vector3(-13,0,-18));
            Camera.main.transform.SetPositionAndRotation(new Vector3(-13,2.4f,-18),Quaternion.Euler(7,180,0));
            foreach(var meal in game.lunch.diners.meals)meal.gameObject.SetActive(true);
            counter.inventory.Pose(0,true,false);counter.Refresh();CardboardFactorySceneBuilder.Capture("CanteenFurniture");
        }
        static void StartRuntime()
        {
            game.ResetShift();Place(new Vector3(CardboardFactoryWorld.WorkshopEast-4,0,CardboardFactoryWorld.WorkshopSouth+2.7f));
            for(int i=0;i<6;i++)Repair();
            workerHomes=game.lunch.diners.workers.Select(w=>w.transform.position).ToArray();sawDeparture=sawRun=sawWait=ranEarly=false;
            stepCount=player.GetComponent<FactoryPlayerFootsteps>().StepsPlayed;
            ending.enabled=game.enabled=game.station.enabled=game.mainMenu.enabled=game.lunch.enabled=game.lunch.diners.enabled=true;
            Camera.main.GetComponent<CardboardCamera>().enabled=true;
            foreach(var worker in game.crowd.runners)worker.enabled=true;
            at=Time.time;stage=3;
        }
        static void Runtime()
        {
            for(int i=0;i<workerHomes.Length;i++)sawDeparture|=(game.lunch.diners.workers[i].transform.position-workerHomes[i]).sqrMagnitude>.25f;
            sawWait|=ending.Phase==FactoryEndingPhase.WaitingForLunch;
            if(ending.Running){sawRun|=player.speed>4;ranEarly|=!game.lunch.diners.AllInCanteen;}
            if(ending.Phase==FactoryEndingPhase.WaitingForLunch && Time.time>=nextProgress)
            {
                nextProgress=Time.time+3;
                File.WriteAllText("Library/FactoryFinaleProgress.txt","Waiting "+(Time.time-at).ToString("F1")+"s\n"+string.Join("\n",game.lunch.diners.workers.Select(w=>w.name+" "+w.transform.position+" "+game.lunch.diners.Describe(w))));
            }
            if(Time.time-at<3 || !game.MenuActive || ending.Phase!=FactoryEndingPhase.None)return;
            Check(sawDeparture,"In real Play Mode frames the workers leave for final lunch during the cinematic");
            Check(sawWait && !ranEarly && game.lunch.diners.AllInCanteen,"Real frame-driven escape waits until every worker is inside the canteen");
            Check(sawRun && player.GetComponent<FactoryPlayerFootsteps>().StepsPlayed>stepCount,"Real frame-driven escape runs automatically with footstep audio");
            Check(game.Mistakes==0 && !game.lunch.Missed,"Real cinematic adds no ordinary-lunch or automatic-run penalty");
            Check(player.transform.position.x>CardboardFactoryWorld.WorkshopEast+14 && game.mainMenu.BlocksGameplay,"Real frame-driven escape reaches the glow and returns to an interactive main menu");
            Finish();
        }
        static void Finish()
        {
            SessionState.SetBool(Active,false);Application.runInBackground=background;
            File.WriteAllLines("Library/FactoryFinaleVerification.txt",new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>180){report.Add("FAIL timeout");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();Lunch();Shortcut();stage=1;at=Time.time;return;}
                if(Time.time-at<.5f)return;
                if(stage==1){StartFinale();stage=2;return;}
                if(stage==2){CompleteFinale();StartRuntime();return;}
                if(stage==3)Runtime();
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
