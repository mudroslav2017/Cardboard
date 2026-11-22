using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryLeverVerification
    {
        const string Active="Cardboard.LeverVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;static FactoryWorkstation station;static Camera camera;static Vector3 eye;static Quaternion look;static float at;static int stage;static double started;static bool background;
        static FactoryLeverVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactoryLeverVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static MeshFilter Base(Transform assembly,Transform motion)=>assembly.GetComponentsInChildren<MeshFilter>(true).First(f=>!f.transform.IsChildOf(motion));
        static bool Same(Matrix4x4 a,Matrix4x4 b){for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>.0001f)return false;return true;}
        static void MachineTime(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)station.Tick(.02f);}
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;station=game.station;camera=Camera.main;eye=camera.transform.position;look=camera.transform.rotation;
            game.ResetShift();game.enabled=false;station.enabled=false;game.mainMenu.enabled=false;camera.GetComponent<CardboardCamera>().enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            foreach(var item in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                Check(item.feedHandle && item.pressHandle && item.feedHandle.GetComponentsInChildren<MeshFilter>(true).All(f=>f.name.StartsWith("Sphere")) && item.pressHandle.GetComponentsInChildren<MeshFilter>(true).All(f=>f.name.StartsWith("Sphere")),"Only the rod and ball belong to both moving handles on "+item.name);
                Check(item.feedGrip.IsChildOf(item.feedHandle) && item.pressGrip.IsChildOf(item.pressHandle),"Hand grip points follow the separate moving handles on "+item.name);
                var baseMesh=Base(item.pressLever,item.pressHandle);var before=baseMesh.transform.localToWorldMatrix;float y=item.pressGrip.position.y;
                item.SetExecutionPose(1,0);
                Check(Same(before,baseMesh.transform.localToWorldMatrix) && item.pressGrip.position.y<y-.15f,"Press lever handle lowers while every base mesh vertex stays fixed on "+item.name);
                item.SetExecutionPose(0,0);
            }
            var fixedFeed=Base(station.feedLever,station.feedHandle);var feedMatrix=fixedFeed.transform.localToWorldMatrix;float feedHeight=station.feedGrip.position.y;
            Check(station.Operate(StationPart.Feed),"Feed lever accepts a normal stroke");station.Tick(.175f);
            Check(Same(feedMatrix,fixedFeed.transform.localToWorldMatrix) && station.feedGrip.position.y<feedHeight-.05f && station.operatorHand.Grip==station.feedGrip,"Actual feed stroke moves the circled rod, ball and hand without moving its base");
            camera.transform.position=fixedFeed.GetComponent<Renderer>().bounds.center-station.transform.forward*1.4f+Vector3.up*.5f;
            camera.transform.rotation=Quaternion.LookRotation(station.feedGrip.position-camera.transform.position);at=Time.time;
        }
        static void PressTests()
        {
            var feed=station.feedHandle;station.Tick(.9f);MachineTime(3.5f);
            Check(station.Stage==StationStage.Loaded && Quaternion.Angle(feed.localRotation,Quaternion.identity)<.01f,"Feed stroke returns its handle and delivers a sheet");
            var baseMesh=Base(station.pressLever,station.pressHandle);var matrix=baseMesh.transform.localToWorldMatrix;float height=station.pressGrip.position.y;
            Check(station.Operate(StationPart.Press),"Press can close with a loaded sheet");station.Tick(.175f);
            Check(Same(matrix,baseMesh.transform.localToWorldMatrix) && station.pressGrip.position.y<height-.05f,"Actual press lever stroke leaves its base fixed");MachineTime(1.2f);
            Check(station.PressClosed && station.CutProgress==0 && !station.GuardClosed,"Press is down before cutting while the grille is down");
            Check(station.Operate(StationPart.Press),"Press can immediately reopen before any cutting when the grille is down");MachineTime(1.2f);
            Check(!station.PressClosed && station.Stage==StationStage.Loaded && station.CutProgress==0 && !game.HasPart,"Early reopening raises the press and keeps the uncut sheet loaded");
            station.Operate(StationPart.Press);MachineTime(1.2f);station.Operate(StationPart.Guard);MachineTime(.8f);
            Check(!station.Operate(StationPart.Press),"A raised grille still blocks the press lever");
            station.Operate(StationPart.Guard);MachineTime(.8f);
            Check(station.Operate(StationPart.Press),"After lowering the grille, the press reopens without requiring a completed cut");MachineTime(1.2f);
            station.Operate(StationPart.Press);MachineTime(1.2f);station.Operate(StationPart.Guard);MachineTime(.8f);for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);station.TurnWheel(90);
            Check(station.Stage==StationStage.Cutting && station.CutProgress>0,"Prepared machine starts cutting normally");station.Operate(StationPart.Guard);MachineTime(.8f);
            Check(station.Operate(StationPart.Press),"Press also reopens after lowering the grille during an unfinished cut");MachineTime(1.2f);
            Check(!station.PressClosed && station.Stage==StationStage.Loaded && station.Pressure==0 && station.CutProgress==0,"Reopening an unfinished press restores the previous sheet and pressure reset behavior");
        }
        static void Finish()
        {
            if(game){game.ResetShift();foreach(var item in game.GetComponentsInChildren<FactoryWorkstation>(true))item.ResetMachine();camera.transform.SetPositionAndRotation(eye,look);camera.GetComponent<CardboardCamera>().AdoptCurrentView();camera.GetComponent<CardboardCamera>().enabled=true;game.enabled=true;station.enabled=true;game.mainMenu.enabled=true;foreach(var worker in game.crowd.runners)worker.enabled=true;}
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>35){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try{if(stage==0){Setup();stage=1;return;}if(Time.time-at<.2f)return;CardboardFactorySceneBuilder.Capture("LeverHandleOnly");PressTests();Finish();}
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
