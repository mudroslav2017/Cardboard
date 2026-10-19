using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryPhysicsVerification
    {
        const string Active="Cardboard.PhysicsVerification";
        static readonly List<string> report=new List<string>();
        static int stage;
        static double started,hitAt;
        static bool background,cursorOnly;
        static InputSettings.BackgroundBehavior inputBackground;
        static Mouse mouse,previousMouse;
        static SimulationMode simulation;
        static CardboardFactoryGame game;
        static CardboardRunner worker;
        static Vector3 workerHome;
        static FactoryWorkerImpact reaction;
        static FactoryPhysicsProp projectile;
        static FactoryPhysicsVerification(){EditorApplication.update+=Tick;}
        static void Check(bool value,string message){report.Add((value?"PASS ":"FAIL ")+message);}
        [MenuItem("Cardboard/Verify physical pickup, throws, workers and cursor")]
        public static void Run(){if(!EditorApplication.isPlayingOrWillChangePlaymode){cursorOnly=false;Begin();}}
        [MenuItem("Cardboard/Verify cursor tip and native visibility")]
        public static void RunCursor(){if(!EditorApplication.isPlayingOrWillChangePlaymode){cursorOnly=true;Begin();}}
        static void Begin()
        {
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            inputBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=false;
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Save()
        {
            File.WriteAllLines(cursorOnly?"Library/FactoryCursorVerification.txt":"Library/FactoryPhysicsVerification.txt",
                new[]{"RESULT: "+report.Count(line=>line.StartsWith("PASS"))+" PASS, "+report.Count(line=>line.StartsWith("FAIL"))+" FAIL"}.Concat(report));
        }
        static void Finish()
        {
            Save();SessionState.SetBool(Active,false);
            Physics.simulationMode=simulation;Application.runInBackground=background;
            InputSystem.settings.backgroundBehavior=inputBackground;
            if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);
            if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
            Cursor.visible=true;EditorApplication.isPlaying=false;
        }
        static void Simulate(int steps,bool hold=false)
        {
            Physics.SyncTransforms();
            for(int i=0;i<steps;i++)
            {
                if(hold)game.objectCarry.TickGrip();
                Physics.Simulate(.02f);
            }
        }
        static void Point(Vector2 point,bool pressed=false)
        {InputSystem.EnableDevice(mouse);InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=(ushort)(pressed?1:0)});InputSystem.Update();mouse.MakeCurrent();}
        static void Menu()
        {
            var menu=game.mainMenu;menu.SuppressQuit=true;menu.Tick(1);menu.Tick(.02f);
            var cursor=menu.pointer;cursor.SendMessage("OnApplicationFocus",true);
            Check(cursor && cursor.model && cursor.model.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.001f,"Cursor keeps authored three-dimensional geometry");
            Check(cursor.model.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap") &&
                cursor.model.GetComponent<Renderer>().sharedMaterial.GetFloat("_Cull")==0,"Cursor uses original texture and double-sided material");
            Check(!cursor.model.GetComponent<Collider>(),"Visible cursor does not intercept button raycasts");
            Cursor.lockState=CursorLockMode.None;
            foreach(var button in menu.buttons)
            {
                Point(menu.lens.WorldToScreenPoint(button.hit.transform.TransformPoint(button.hit.center)));cursor.Refresh(.02f);Physics.SyncTransforms();
                var screen=menu.lens.WorldToScreenPoint(cursor.HotspotWorldPosition);
                Check(Vector2.Distance(new Vector2(screen.x,screen.y),cursor.ScreenPosition)<.05f,"Cursor hotspot matches mouse on "+button.action);
                bool reaches=Physics.Raycast(cursor.PointerRay,out var hit,15,1<<10) && hit.collider==button.hit;
                Check(reaches,"Cursor ray reaches "+button.action+" button");
                if(!reaches)report.Add("DETAIL pointer="+cursor.ScreenPosition+" target="+menu.lens.WorldToScreenPoint(button.hit.bounds.center)+" enabled="+button.hit.enabled+" hit="+(hit.collider?hit.collider.name:"none"));
                var point=cursor.ScreenPosition;float scale=cursor.model.localScale.x;Point(point,true);
                for(int i=0;i<30;i++)cursor.Refresh(1f/60);
                screen=menu.lens.WorldToScreenPoint(cursor.HotspotWorldPosition);
                Check(cursor.model.localScale.x<scale && Vector2.Distance(new Vector2(screen.x,screen.y),point)<.05f,
                    "Cursor tip stays on the target during click scaling: "+button.action);
                Point(point);cursor.Refresh(.02f);
            }
            Point(menu.lens.WorldToScreenPoint(menu.buttons[1].hit.bounds.center));cursor.Refresh(.02f);
            report.Add("DETAIL cursor model active="+cursor.model.gameObject.activeInHierarchy+" position="+cursor.model.localPosition+" scale="+cursor.model.localScale+" bounds="+cursor.model.GetComponent<Renderer>().bounds+" screen="+cursor.ScreenPosition+" camera="+menu.lens.pixelRect+" overlay="+cursor.overlay.enabled);
            CardboardFactorySceneBuilder.Capture("VolumeCursorMenu");
            Check(!Cursor.visible && cursor.model.gameObject.activeSelf,"Native cursor is replaced by the visible imported cursor");
            var vertices=cursor.model.GetComponent<MeshFilter>().sharedMesh.vertices;
            Check(vertices.Any(vertex=>Vector3.Distance(vertex,cursor.HotspotLocal)<.00001f),"Hotspot is an actual model vertex");
            Check(vertices.All(vertex=>menu.lens.transform.InverseTransformVector(cursor.model.TransformPoint(vertex)-cursor.HotspotWorldPosition).y<.0001f),
                "The top of the rotated model is the pointer hotspot");
            FactoryVolumeCursor.SetSystemVisible(true);
            Check(!Cursor.visible,"Other controls cannot reveal the native cursor while the custom cursor owns it");
            menu.SendMessage("Update");
            Check(!Cursor.visible,"Menu Update does not toggle the system cursor on");
            cursor.SendMessage("OnApplicationFocus",false);
            Check(Cursor.visible && !cursor.model.gameObject.activeSelf,"Losing game focus restores the desktop cursor");
            cursor.SendMessage("OnApplicationFocus",true);cursor.Refresh(.02f);
            Check(!Cursor.visible && cursor.model.gameObject.activeSelf,"Regaining focus hides the system cursor immediately");
            cursor.enabled=false;FactoryVolumeCursor.SetSystemVisible(true);
            Check(Cursor.visible,"Ordinary cursor remains available without the custom cursor");
            cursor.enabled=true;cursor.Refresh(.02f);
            Check(!Cursor.visible,"Re-enabling the custom cursor reclaims native visibility");
            Cursor.lockState=CursorLockMode.Locked;cursor.Refresh(.02f);
            Check(!cursor.model.gameObject.activeSelf,"Cursor hides while the player controls the camera");
            Cursor.lockState=CursorLockMode.None;menu.Choose(FactoryMenuAction.Settings);menu.Tick(.3f);menu.Tick(menu.settingsEnterDelay+.9f);menu.Tick(.02f);
            Point(menu.lens.WorldToScreenPoint(menu.back.hit.bounds.center));cursor.Refresh(.02f);Physics.SyncTransforms();
            Check(Physics.Raycast(cursor.PointerRay,out var backHit,15,1<<10) && backHit.collider==menu.back.hit,"Imported cursor reaches settings Back button");
            Point(menu.lens.WorldToScreenPoint(menu.volumeTrack.bounds.center));cursor.Refresh(.02f);
            Check(menu.volumeTrack.Raycast(cursor.PointerRay,out _,15),"Imported cursor reaches volume slider");
            menu.SendMessage("Close");cursor.Refresh(.02f);
            Check(cursor.overlay.enabled && cursor.model.gameObject.activeSelf,"Free pointer also renders after leaving the main menu");
        }
        static void Place(FactoryPhysicsProp prop,Vector3 position)
        {
            prop.Body.linearVelocity=prop.Body.angularVelocity=Vector3.zero;
            prop.Body.position=position;prop.Body.rotation=Quaternion.identity;prop.Body.WakeUp();Physics.SyncTransforms();
        }
        static void CarryAndDrop(FactoryPhysicsProp prop)
        {
            var lens=Camera.main;lens.transform.SetPositionAndRotation(new Vector3(200,1.62f,200),Quaternion.identity);
            Place(prop,new Vector3(200,1,201.2f));
            Check(prop.Take(game),"E pickup path accepts "+prop.itemName);
            Check(prop.IsHeld && !prop.Body.isKinematic && !prop.Body.useGravity,"Held "+prop.itemName+" remains a dynamic rigidbody");
            Vector3 before=prop.Body.position;Simulate(1,true);
            Check(Vector3.Distance(before,prop.Body.position)<.35f,"Pickup begins with a smooth physical step: "+prop.itemName);
            Simulate(90,true);
            Check(Vector3.Distance(prop.Body.position,game.objectCarry.TargetPosition)<.12f,"Held "+prop.itemName+" settles at the grip");
            report.Add("DETAIL grip "+prop.itemName+" body="+prop.Body.position+" target="+game.objectCarry.TargetPosition+" velocity="+prop.Body.linearVelocity+" com="+prop.Body.worldCenterOfMass+" scale="+prop.transform.lossyScale+" timeScale="+Time.timeScale);
            lens.transform.rotation=Quaternion.Euler(0,24,0);before=prop.Body.position;Simulate(1,true);
            Check(Vector3.Distance(prop.Body.position,game.objectCarry.TargetPosition)>.04f &&
                Vector3.Distance(before,prop.Body.position)<.3f,"Held "+prop.itemName+" trails a camera turn");
            Simulate(100,true);
            if(prop.instructionSheet)
            {
                game.instruction.RefreshHints();CardboardFactorySceneBuilder.Capture("PhysicalInstructionFront");
                Check(game.instruction.Flip(),"Instruction still flips on its separate action");
                Simulate(100,true);
                Check(Quaternion.Angle(prop.Body.rotation,lens.transform.rotation*Quaternion.Euler(0,180,0))<4,"Instruction physically turns to its reverse side");
                Check(game.instruction.controlHint.GetComponent<TextMesh>().text=="LBM - TROW, RBM - PUT" && !game.instruction.flipHint,"Instruction shows the exact requested single control line");
                game.instruction.RefreshHints();
                Check(!game.instruction.controlHint.gameObject.activeSelf && (!game.instruction.flipHint || !game.instruction.flipHint.gameObject.activeSelf),"Front hints do not show through the reverse side");
                CardboardFactorySceneBuilder.Capture("PhysicalInstruction");
            }
            before=prop.Body.position;game.Interact();
            Check(game.objectCarry.Held==prop,"E does not put down "+prop.itemName);
            game.objectCarry.TickInput(false,true);
            Check(!game.objectCarry.Held && !prop.IsHeld && prop.Body.useGravity,"RMB releases "+prop.itemName+" and restores gravity");
            Check(Vector3.Distance(before,prop.Body.position)<.0001f,"Release keeps current height without snapping to the floor: "+prop.itemName);
            Simulate(20);
            Check(prop.Body.position.y<before.y-.12f,"Released "+prop.itemName+" falls under gravity");
            Simulate(170);
            Check(prop.GetComponentsInChildren<Collider>().Min(shape=>shape.bounds.min.y)>-.08f,"Released "+prop.itemName+" does not tunnel through the floor");
            report.Add("DETAIL landed "+prop.itemName+" position="+prop.Body.position+" bounds="+prop.GetComponent<Collider>().bounds);
            prop.ResetHome();
        }
        static void WorkstationControls()
        {
            Physics.SyncTransforms();
            foreach(var control in game.station.GetComponentsInChildren<FactoryStationControl>(true))
            {
                var shape=control.GetComponent<BoxCollider>();if(!shape || !shape.enabled || !control.gameObject.activeInHierarchy)continue;
                Vector3 direction=control.part==StationPart.Bath?control.transform.TransformDirection(Vector3.back):-game.station.transform.forward;
                Vector3 target=shape.bounds.center;
                var focus=game.TargetForRay(new Ray(target+direction*1.4f,-direction));
                Check(focus && focus.GetComponent<FactoryStationControl>()==control,"Physical workstation geometry preserves interaction ray: "+control.part+" "+control.number);
                if(!focus || focus.GetComponent<FactoryStationControl>()!=control)
                    report.Add("DETAIL control blocked "+control.name+" by "+(focus?focus.name:"solid geometry"));
            }
        }
        static void ClickThrows(FactoryPhysicsProp prop)
        {
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,1.62f,200),Quaternion.identity);
            var speeds=new List<float>();
            foreach(int heldFrames in new[]{1,30,90})
            {
                Place(prop,new Vector3(200,1,201.3f));prop.Take(game);
                for(int i=0;i<heldFrames;i++)game.objectCarry.TickInput(false,false);
                Check(prop.IsHeld,"Waiting without a new click keeps "+prop.itemName+" in hand");
                game.objectCarry.TickInput(true,false);speeds.Add(prop.Body.linearVelocity.z);
                Check(!prop.IsHeld && prop.Body.useGravity && prop.ThrowArmed && prop.Body.linearVelocity.z>7,"LMB press immediately throws "+prop.itemName+" at full fixed speed");
                game.objectCarry.TickInput(false,false);
                Check(Mathf.Abs(prop.Body.linearVelocity.z-speeds[speeds.Count-1])<.001f,"Holding or releasing LMB does not produce a second impulse");
                prop.ResetHome();
            }
            Check(speeds.Max()-speeds.Min()<.001f,"Time spent holding the object never changes its throw speed: "+prop.itemName);
            Place(prop,new Vector3(200,1,201.3f));prop.Take(game);Vector3 position=prop.Body.position;
            game.objectCarry.TickInput(false,true);
            Check(!prop.IsHeld && !prop.ThrowArmed && prop.Body.linearVelocity.magnitude<=.56f && Vector3.Distance(position,prop.Body.position)<.0001f,"RMB gently puts down "+prop.itemName+" at its current position");
            prop.ResetHome();
        }
        static void ClickGear()
        {
            var speeds=new List<float>();
            for(int attempt=0;attempt<3;attempt++)
            {
                game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));game.objectCarry.TickInput(false,false);
                Check(game.HasPart,"Manufactured gear stays in hand without a click");game.objectCarry.TickInput(true,false);
                var loose=game.GetComponentsInChildren<FactoryPhysicsProp>().First(prop=>prop.workbenchPart);
                speeds.Add(loose.Body.linearVelocity.z);Check(!game.HasPart && loose.ThrowArmed && loose.Body.linearVelocity.z>7,"LMB press immediately throws the manufactured gear");
                loose.gameObject.SetActive(false);
            }
            Check(speeds.Max()-speeds.Min()<.001f,"Manufactured gears use the same fixed throw speed on every click");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));game.Interact();Check(game.HasPart,"E does not place manufactured gears");
            game.objectCarry.TickInput(false,true);var placed=game.GetComponentsInChildren<FactoryPhysicsProp>().First(prop=>prop.workbenchPart);
            Check(!game.HasPart && placed.Body.linearVelocity.sqrMagnitude<.001f,"RMB gently puts down a manufactured gear");placed.gameObject.SetActive(false);
        }
        static void PhysicsChecks()
        {
            WorkstationControls();
            game.enabled=false;Camera.main.GetComponent<CardboardCamera>().enabled=false;
            foreach(var runner in game.crowd.runners)runner.enabled=false;
            game.lunch.enabled=false;game.objectCarry.enabled=false;game.instruction.enabled=false;
            var controller=CardboardRunner.Player.GetComponent<CharacterController>();controller.enabled=false;
            CardboardRunner.Player.transform.position=new Vector3(200,0,200);controller.enabled=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Physics verification floor";
            floor.transform.position=new Vector3(200,-.1f,204);floor.transform.localScale=new Vector3(14,.2f,14);floor.layer=8;
            var props=game.GetComponentsInChildren<FactoryPhysicsProp>();
            foreach(var prop in props)prop.Body.interpolation=RigidbodyInterpolation.None;
            Check(props.Length==53,"Instruction, 34 barrels and 18 empty boxes are physical");
            Check(props.All(prop=>prop.Body && !prop.Body.isKinematic && prop.GetComponent<Collider>()),"Every pickup has a dynamic body and solid collision");
            Check(!game.GetComponentsInChildren<FactoryStationControl>().Where(control=>control.part==StationPart.Bath)
                .Any(control=>control.GetComponentInParent<FactoryPhysicsProp>()),"Liquid baths remain fixed workstation controls");
            CarryAndDrop(game.instruction.physical);
            CarryAndDrop(props.First(prop=>prop.itemName=="бочку"));
            CarryAndDrop(props.First(prop=>prop.itemName=="коробку"));
            ClickThrows(game.instruction.physical);ClickThrows(props.First(prop=>prop.itemName=="бочку"));ClickThrows(props.First(prop=>prop.itemName=="коробку"));ClickGear();
            Camera.main.transform.SetPositionAndRotation(new Vector3(200,1.62f,200),Quaternion.identity);
            var box=props.First(prop=>prop.itemName=="коробку");Place(box,new Vector3(200,.8f,200.3f));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Grip collision test wall";
            wall.transform.position=new Vector3(200,1.5f,201.15f);wall.transform.localScale=new Vector3(6,3,.2f);wall.layer=8;
            box.Take(game);Simulate(100,true);
            Check(box.Body.worldCenterOfMass.z<201.12f,"Held box collides with a wall instead of passing through it");
            game.objectCarry.Release(false);wall.SetActive(false);box.ResetHome();
            var recipe=FactoryRecipe.ForType(2);game.ReceiveWorkbenchPart(recipe);
            var hand=Camera.main.GetComponentInChildren<FactoryHeldGearMotion>();var position=hand.transform.position;
            game.ReleasePart(true);
            var loose=game.GetComponentsInChildren<FactoryPhysicsProp>().First(prop=>prop.workbenchPart);
            Check(!game.HasPart && Vector3.Distance(loose.transform.position,position)<.001f,"Throwing a manufactured gear preserves its hand position");
            Check(loose.Body.linearVelocity.z>9 && loose.ThrowArmed,"Manufactured gear uses the same physical throw");
            Check(loose.Take(game) && game.HasPart && game.HeldRecipe.Equals(recipe),"Thrown gear can be retrieved with its recipe and finish intact");
            game.DropPart();
            foreach(var part in game.GetComponentsInChildren<FactoryPhysicsProp>().Where(prop=>prop.workbenchPart))
                part.gameObject.SetActive(false);
            worker=game.crowd.runners.First(runner=>!runner.isPlayer);workerHome=worker.transform.position;
            var workerController=worker.GetComponent<CharacterController>();if(workerController)workerController.enabled=false;
            worker.transform.SetPositionAndRotation(new Vector3(200,0,204),Quaternion.identity);
            if(workerController)workerController.enabled=true;
            reaction=worker.GetComponent<FactoryWorkerImpact>();
            projectile=props.First(prop=>prop.itemName=="бочку");Place(projectile,new Vector3(200,1,201.3f));
            Check(projectile.Take(game),"Barrel can be picked up before a throw");
            game.objectCarry.Release(true);
            Check(projectile.Body.linearVelocity.z>7 && projectile.ThrowArmed,"LMB throw launches barrel forward");
            Simulate(45);
            Check(reaction.Reacting && reaction.PushVelocity.z>0,"A real thrown-barrel collision triggers physical worker recoil");
            Check(Mathf.Abs(game.Mistakes-.5f)<.001f,"First thrown-object impact adds exactly half a star");
            var before=worker.transform.position;reaction.TickReaction(.05f);
            Check(Vector3.Distance(before,worker.transform.position)>.01f,"Impact physically pushes the worker capsule");
            Place(projectile,new Vector3(200,1,201.3f));projectile.Body.linearVelocity=Vector3.forward*10;Simulate(45);
            Check(Mathf.Abs(game.Mistakes-.5f)<.001f,"Repeated contact from the same throw does not award another penalty");
            hitAt=Time.time;Save();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            if(EditorApplication.timeSinceStartup-started>45 && started>0)
            {report.Add("FAIL Timed out at stage "+stage);Finish();return;}
            if(!EditorApplication.isPlaying || !CardboardRunner.Player || !CardboardFactoryGame.Instance)return;
            try
            {
                if(stage==0)
                {
                    game=CardboardFactoryGame.Instance;simulation=Physics.simulationMode;
                    Physics.simulationMode=SimulationMode.Script;previousMouse=Mouse.current;mouse=InputSystem.AddDevice<Mouse>();
                    Menu();if(cursorOnly){Finish();return;}PhysicsChecks();stage=1;return;
                }
                if(stage==1 && Time.time-hitAt>.3f)
                {
                    for(int i=0;i<40;i++)reaction.TickReaction(.02f);
                    var toward=CardboardRunner.Player.transform.position-worker.transform.position;toward.y=0;
                    Check(Vector3.Dot(worker.transform.forward,toward.normalized)>.95f,"Hit worker turns and looks toward the player");
                    report.Add("DETAIL gaze dot="+Vector3.Dot(worker.transform.forward,toward.normalized)+" time="+Time.time+" reaction="+reaction.Reacting);
                    Check(reaction.Reacting,"Worker holds the player's gaze briefly");
                    stage=2;Save();return;
                }
                if(stage==2 && Time.time-hitAt>3.1f)
                {
                    Check(!reaction.Reacting && !reaction.TickReaction(.02f),"Worker reaction expires and releases normal movement");
                    var controller=worker.GetComponent<CharacterController>();if(controller)controller.enabled=false;
                    worker.transform.position=workerHome;if(controller)controller.enabled=true;
                    worker.TickMovement(.1f);Check(worker.speed>0,"Worker resumes the factory route");
                    Finish();
                }
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
