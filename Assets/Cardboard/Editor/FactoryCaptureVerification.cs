using System;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryCaptureVerification
    {
        static void Load(FactoryWorkstation station)
        {
            station.Operate(StationPart.Feed);station.Tick(.4f);station.Tick(.65f);station.Tick(2.3f);
            station.Operate(StationPart.Press);station.Tick(1.1f);
            station.Operate(StationPart.Guard);station.Tick(.7f);
        }
        static void Pump(FactoryWorkstation station){for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);}
        static void Until(FactoryCaptureSequence sequence,CapturePhase phase)
        {
            for(int i=0;i<1500 && sequence.Phase!=phase && sequence.Phase!=CapturePhase.Restarting;i++)sequence.Tick(.05f);
            if(sequence.Phase!=phase)throw new InvalidOperationException("Capture did not reach "+phase+"; at "+sequence.Phase);
        }
        static void Frame(FactoryCaptureSequence sequence,string name)
        {
            Camera.main.transform.SetPositionAndRotation(sequence.CameraPosition,sequence.CameraRotation);
            Canvas.ForceUpdateCanvases();CardboardFactorySceneBuilder.Capture(name);
        }
        public static void Check(CardboardFactoryGame game,Action<bool,string> check)
        {
            var station=game.station;var sequence=game.captureSequence;
            game.ResetShift();Load(station);
            station.BeginWheel(new Vector2(200,200));station.TurnWheel(30);station.TurnWheel(30);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("давление") && station.CutProgress==0,"Starting below full pressure awards one explained sequence error per grip");
            station.EndGrip(false);Pump(station);station.TurnWheel(30);
            check(game.Mistakes==1 && station.CutProgress>0,"Filling pressure remedies the sequence error without refunding its star");
            game.ResetShift();station.Operate(StationPart.Selector);Load(station);Pump(station);
            station.TurnWheel(30);station.TurnWheel(30);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("перфокарте") && station.Stage==StationStage.Cutting,"Cutting wrong selected gear awards a star at the first rotation");
            station.BeginWheel(Vector2.zero);station.TurnWheel(30);station.EndGrip(false);
            check(game.Mistakes==1,"Continuing the same wrong blank cannot generate a star on every turn");
            game.ResetShift();station.BeginWheel(Vector2.zero);station.TurnWheel(30);station.TurnWheel(30);station.EndGrip(false);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("заготовки"),"Turning before feeding a blank is an explained order error");
            game.ResetShift();
            sequence.AutomaticRestart=false;
            var player=CardboardRunner.Player;
            Vector3 before=player.transform.position;
            game.RegisterMistake("Ошибка 1");game.RegisterMistake("Ошибка 2");game.RegisterMistake("Ошибка 3");
            check(game.State==FactoryState.Chased && !game.CameraOverride && !game.PlayerBlocked && player.transform.position==before,"Third error starts pursuit without teleporting or taking controls");
            check(sequence.workers.Length==game.crowd.runners.Count-1,"All cardboard workers join capture");
            foreach(var worker in sequence.workers)
            {
                game.WorkerTarget(worker,out float speed);
                check(speed>6,"Every worker runs after player: "+worker.name);
            }
            var hunter=sequence.workers[0];var body=hunter.GetComponent<CharacterController>();
            body.enabled=false;hunter.transform.position=player.transform.position+Vector3.right*.8f;body.enabled=true;Physics.SyncTransforms();
            Vector3 cameraBefore=Camera.main.transform.position;
            sequence.Tick(.01f);
            check(Vector3.Distance(sequence.CameraPosition,cameraBefore)<.001f,"Grab takes over exactly at current first-person view without a one-frame teleport");
            check(sequence.Phase==CapturePhase.Lifting && game.CameraOverride && game.PlayerBlocked,"Camera is taken only when a worker physically reaches player");
            check(!player.GetComponent<CharacterController>().enabled && station.ExecutionControlled,"Lift disables locomotion and hands station over to execution");
            Until(sequence,CapturePhase.Carrying);sequence.Tick(.2f);
            Vector3 first=sequence.CameraPosition;Quaternion rotation=sequence.CameraRotation;sequence.Tick(.13f);
            check(Vector3.Dot(sequence.CameraRotation*Vector3.forward,Vector3.up)>.6f,"Carried first-person view tilts upward with the workers and ceiling in view");
            check(Vector3.Distance(first,sequence.CameraPosition)>.01f && Quaternion.Angle(rotation,sequence.CameraRotation)>.1f,"Carried camera moves and sways with footsteps");
            Camera.main.transform.SetPositionAndRotation(sequence.CameraPosition,sequence.CameraRotation);
            foreach(int index in new[]{sequence.CarrierLeft,sequence.CarrierRight})
            {
                var worker=sequence.workers[index];
                Vector3 face=Camera.main.WorldToViewportPoint(worker.transform.position+Vector3.up*1.85f);
                check(face.z>0 && face.x>.08f && face.x<.92f && face.y>.08f && face.y<.92f,"Carrier face remains in the first-person frame: "+worker.name);
            }
            bool noAddedArms=true;
            foreach(var worker in sequence.workers)
                noAddedArms &= !worker.transform.Find("Capture • articulated cardboard arms");
            check(noAddedArms,"Capture uses the original cardboard people without added arms or hands");
            Frame(sequence,"CapturedCarryWorkers");
            Until(sequence,CapturePhase.Lever);Quaternion leverHome=station.pressLever.localRotation;sequence.Tick(1.0f);
            check(Vector3.Distance(sequence.CameraPosition,sequence.BedPosition)<.001f,"First-person head lies at the actual workstation cutting point");
            check(Vector3.Distance(sequence.workers[sequence.LeverWorker].transform.position,station.transform.TransformPoint(new Vector3(.72f,0,-1.35f)))<.01f && Quaternion.Angle(leverHome,station.pressLever.localRotation)>20,"A cardboard worker stands beside the animated press lever");
            Frame(sequence,"CapturedWorkerLever");
            float open=station.press.position.y;Until(sequence,CapturePhase.Crushing);sequence.Tick(.55f);
            check(station.press.position.y<open-.3f,"Actual station press descends toward player's head");
            check(Vector3.Dot(sequence.CameraRotation*Vector3.forward,Vector3.up)>.99f,"Crushing remains first person directly beneath press");
            Frame(sequence,"CapturedPressDescending");
            Until(sequence,CapturePhase.Grade);
            check(game.State==FactoryState.Dead && sequence.grade.gameObject.activeSelf && sequence.grade.wordOpacity.alpha==0,"Press contact abruptly blacks out the scene before grade appears");
            sequence.Tick(.1f);
            check(sequence.grade.wordOpacity.alpha>0 && sequence.grade.wordOpacity.alpha<1 && sequence.grade.word.localScale.x>2,"Red grade flies away from camera with increasing opacity");
            check(sequence.grade.lettering.text=="ЛОХ" && sequence.grade.lettering.color.r>.8f && sequence.grade.lettering.color.g<.1f,"Death grade is the requested red word in shared font");
            sequence.Tick(.75f);
            check(sequence.grade.pin.gameObject.activeSelf && sequence.grade.pinOpacity.alpha==1 && sequence.grade.pin.localScale==Vector3.one,"Cardboard pin flies in and nails the grade");
            Frame(sequence,"PinnedDeathGrade");
            sequence.Tick(1.9f);
            check(!sequence.RestartRequested,"Pinned grade holds for a full two seconds");
            sequence.Tick(.1f);
            check(sequence.RestartRequested && sequence.Phase==CapturePhase.Restarting,"Full restart is requested two seconds after pin impact");
            game.ResetShift();sequence.AutomaticRestart=true;
            check(!game.CaptureActive && !sequence.grade.gameObject.activeSelf && player.GetComponent<CharacterController>().enabled && !station.ExecutionControlled,"New shift restores controls, station and clear screen");
            var path=new FactoryCaptureNavigation(.78f).Path(new Vector3(-13,0,-23),station.transform.TransformPoint(new Vector3(0,0,-1.8f)));
            check(path.Count>3,"Carrier route reaches real station from canteen through doorway");
        }
    }
}
