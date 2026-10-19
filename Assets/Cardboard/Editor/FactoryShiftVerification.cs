using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryShiftVerification
    {
        const string Active="Cardboard.ShiftVerification";
        static readonly List<string> report=new List<string>();
        static int stage;
        static double started;
        static float reactionAt;
        static CardboardFactoryGame game;
        static CardboardRunner observed;
        static FactoryWorkerImpact reaction;
        static Quaternion[] gearRotations;
        static SimulationMode oldSimulation;
        static bool background;
        static GameObject floor;
        static FactoryShiftVerification(){EditorApplication.update+=Tick;}
        static void Check(bool pass,string text){report.Add((pass?"PASS ":"FAIL ")+text);}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;oldSimulation=Physics.simulationMode;
            background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);EditorApplication.isPaused=false;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void Save(){File.WriteAllLines("Library/FactoryShiftVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        static void Finish()
        {
            if(game){game.ResetShift();game.enabled=true;if(game.station)game.station.enabled=true;if(game.mainMenu)game.mainMenu.enabled=true;foreach(var worker in game.crowd.runners)worker.enabled=true;var view=Camera.main?Camera.main.GetComponent<CardboardCamera>():null;if(view)view.enabled=true;}
            if(floor)Object.DestroyImmediate(floor);
            Physics.simulationMode=oldSimulation;Application.runInBackground=background;Save();SessionState.SetBool(Active,false);EditorApplication.isPlaying=false;
        }
        static void Place(CardboardRunner worker,Vector3 point)
        {
            var body=worker.GetComponent<CharacterController>();bool enabled=body && body.enabled;
            if(enabled)body.enabled=false;worker.transform.SetPositionAndRotation(point,Quaternion.identity);if(enabled)body.enabled=true;
        }
        static void Simulate(FactoryTrayPhysics tray,int count)
        {
            Physics.SyncTransforms();
            for(int i=0;i<count;i++){tray.TickSupport(.02f);Physics.Simulate(.02f);}
        }
        static void Configuration()
        {
            Check(game.gears.Length==17,"17 sockets match the reference mechanism");
            Physics.SyncTransforms();
            bool reachable=true;
            foreach(var gear in game.gears)
            {
                Vector3 eye=new Vector3(gear.transform.position.x,1.62f,gear.transform.position.z+1.2f);
                var target=game.TargetForRay(new Ray(eye,gear.transform.position-eye));
                if(!target || target.gear!=gear){reachable=false;report.Add("DETAIL unreachable socket "+(gear.index+1)+" hit="+(target?target.name:"none"));}
            }
            Check(reachable,"All 17 gear sockets can be targeted from standing height within E interaction range");
            Check(game.RepairGoal==6,"The shift requires six repairs");
            var backing=game.gears[0].transform.parent.Find("Machine chassis • cardboard wall texture").GetComponent<Renderer>();
            Check(backing.sharedMaterial.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(FactoryWallsBoxInstaller.BackboardTexture),"The blue backing uses the wall texture");
            var meals=new[]{game.lunch.tray}.Concat(game.lunch.diners.meals).ToArray();
            foreach(var meal in meals)
            {
                var plate=meal.Find("Plate • textured model");
                Check(plate && plate.GetComponent<MeshFilter>().sharedMesh.name=="LunchPlate" && plate.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(FactoryShiftUpgrade.PlateTexture),"Textured Plate.fbx dish on "+meal.name);
            }
            var material=meals[0].Find("Plate • textured model").GetComponent<Renderer>().sharedMaterial;
            Check(material.GetFloat("_Cull")==0 && material.doubleSidedGI,"Plate material is double sided");
            var physics=game.lunch.physicsTray;
            Check(physics && physics.contents.Length==3,"Player tray has independent plate, food and cup bodies");
            Check(physics.contents.All(b=>b.GetComponentsInChildren<MeshCollider>().All(c=>c.convex)),"All dynamic meal colliders are convex");
            Check(physics.GetComponentsInChildren<BoxCollider>().Length==5,"Tray has a solid base and four open physical rims");
            foreach(var worker in game.crowd.runners.Where(w=>!w.isPlayer))
            {
                var impact=worker.GetComponent<FactoryWorkerImpact>();
                Check(impact && impact.message && impact.voices.Length==3 && impact.voice && impact.message.GetComponent<Renderer>().sharedMaterial.GetFloat("_Cull")==0,"Worker has a double-sided message and three sound variants: "+worker.name);
            }
            Check(game.crowd.runners.Where(w=>!w.isPlayer).All(w=>w.puppet.gameObject.activeSelf),"All 11 worker puppets remain visible");
        }
        static void Orders()
        {
            var firsts=new HashSet<int>();bool single=true;
            for(int i=0;i<40;i++)
            {
                game.ResetShift();firsts.Add(game.CurrentOrder);
                single &= game.gears.Count(g=>g.broken)==1 && game.Memorized==game.gears[game.CurrentOrder-1] && game.OrderedType==game.Memorized.recipe.Type;
            }
            Check(single,"Every random fault prints the exact broken socket and recipe");
            Check(firsts.Count>=10,"Random starts cover many different sockets ("+firsts.Count+" / 17)");
            var station=game.station;game.ResetShift();station.Tick(.65f);
            Check(Mathf.Abs(station.OrderTravel-.25f)<.001f && station.IsPrinting,"Old 0.65 second duration now prints only one quarter of the card");
            Check(station.orderView.GearType==game.OrderedType && station.orderCard.GetComponentsInChildren<TextMesh>(true).Length==0,"Card displays the broken gear model, without a numeric label");
            Check(station.printerSignal.speaker.clip==AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game_Assets/Sounds/Ring.mp3"),"Printer uses the supplied Ring.mp3 clip");
            Check(station.printerSignal.Signalling && station.printerSignal.beacon.enabled && station.printerSignal.speaker.isPlaying,"Printer glows red and beeps throughout printing");
            station.Tick(1.94f);Check(station.IsPrinting && station.OrderTravel<1,"The alert stays on until the card finishes");
            station.Tick(.02f);Check(!station.IsPrinting && !station.printerSignal.Signalling && !station.printerSignal.speaker.isPlaying,"Printing finishes at 2.6 seconds and silences the printer");
            var repaired=new HashSet<int>();
            Check(!game.ReadyToLeave,"Healthy idle sockets do not unlock the exit early");
            for(int i=0;i<game.RepairGoal;i++)
            {
                var gear=game.Memorized;repaired.Add(gear.index);game.ReceiveWorkbenchPart(gear.recipe);
                Check(game.TryInstall(gear) && !gear.broken,"Matching physical gear repairs random socket "+(gear.index+1));
                if(i<game.RepairGoal-1)
                {
                    Check(game.CurrentOrder==0 && !game.ReadyToLeave,"Repair leaves a quiet interval before the next random fault");
                    game.TickFaults(8.1f);Check(game.CurrentOrder>0 && game.gears.Count(g=>g.broken)==1,"A new random fault creates the next work order");
                }
            }
            Check(repaired.Count==game.RepairGoal && game.ReadyToLeave && game.WorkingGears==17,"Six distinct repaired sockets complete the shift");
            game.TickFaults(100);Check(game.CurrentOrder==0,"No new mandatory faults after the repair goal");
        }
        static void Trays()
        {
            game.ResetShift();var lunch=game.lunch;var tray=lunch.physicsTray;
            Place(CardboardRunner.Player,lunch.pickup.position+new Vector3(0,-1,-1));
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.identity);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Verification floor";floor.layer=8;floor.transform.position=new Vector3(200,-.2f,200);floor.transform.localScale=new Vector3(30,.4f,30);
            lunch.TickSchedule(61);Check(lunch.TryTake(),"Ready lunch tray can be picked up");
            Check(tray.contents.All(b=>!b.isKinematic && b.useGravity && b.transform.parent==lunch.transform),"Pickup detaches every dish and enables actual gravity");
            game.station.Tick(.65f);Check(game.station.OrderTravel>.24f,"Printer keeps printing while the player carries a tray");
            Simulate(tray,35);
            report.Add("DETAIL stable meal: "+string.Join("; ",tray.contents.Select(b=>b.name+" "+tray.transform.InverseTransformPoint(b.worldCenterOfMass))));
            Check(lunch.Carrying && tray.contents.All(b=>tray.transform.InverseTransformPoint(b.worldCenterOfMass).y>-.03f),"A level tray supports all three bodies without ejecting them");
            var dish=tray.contents[0];Vector3 before=dish.position;Vector3 root=tray.body.position;
            tray.Aim(root+Vector3.right*.15f,tray.body.rotation);Simulate(tray,1);
            Check(Mathf.Abs((dish.position-before).x)<.11f,"Dishes lag behind a sudden tray movement instead of following a parent transform");
            game.ResetShift();lunch.TickSchedule(61);Place(CardboardRunner.Player,lunch.pickup.position+new Vector3(0,-1,-1));
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.Euler(30,0,0));lunch.TryTake();Simulate(tray,40);
            Check(lunch.Carrying,"Looking down toward the counter does not instantly spill a newly picked-up tray");
            Camera.main.transform.rotation=Quaternion.identity;lunch.TickBalance(.02f,false,false);
            Place(CardboardRunner.Player,lunch.destination.position+new Vector3(0,-.8f,-1));
            Check(lunch.TryServe() && lunch.State==LunchState.Served && !tray.Carrying,"Player can set the balanced tray on the dining table");
            Check(tray.contents.All(b=>!b.isKinematic && b.useGravity),"Serving leaves the dishes physical instead of gluing them to the tray");
            // Check each open edge with real gravity and contact, not scripted spill trajectories.
            foreach(var angles in new[]{new Vector3(65,0,0),new Vector3(-65,0,0),new Vector3(0,0,65),new Vector3(0,0,-65)})
            {
                game.ResetShift();lunch.TickSchedule(61);Place(CardboardRunner.Player,lunch.pickup.position+new Vector3(0,-1,-1));
                Camera.main.transform.SetPositionAndRotation(new Vector3(200,2,200),Quaternion.identity);lunch.TryTake();
                tray.ResetContents();var position=new Vector3(200,1.8f,200);var rotation=Quaternion.Euler(angles);tray.Begin(position,rotation);
                var initial=tray.contents.Select(b=>b.position).ToArray();Simulate(tray,100);
                Vector3 direction=angles.x!=0?Vector3.forward*Mathf.Sign(angles.x):Vector3.left*Mathf.Sign(angles.z);
                float travelled=tray.contents.Select((b,i)=>Vector3.Dot(b.position-initial[i],direction)).Max();
                report.Add("DETAIL spill "+angles+" travelled="+travelled+" state="+lunch.State);
                Check(travelled>.17f && lunch.State==LunchState.Spilled,"Physical meal can fall over edge "+angles);
                report.Add("DETAIL floor: "+string.Join("; ",tray.contents.Select(b=>b.name+" origin="+b.position+" velocity="+b.linearVelocity)));
                Check(tray.contents.All(b=>b.GetComponentsInChildren<Collider>().Min(c=>c.bounds.min.y)>-.035f),"Spilled dishes collide with the floor: "+angles);
            }
            lunch.ResetLunch();Check(tray.contents.All(b=>b.isKinematic && b.transform.parent==tray.transform),"Restart restores every dish and its pickup pose");
        }
        static void Reactions()
        {
            game.ResetShift();Place(CardboardRunner.Player,new Vector3(200,.2f,200));
            var workers=game.crowd.runners.Where(w=>!w.isPlayer).ToArray();
            for(int i=0;i<workers.Length;i++){workers[i].enabled=false;Place(workers[i],new Vector3(300+i*5,0,300));}
            observed=workers[0];Place(observed,new Vector3(200,.2f,202));Place(workers[1],new Vector3(203,.2f,203));reaction=observed.GetComponent<FactoryWorkerImpact>();
            game.RegisterMistake("Ошибка станка для проверки",.5f);
            Check(reaction.Reacting && reaction.message.gameObject.activeSelf && reaction.voice.isPlaying,"A machine error makes the closest worker display Messege and speak");
            Check(!workers[1].GetComponent<FactoryWorkerImpact>().Reacting,"Other workers do not also react to the same machine error");
            reaction.ResetReaction();Check(!reaction.message.gameObject.activeSelf && !reaction.voice.isPlaying,"Reset removes the message and stops its sound");
                        var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.layer=8;occluder.transform.position=new Vector3(200,1.8f,201);occluder.transform.localScale=new Vector3(.8f,3.6f,.2f);Physics.SyncTransforms();
            Check(game.FindWitness()==workers[1],"A wall excludes the closer worker from witnessing a jump");
            game.ObserveConduct(true,0,.5f,false,.02f);Object.DestroyImmediate(occluder);
            Check(!reaction.Reacting && workers[1].GetComponent<FactoryWorkerImpact>().Reacting,"A witnessed conduct error uses its actual observer, even when another worker is closer");
            workers[1].GetComponent<FactoryWorkerImpact>().ResetReaction();
            reaction.ReceiveImpact(Vector3.forward,2);
            game.RegisterMistake("Попадание предметом для проверки",.5f,observed);
            Check(reaction.Reacting && reaction.PushVelocity.z>1 && reaction.message.gameObject.activeSelf && reaction.voice.isPlaying,"A hit worker recoils, displays Messege and plays its sound");
            var position=observed.transform.position;reaction.TickReaction(.05f);
            report.Add("DETAIL recoil before="+position+" after="+observed.transform.position+" velocity="+reaction.PushVelocity+" controlled="+game.captureSequence.ControlsWorkers+" reacting="+reaction.Reacting);
            Check(observed.transform.position.z>position.z,"Worker recoil physically moves the character controller");
            Check(Mathf.Abs(game.Mistakes-1.5f)<.001f,"Each tested reaction keeps the existing half-star penalties");
            reactionAt=Time.time;gearRotations=game.gears.Select(g=>g.healthy.localRotation).ToArray();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>75){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0)
                {
                    game=CardboardFactoryGame.Instance;game.enabled=false;game.station.enabled=false;
                    typeof(FactoryMainMenu).GetMethod("Close",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.mainMenu,null);game.mainMenu.enabled=false;
                    foreach(var worker in game.crowd.runners)worker.enabled=false;
                    var camera=Camera.main.GetComponent<CardboardCamera>();if(camera)camera.enabled=false;
                    Physics.simulationMode=SimulationMode.Script;
                    Configuration();Orders();Trays();Reactions();stage=1;Save();return;
                }
                if(stage==1 && Time.time-reactionAt>.3f)
                {
                    reaction.TickReaction(.7f);var toPlayer=CardboardRunner.Player.transform.position-observed.transform.position;toPlayer.y=0;
                    Check(Vector3.Dot(observed.transform.forward,toPlayer.normalized)>.95f,"Reacting worker turns toward the player");
                    Check(game.gears.Where(g=>!g.broken).All(g=>Quaternion.Angle(gearRotations[g.index],g.healthy.localRotation)>1),"Every healthy gear rotates during the shift");
                    Check(game.gears.Where(g=>g.broken).All(g=>Quaternion.Angle(gearRotations[g.index],g.healthy.localRotation)<.01f),"The randomly broken gear stops rotating");
                    Camera.main.transform.position=observed.transform.position+new Vector3(1.3f,1.6f,-3.3f);
                    Camera.main.transform.rotation=Quaternion.LookRotation(observed.transform.position+Vector3.up*1.3f-Camera.main.transform.position);
                    CardboardFactorySceneBuilder.Capture("WorkerReaction");stage=2;Save();
                }
                if(stage==2 && Time.time-reactionAt>3.1f)
                {
                    Check(!reaction.Reacting && !reaction.message.gameObject.activeSelf && !reaction.voice.isPlaying,"Reaction expiration hides the message and stops the loop");
                    Check(!reaction.TickReaction(.02f),"Worker releases the reaction and can resume its route");Finish();
                }
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
        public static void Preview()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try
            {
                var machine=game.gears[0].transform.parent;
                camera.transform.position=machine.TransformPoint(new Vector3(0,3.4f,13));camera.transform.rotation=Quaternion.LookRotation(machine.TransformPoint(new Vector3(0,3.4f,0))-camera.transform.position);camera.fieldOfView=51;
                CardboardFactorySceneBuilder.Capture("NewMechanism");
                camera.transform.position=game.lunch.tray.position+new Vector3(.65f,.85f,.8f);camera.transform.rotation=Quaternion.LookRotation(game.lunch.tray.position+Vector3.up*.04f-camera.transform.position);camera.fieldOfView=40;
                CardboardFactorySceneBuilder.Capture("NewLunchTray");
                var station=game.station;var card=station.orderCard;bool active=card.gameObject.activeSelf;var cardPosition=card.localPosition;var icons=station.orderView.icons.Select(i=>i.gameObject.activeSelf).ToArray();
                try
                {
                    card.gameObject.SetActive(true);card.localPosition=new Vector3(0,-.65f,.055f);station.orderView.ShowType(3);
                    var printer=card.parent;camera.transform.position=printer.TransformPoint(new Vector3(.32f,-.45f,1.9f));camera.transform.rotation=Quaternion.LookRotation(card.position-camera.transform.position);camera.fieldOfView=30;
                    CardboardFactorySceneBuilder.Capture("GearPunchcard");
                }
                finally{card.gameObject.SetActive(active);card.localPosition=cardPosition;for(int i=0;i<icons.Length;i++)station.orderView.icons[i].gameObject.SetActive(icons[i]);}
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
    }
}
