using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryHeldGearVerification
    {
        static readonly List<string> report=new List<string>();
        static bool running,background;
        static double started;
        static int stage,frame;
        static FactoryHeldGearVerification(){EditorApplication.update+=Update;}
        static void Check(bool value,string message){report.Add((value?"PASS ":"FAIL ")+message);}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();running=true;stage=frame=0;started=EditorApplication.timeSinceStartup;
            background=Application.runInBackground;Application.runInBackground=true;
            FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Finish()
        {
            running=false;Application.runInBackground=background;
            File.WriteAllLines("Library/FactoryHeldGearVerification.txt",new[]{"RESULT: "+report.Count(line=>line.StartsWith("PASS"))+" PASS, "+report.Count(line=>line.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
        static void Motion(FactoryHeldGearMotion motion,FactoryInventorySlot slot)
        {
            var home=motion.transform.localPosition;var rotation=motion.transform.localRotation;
            motion.ResetInertia();slot.ResetInertia();
            for(int i=0;i<60;i++)
            {
                var speed=new Vector3(3.2f,0,6);var turn=new Vector3(0,120,0);
                motion.TickInertia(1f/60,speed,turn,Vector3.zero);
                slot.TickInertia(1f/60,speed,turn,Vector3.zero);
            }
            Check(motion.InertialOffset.x<0 && motion.InertialOffset.z<0 && motion.InertialTilt.y<0,"Gear trails right turns and forward movement in the correct direction");
            Check(motion.InertialOffset.magnitude>slot.InertialOffset.magnitude*5.4f &&
                motion.InertialTilt.magnitude>slot.InertialTilt.magnitude*5.4f,"Held gear inertia is about six times as strong as the current inventory inertia");
            Check(Vector3.Distance(motion.transform.localPosition-home,motion.InertialOffset)<.00001f &&
                Quaternion.Angle(rotation,motion.transform.localRotation)>.5f,"Inertia moves and tilts the actual held model");
            var previous=motion.InertialOffset;
            motion.TickInertia(1f/60,Vector3.zero,Vector3.zero,Vector3.zero);
            Check(motion.InertialOffset.magnitude>previous.magnitude*.9f,"Stopping retains momentum instead of snapping to the default pose");
            for(int i=0;i<180;i++)motion.TickInertia(1f/60,Vector3.zero,Vector3.zero,Vector3.zero);
            Check(Vector3.Distance(home,motion.transform.localPosition)<.00001f &&
                Quaternion.Angle(rotation,motion.transform.localRotation)<.01f,"Gear smoothly returns to its original hand position and angle");
            motion.ResetInertia();
            for(int i=0;i<60;i++)motion.TickInertia(1f/60,Vector3.right*3.2f,Vector3.up*120,Vector3.zero);
            var at60=motion.InertialOffset;motion.ResetInertia();
            for(int i=0;i<144;i++)motion.TickInertia(1f/144,Vector3.right*3.2f,Vector3.up*120,Vector3.zero);
            Check(Vector3.Distance(at60,motion.InertialOffset)<.0001f,"Motion remains consistent at 60 and 144 FPS");
            for(int i=0;i<60;i++)motion.TickInertia(.05f,Vector3.one*100,Vector3.one*10000,Vector3.one);
            Check(motion.InertialOffset.magnitude<.102f && motion.InertialTilt.magnitude<18,"Abrupt movement stays bounded without large swings");
            var camera=Camera.main.transform;var position=camera.position;var facing=camera.rotation;
            motion.ResetInertia();motion.RefreshPose(1f/60);
            for(int i=1;i<=20;i++)
            {
                camera.SetPositionAndRotation(position+facing*Vector3.right*(i*.02f),facing*Quaternion.Euler(0,i*2,0));
                motion.RefreshPose(1f/60);
            }
            Check(motion.InertialOffset.x<-.005f && motion.InertialTilt.y<-.5f,"Real camera translation and rotation drive the same hand inertia");
            camera.position+=Vector3.one*10;motion.RefreshPose(1f/60);
            Check(motion.InertialOffset==Vector3.zero && motion.InertialTilt==Vector3.zero,"Teleporting resets inertia without a pickup or camera jump");
            camera.SetPositionAndRotation(position,facing);motion.ResetInertia();slot.ResetInertia();
        }
        static void Lifecycle(CardboardFactoryGame game)
        {
            var camera=Camera.main;game.enabled=false;camera.GetComponent<CardboardCamera>().enabled=false;
            var slot=camera.GetComponent<FactoryInventorySlot>();
            for(int type=1;type<=3;type++)
            {
                game.ReceiveWorkbenchPart(FactoryRecipe.ForType(type,false));
                var motion=camera.GetComponentInChildren<FactoryHeldGearMotion>();
                Check(motion && game.HasPart && game.HeldRecipe.Type==type,"Authored gear "+type+" receives inertia automatically on pickup");
                Check(motion.transform.localScale==Vector3.one*.24f && !motion.GetComponentInChildren<Collider>(),
                    "Gear "+type+" retains its original size and does not intercept interactions");
                if(type==1)Motion(motion,slot);
                motion.TickInertia(.05f,Vector3.one*6,Vector3.up*180,Vector3.zero);
                Check(game.BeginDip() && !motion.gameObject.activeSelf && motion.InertialOffset==Vector3.zero,
                    "Dipping gear "+type+" hides the model and clears its momentum");
                game.FinishDip(type);
                Check(motion.gameObject.activeSelf && motion.InertialOffset==Vector3.zero && game.HeldRecipe.finish==type,
                    "Finished gear "+type+" returns to the hand without a positional jump");
                Check(game.TryInstall(game.gears[type-1]) && !motion.gameObject.activeSelf && !game.HasPart,
                    "Gear "+type+" still installs into the correct mechanism and leaves the hand");
            }
            Check(game.Mistakes==0 && game.ReadyToLeave,"Inertia does not change the three-order production flow");
            game.ResetShift();game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));
            var carried=camera.GetComponentInChildren<FactoryHeldGearMotion>();
            carried.TickInertia(.05f,Vector3.one*6,Vector3.up*180,Vector3.zero);
            game.DropPart();Check(!carried.gameObject.activeSelf && carried.InertialOffset==Vector3.zero && !game.HasPart,"Dropping a gear removes it and clears inertia");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));
            var preview=camera.GetComponentInChildren<FactoryHeldGearMotion>();
            var eye=game.station.transform.TransformPoint(new Vector3(-1,1.9f,-5));
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.35f,0))-eye));
            preview.ResetInertia();slot.Pose(1,true,false);
        }
        static void Update()
        {
            if(!running)return;
            try
            {
                if(EditorApplication.timeSinceStartup-started>30){Check(false,"Verification timed out");Finish();return;}
                if(!EditorApplication.isPlaying || frame==Time.frameCount)return;frame=Time.frameCount;
                var game=CardboardFactoryGame.Instance;if(!game || game.CurrentOrder!=1)return;
                if(stage==0){Lifecycle(game);stage=1;return;}
                if(stage++<3)return;
                var motion=Camera.main.GetComponentInChildren<FactoryHeldGearMotion>();
                CardboardFactorySceneBuilder.Capture("HeldGearInertiaIdle");
                for(int i=0;i<30;i++)motion.TickInertia(1f/60,Vector3.right*3.2f,Vector3.up*180,Vector3.zero);
                CardboardFactorySceneBuilder.Capture("HeldGearInertiaTurning");
                game.Capture(FactoryDeath.Press);
                Check(!motion.gameObject.activeSelf && motion.InertialOffset==Vector3.zero,"Capture hides the held gear and clears its momentum");
                game.ResetShift();
                Check(!Camera.main.GetComponentInChildren<FactoryHeldGearMotion>() && !game.HasPart,"A new shift clears all carried gear motion");
                Finish();
            }
            catch(Exception error){Check(false,error.ToString());Finish();Debug.LogException(error);}
        }
    }
}
