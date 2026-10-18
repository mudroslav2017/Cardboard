using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryEnvironmentVerification
    {
        static bool running,oldBackground;static int stage;
        static readonly List<string> report=new List<string>();
        static FactoryEnvironmentVerification(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;running=true;oldBackground=Application.runInBackground;
            Application.runInBackground=true;FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static Bounds BoundsOf(Transform item)
        {
            var renderers=item.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer && !r.GetComponent<TextMesh>()).ToArray();
            var result=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))result.Encapsulate(renderer.bounds);return result;
        }
        static void MovePlayer(Vector3 eye,Vector3 target)
        {
            var player=CardboardRunner.Player;var body=player.GetComponent<CharacterController>();body.enabled=false;
            player.transform.position=new Vector3(eye.x,0,eye.z);body.enabled=true;
            Camera.main.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));Physics.SyncTransforms();
        }
        static void FocusDoor(CardboardFactoryGame game,FactoryDoor door,bool open)
        {
            var leaf=door.hinge.GetComponentInChildren<FactoryInteractable>();
            Vector3 eye=open?leaf.transform.position+leaf.transform.forward*1.7f:door.passage.TransformPoint(new Vector3(0,1.6f,2.4f));
            eye.y=1.6f;MovePlayer(eye,leaf.transform.position);
            game.SendMessage("UpdateFocus");
            Check(game.Focus && game.Focus.action==FactoryAction.Door && game.Focus.GetComponentInParent<FactoryDoor>()==door,"E focus reaches "+(open?"open":"closed")+" door: "+door.name);
            Check(game.FocusPrompt.Contains(open?"закрыть":"открыть"),"Door action prompt matches current state");
            game.Interact();
        }
        static void Doors(CardboardFactoryGame game)
        {
            var doors=game.GetComponentsInChildren<FactoryDoor>();Check(doors.Length==2,"Both canteen and factory exit have doors");
            foreach(var door in doors)
            {
                door.SetOpen(false,true);Physics.SyncTransforms();
                var from=door.passage.TransformPoint(new Vector3(0,1,1));
                var to=door.passage.TransformPoint(new Vector3(0,1,-1));
                Check(Physics.Linecast(from,to,out var hit,1<<8) && FactoryDoor.IsLeaf(hit.collider),"Closed door physically blocks passage: "+door.name);
                FocusDoor(game,door,false);Check(door.IsOpen,"E dispatch requests opening");
                door.Tick(.2f);Check(door.Openness>0 && door.Openness<1 && Quaternion.Angle(door.hinge.localRotation,Quaternion.identity)>1,"Door opens smoothly through intermediate pose");
                door.Tick(1);door.Tick(10);Physics.SyncTransforms();
                Check(door.IsOpen && door.Openness==1,"Door stays open without an auto-close timer");
                Check(!Physics.Linecast(from,to,1<<8),"Open door leaves actual passage clear");
                FocusDoor(game,door,true);Check(!door.IsOpen,"E dispatch closes the open door");
                door.Tick(1);Physics.SyncTransforms();
                Check(door.Openness==0 && Physics.Linecast(from,to,1<<8),"Door returns to frame and restores collision");
                door.SetOpen(true);door.Tick(.2f);float before=door.Openness;door.Toggle();door.Tick(.1f);
                Check(door.Openness<before && door.Openness>0,"A second press reverses a moving door smoothly");door.SetOpen(false,true);
            }
            var canteen=doors.First(door=>door.workersMayOpen);
            var start=canteen.passage.position+Vector3.forward*2.2f;start.y=0;
            MovePlayer(start+Vector3.up*1.62f,canteen.passage.position+Vector3.up*1.4f);
            var body=CardboardRunner.Player.GetComponent<CharacterController>();body.Move(Vector3.back*4.5f);
            Check(CardboardRunner.Player.transform.position.z>canteen.passage.position.z+.25f,"Player capsule cannot walk through closed door");
            canteen.SetOpen(true,true);Physics.SyncTransforms();body.Move(Vector3.back*4.5f);
            Check(CardboardRunner.Player.transform.position.z<canteen.passage.position.z-.5f,"Player capsule walks through open doorway");
            canteen.SetOpen(false,true);
            FactoryDoor.OpenForWorker(canteen.passage.position+Vector3.forward*1.8f,Vector3.back);
            Check(canteen.IsOpen,"Approaching coworker opens the lunch door");canteen.Tick(1);
            Check(!FactoryCaptureNavigation.LineBlocked(canteen.passage.position+new Vector3(0,1,1),canteen.passage.position+new Vector3(0,1,-1)),"Open lunch door also admits pursuit");
            canteen.SetOpen(false,true);Physics.SyncTransforms();
            Check(FactoryCaptureNavigation.LineBlocked(canteen.passage.position+new Vector3(0,1,1),canteen.passage.position+new Vector3(0,1,-1)),"Closed door prevents grabbing player through the panel");
            Check(!game.TryExit() && !game.Ended,"Opening an exit does not bypass repairing the three gears");
            Check(new FactoryCaptureNavigation(.78f).Path(new Vector3(-13,0,-25),game.station.transform.TransformPoint(new Vector3(0,0,-1.8f))).Count>3,"Navigation plans a carrying route through the usable door");
        }
        static void Surfaces(CardboardFactoryGame game)
        {
            float floor=FactoryEnvironmentInstaller.FloorY(game.transform);var used=new HashSet<Texture>();
            foreach(var station in game.stations)
            {
                Check(Mathf.Abs(BoundsOf(station.transform.Find("Table")).min.y-floor)<.006f,"Table legs touch visible floor: "+station.name);
                foreach(var bath in station.baths)Check(Mathf.Abs(BoundsOf(bath.Find("Imported Box vessel")).min.y-floor)<.006f,"Finishing vessel touches floor");
                Check(Mathf.Abs(station.pumpPlate.GetComponent<Renderer>().bounds.min.y-floor)<.003f,"Pump remains above floor");
                var support=station.transform.Find("Conveyor floor stand");
                Check(support && Mathf.Abs(BoundsOf(support).min.y-floor)<.003f,"Conveyor is supported from floor");
                var board=station.transform.Find("Workstation backboard").GetComponent<Renderer>();
                Check(board.sharedMaterials.All(m=>AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))==FactoryWallsBoxInstaller.BackboardTexture && m.GetColor("_BaseColor")==Color.white),"Reserved plain texture replaces blue on workstation backboard");
            }
            bool reservedOnlyOnBoards=true;
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.name!="Workstation backboard")
                    foreach(var material in renderer.sharedMaterials)
                        if(material && material.HasProperty("_BaseMap"))
                        {
                            var texture=material.GetTexture("_BaseMap");string path=AssetDatabase.GetAssetPath(texture);
                            reservedOnlyOnBoards &= path!=FactoryWallsBoxInstaller.BackboardTexture;
                            if(renderer.sharedMaterial.name.StartsWith("CardboardWall_"))used.Add(texture);
                        }
            Check(reservedOnlyOnBoards,"Reserved backboard texture is absent from all other surfaces");
            Check(used.Count==8,"All five illustrated and three new plain textures are used on walls");
            Check(Mathf.Abs(BoundsOf(game.instruction.sheet).min.y-floor)<.003f,"Instruction rests on floor");
        }
        static void Manufacture(CardboardFactoryGame game)
        {
            game.ResetShift();var station=game.station;
            Check(station.Operate(StationPart.Feed),"Lowered workstation accepts material");station.Tick(1.1f);station.Tick(2.3f);
            Check(station.Stage==StationStage.Loaded,"Conveyor still delivers blank to aligned cutting bed");
            Check(station.Operate(StationPart.Press),"Press lever operates");station.Tick(1.1f);
            Check(station.Operate(StationPart.Guard),"Guard can be raised");station.Tick(.7f);
            for(int i=0;i<4;i++)Check(station.TryPump(new Vector3(station.pumpPlate.position.x,0,station.pumpPlate.position.z)),"Ground-height landing pumps pressure");
            Check(station.PressureReady && station.TurnWheel(1080) && station.Stage==StationStage.Cut,"Gear can be cut after four jumps");
            station.Tick(.7f);Check(station.Operate(StationPart.Press),"Press can be released after cutting");station.Tick(1.1f);
            Check(station.Operate(StationPart.Output) && game.HasPart,"Finished blank can be picked up");
            Check(station.Operate(StationPart.Bath,1),"New grounded vessel accepts gear");station.Tick(1.2f);
            Check(game.HeldRecipe.finish==1 && game.TryInstall(game.gears[0]) && game.Mistakes==0,"Complete manufacture, dipping and installation works after grounding");
            game.ResetShift();
        }
        static void Tick()
        {
            if(!running || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            var game=CardboardFactoryGame.Instance;if(!game || !CardboardRunner.Player || game.MenuActive || Time.time<.2f)return;
            bool done=false;
            try
            {
                if(stage==0){Surfaces(game);Doors(game);Manufacture(game);FactoryLunchCrowdVerification.Reset();stage=1;}
                else done=FactoryLunchCrowdVerification.Tick(game,Check);
            }
            catch(Exception error){report.Add("FAIL "+error);done=true;}
            if(!done)return;
            running=false;File.WriteAllLines("Library/FactoryEnvironmentVerification.txt",report);
            game.ResetShift();Application.runInBackground=oldBackground;EditorApplication.isPlaying=false;
        }
    }
}
