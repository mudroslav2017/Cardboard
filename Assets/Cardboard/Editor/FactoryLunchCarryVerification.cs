using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryLunchCarryVerification
    {
        const string Pending="Cardboard.LunchCarryVerification";
        static readonly List<string> report=new List<string>();
        static FactoryLunchCarryVerification(){EditorApplication.update+=Tick;}
        public static void Batch()
        {
            EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);
            FactoryCanteenInstaller.CarryUpdateCurrent();
            Run();
        }
        [MenuItem("Cardboard/Verify canteen tray carrying")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.WriteAllText("Library/FactoryLunchCarryVerification.txt","RUNNING");
            SessionState.SetBool(Pending,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);}
        static void Place(Vector3 position,Quaternion rotation)
        {
            var player=CardboardRunner.Player;var controller=player.GetComponent<CharacterController>();
            bool active=controller.enabled;controller.enabled=false;player.transform.position=position;controller.enabled=active;
            Camera.main.transform.SetPositionAndRotation(position+Vector3.up*1.62f,rotation);Physics.SyncTransforms();
        }
        static void StartLunch(CardboardFactoryGame game)
        {
            game.ResetShift();game.ReceiveWorkbenchPart(game.Memorized.recipe);game.TryInstall(game.Memorized);
            game.lunch.TickSchedule(game.lunch.lunchAfter+.1f);
            Place(new Vector3(game.lunch.pickup.position.x,0,game.lunch.pickup.position.z+1.5f),Quaternion.Euler(20,180,0));
        }
        static void Step(FactoryLunch lunch,int count,bool left=false,bool right=false,bool assist=false)
        {
            for(int i=0;i<count;i++)
            {
                if(assist){left=lunch.Tilt>.10f && lunch.AngularSpeed>0;right=lunch.Tilt<-.10f && lunch.AngularSpeed<0;}
                lunch.TickBalance(.02f,left,right);lunch.physicsTray.TickSupport(.02f);Physics.Simulate(.02f);
            }
        }
        static bool Attached(FactoryTrayPhysics tray)
        {
            for(int i=0;i<tray.contents.Length;i++)
            {
                var item=tray.contents[i];
                if(!item.isKinematic || item.useGravity || item.detectCollisions || item.transform.parent!=tray.transform ||
                    Vector3.Distance(item.transform.localPosition,tray.homes[i])>.0001f ||
                    Quaternion.Angle(item.transform.localRotation,tray.rotations[i])>.01f)return false;
            }
            return true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            var game=CardboardFactoryGame.Instance;if(!game || !CardboardRunner.Player || Time.time<.3f)return;
            SessionState.SetBool(Pending,false);report.Clear();
            var simulation=Physics.simulationMode;var previousKeyboard=Keyboard.current;Keyboard keyboard=null;
            try
            {
                game.ResetShift();game.mainMenu.enabled=false;game.enabled=false;game.intro.enabled=false;
                Camera.main.GetComponent<CardboardCamera>().enabled=false;
                foreach(var runner in game.crowd.runners)runner.enabled=false;
                var lunch=game.lunch;var tray=lunch.physicsTray;var player=CardboardRunner.Player;
                lunch.enabled=false;lunch.diners.enabled=false;tray.enabled=false;
                Physics.simulationMode=SimulationMode.Script;
                Check(lunch.destination.position.z< -29 && Vector3.Distance(lunch.pickup.position,lunch.destination.position)>15,
                    "Delivery is at the far end dining table, over 15 metres from pickup");
                var lights=game.GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith("Canteen fill light")).ToArray();
                Check(lights.Length==6 && lights.All(l=>l.enabled && l.intensity>=4 && l.range>=12),"Six bright warm fill lights cover the canteen");
                Check(!lunch.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Put tray here • placeholder" || t.name=="Stand here to eat • placeholder"),
                    "Yellow delivery blocks are removed from the table and floor");
                var deliveryLight=game.GetComponentsInChildren<Light>().FirstOrDefault(l=>l.name=="Canteen delivery spotlight");
                Check(deliveryLight && deliveryLight.enabled && deliveryLight.intensity>lunch.traySpotlight.intensity &&
                    lights.All(l=>l.intensity<deliveryLight.intensity) &&
                    Vector3.Angle(deliveryLight.transform.forward,lunch.destination.position-deliveryLight.transform.position)<.1f,
                    "The brightest canteen light points directly at the tray delivery spot");
                Place(new Vector3(-13,0,-17.4f),Quaternion.Euler(6,180,0));
                CardboardFactorySceneBuilder.Capture("CanteenCarryLighting");
                Place(new Vector3(-10.5f,0,-32),Quaternion.Euler(18,66,0));
                CardboardFactorySceneBuilder.Capture("CanteenDeliveryLight");
                StartLunch(game);Check(lunch.TryTake(),"Lunch tray can be picked up");
                Check(Attached(tray),"All dishes are fixed to the tray with gravity and collisions disabled");
                var hud=UnityEngine.Object.FindFirstObjectByType<FactoryLunchHUD>();hud.SendMessage("LateUpdate");
                Check(hud.hint.gameObject.activeInHierarchy && hud.hint.text.Contains("LMB") && hud.hint.text.Contains("RMB") &&
                    hud.title.text.Contains("FAR END TABLE"),"English balance controls and far table objective are visible while carrying");
                Check(!lunch.TryServe(),"Serving at the counter is rejected");
                lunch.RefreshDeliveryLight();float bright=deliveryLight.intensity;
                Step(lunch,45,assist:true);lunch.RefreshDeliveryLight();float dim=deliveryLight.intensity;
                Step(lunch,25,assist:true);lunch.RefreshDeliveryLight();
                Check(bright==24 && dim==2 && deliveryLight.intensity==24 && lunch.deliveryBulb,
                    "Delivery spotlight and visible bulb blink while carrying");
                var cameraMotion=Camera.main.GetComponent<CardboardCamera>();
                Check(lunch.diners.places.All(p=>Mathf.Abs(p.position.x-lunch.destination.position.x)>1.35f ||
                    Mathf.Abs(p.position.z-(lunch.destination.position.z+.45f))>2.4f),"Diners leave the delivery table's central approach clear");
                keyboard=InputSystem.AddDevice<Keyboard>();
                foreach(bool sprint in new[]{false,true})
                {
                    InputSystem.QueueStateEvent(keyboard,sprint?new KeyboardState(Key.W,Key.LeftShift):new KeyboardState(Key.W));InputSystem.Update();
                    for(int i=0;i<30;i++)player.TickMovement(.02f);
                    Check(player.speed<=1.31f && player.speed>1.2f,"Carrying speed stays capped at 1.3 m/s with Shift="+sprint);
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                StartLunch(game);
                foreach(var worker in lunch.diners.workers)
                {
                    int index=Array.IndexOf(lunch.diners.workers,worker);var controller=worker.GetComponent<CharacterController>();
                    controller.enabled=false;worker.transform.SetPositionAndRotation(lunch.diners.places[index].position,lunch.diners.places[index].rotation);controller.enabled=true;
                }
                lunch.diners.Tick(.02f);Physics.SyncTransforms();
                var route=new FactoryCaptureNavigation(.4f).Path(player.transform.position,new Vector3(-7,0,-31.5f));
                Check(route.Count>3 && Vector3.Distance(route[route.Count-1],new Vector3(-7,0,-31.5f))<.6f,
                    "A walkable route reaches the far table between the dining furniture");
                lunch.TryTake();int point=0;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();
                for(int i=0;i<2500 && point<route.Count && lunch.Carrying;i++)
                {
                    var delta=route[point]-player.transform.position;delta.y=0;
                    if(delta.magnitude<.22f){point++;continue;}
                    var view=Quaternion.LookRotation(delta);
                    Camera.main.transform.SetPositionAndRotation(player.transform.position+Vector3.up*1.62f,view);
                    player.TickMovement(.02f);Camera.main.transform.position=player.transform.position+Vector3.up*1.62f;
                    Step(lunch,1,assist:true);
                }
                for(int i=0;i<100 && point==route.Count && lunch.Carrying && !lunch.CanServe;i++)Step(lunch,1,assist:true);
                lunch.SendMessage("LateUpdate");
                report.Add("DETAIL occupied route: "+point+"/"+route.Count+" player="+player.transform.position+" state="+lunch.State+" canServe="+lunch.CanServe);
                Check(point==route.Count && lunch.Carrying && lunch.CanServe && Attached(tray),
                    "Balanced player physically reaches the delivery spot with all diners and meals present");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                StartLunch(game);lunch.TryTake();Place(new Vector3(-13,0,-25),Quaternion.Euler(0,180,0));cameraMotion.AdoptCurrentView();
                tray.ResetContents();tray.Begin(new Vector3(-13,1.32f,-25.72f),Quaternion.Euler(0,180,0));
                float minX=player.transform.position.x,maxX=minX,maxCameraRoll=0;
                for(int i=0;i<250 && lunch.Carrying;i++)
                {
                    player.TickMovement(.02f);cameraMotion.RefreshPlayerPose(.02f);Step(lunch,1,assist:true);
                    minX=Mathf.Min(minX,player.transform.position.x);maxX=Mathf.Max(maxX,player.transform.position.x);
                    maxCameraRoll=Mathf.Max(maxCameraRoll,Mathf.Abs(Mathf.DeltaAngle(0,Camera.main.transform.eulerAngles.z)));
                }
                Check(lunch.Carrying && maxX-minX>.15f && maxCameraRoll>5,"Carrying sways the player sideways and visibly rolls the camera");
                Place(new Vector3(200,0,200),Quaternion.identity);tray.ResetContents();tray.Begin(new Vector3(200,1.32f,200.72f),Quaternion.identity);
                for(int i=0;i<1500 && lunch.Carrying;i++)
                {
                    float t=i*.02f;Camera.main.transform.SetPositionAndRotation(new Vector3(200+t*1.3f,1.62f,200),Quaternion.Euler(55*Mathf.Sin(t*.7f),180*Mathf.SmoothStep(0,1,t/12),0));
                    Step(lunch,1,assist:true);
                }
                Check(lunch.Carrying && Attached(tray),"Dishes retain their exact local poses during 30 seconds of walking, turning and looking up/down");
                tray.body.rotation=Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,25);Step(lunch,200,assist:true);
                Check(lunch.Carrying && Mathf.Abs(tray.RollDegrees)<20,"Balance controls recover a 25 degree lean despite the player's sway; roll="+tray.RollDegrees);
                StartLunch(game);lunch.TryTake();Place(new Vector3(200,0,200),Quaternion.identity);tray.ResetContents();tray.Begin(new Vector3(200,1.32f,200.72f),Quaternion.identity);
                tray.body.rotation=Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0);tray.body.angularVelocity=Vector3.zero;
                Step(lunch,15,true,false);float leftRoll=tray.RollDegrees;
                StartLunch(game);lunch.TryTake();Place(new Vector3(200,0,200),Quaternion.identity);tray.ResetContents();tray.Begin(new Vector3(200,1.32f,200.72f),Quaternion.identity);
                tray.body.rotation=Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0);tray.body.angularVelocity=Vector3.zero;
                Step(lunch,15,false,true);
                Check(leftRoll>1 && tray.RollDegrees< -1,"LMB and RMB balance in opposite directions");
                StartLunch(game);lunch.TryTake();Place(new Vector3(200,0,200),Quaternion.identity);tray.ResetContents();tray.Begin(new Vector3(200,1.32f,200.72f),Quaternion.identity);
                float unbalancedRoll=0;
                for(int i=0;i<750 && lunch.Carrying;i++){Step(lunch,1);unbalancedRoll=Mathf.Max(unbalancedRoll,Mathf.Abs(tray.RollDegrees));}
                Check(unbalancedRoll>18,"Player sway produces a substantial tray lean when balance is left unattended; peak="+unbalancedRoll);
                StartLunch(game);lunch.TryTake();
                Place(new Vector3(-7,0,-25.7f),Quaternion.Euler(15,180,0));lunch.SendMessage("LateUpdate");
                Check(!lunch.TryServe(),"The former near table cannot complete the delivery");
                Place(new Vector3(-7,0,-31.7f),Quaternion.Euler(15,0,0));
                tray.body.rotation=Quaternion.Euler(0,0,24);lunch.SendMessage("LateUpdate");
                Check(!lunch.TryServe(),"A heavily tilted tray cannot be served");
                hud.SendMessage("LateUpdate");Check(hud.title.text.Contains("E - SET DOWN TRAY") && hud.title.text.Contains("HOLD IT LEVEL"),"English E hint appears near the table and explains when the tray needs leveling");
                tray.body.rotation=Quaternion.identity;tray.body.angularVelocity=Vector3.zero;lunch.SendMessage("LateUpdate");
                Physics.SyncTransforms();
                var interaction=game.TargetForRay(new Ray(Camera.main.transform.position,lunch.destinationHit.transform.position-Camera.main.transform.position));
                Check(interaction && interaction.GetComponent<FactoryLunchControl>() && interaction.GetComponent<FactoryLunchControl>().serve,
                    "Delivery interaction is reachable by the player's ray at the far table");
                Camera.main.transform.rotation=Quaternion.Euler(0,180,0);game.SendMessage("UpdateFocus");
                hud.SendMessage("LateUpdate");CardboardFactorySceneBuilder.Capture("CanteenDeliveryPrompt");
                game.Interact();Check(lunch.State==LunchState.Served && !game.HandsBusy,"E sets down a level tray near the table without aiming through diners");
                cameraMotion.RefreshPlayerPose(.02f);lunch.RefreshDeliveryLight();
                Check(lunch.SwayRoll==0 && lunch.StaggerVelocity==0 && Mathf.Abs(Mathf.DeltaAngle(0,Camera.main.transform.eulerAngles.z))<.1f && deliveryLight.intensity==24,
                    "Serving stops the drunken sway and restores a steady delivery light");
                var parked=tray.transform.position;Step(lunch,250);
                Check(tray.body.isKinematic && Attached(tray) && Vector3.Distance(parked,lunch.destination.position)<.001f &&
                    Vector3.Distance(parked,tray.transform.position)<.001f,"Served tray and all dishes stay fixed at the far table");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();
                for(int i=0;i<50;i++)player.TickMovement(.02f);
                Check(player.speed>3.1f,"Normal walking speed returns after serving");
                StartLunch(game);lunch.TryTake();
                Place(new Vector3(200,0,200),Quaternion.identity);tray.ResetContents();tray.Begin(new Vector3(200,1.32f,200.72f),Quaternion.identity);
                Step(lunch,750,false,true);
                Check(lunch.State==LunchState.Spilled && game.Mistakes==1,"Sustained overbalancing still drops the tray and awards one penalty");
                Check(tray.contents.All(item=>!item.isKinematic && item.useGravity && item.detectCollisions && item.transform.parent==lunch.transform),
                    "Dropping restores dish physics, collision and independent movement");
                lunch.Spill();Check(game.Mistakes==1,"A spill awards its penalty only once");
                game.ResetShift();Check(Attached(tray) && tray.body.isKinematic,"Restart restores every dish and the tray");
                StartLunch(game);lunch.TryTake();Place(new Vector3(-13,0,-26),Quaternion.Euler(14,180,0));
                tray.ResetContents();tray.Begin(Camera.main.transform.position+Quaternion.Euler(0,180,0)*new Vector3(0,-.3f,.72f),Quaternion.Euler(4,180,0));
                Step(lunch,120,assist:true);hud.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();
                CardboardFactorySceneBuilder.Capture("CanteenCarryControls");
            }
            catch(Exception error){Check(false,error.ToString());Debug.LogException(error);}
            finally
            {
                Physics.simulationMode=simulation;
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
                File.WriteAllLines("Library/FactoryLunchCarryVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));
                EditorApplication.isPlaying=false;
                if(Application.isBatchMode)EditorApplication.Exit(report.Any(l=>l.StartsWith("FAIL"))?1:0);
            }
        }
    }
}
