using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryAudioVerification
    {
        const string Active="Cardboard.AudioVerification";
        static readonly List<string> lines=new List<string>();
        static CardboardFactoryGame game;static FactoryWorkstation station;static FactoryWorkbenchSound sound;
        static FactoryPlayerFootsteps steps;static CardboardRunner player;
        static Vector3 home;static Quaternion rotation;static float at;static int stage;static double started;static bool background;
        static FactoryAudioVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);}
        static void Save(){File.WriteAllLines("Library/FactoryAudioVerification.txt",new[]{"RESULT: "+lines.Count(l=>l.StartsWith("PASS"))+" PASS, "+lines.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(lines));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            lines.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void Move(Vector3 position)
        {var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.position=position;body.enabled=true;steps.TickSteps();}
        static void MachineTime(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)station.Tick(.02f);}
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;station=game.station;sound=station.GetComponent<FactoryWorkbenchSound>();player=CardboardRunner.Player;steps=player.GetComponent<FactoryPlayerFootsteps>();
            home=player.transform.position;rotation=player.transform.rotation;
            game.ResetShift();game.enabled=false;station.enabled=false;game.mainMenu.enabled=false;steps.enabled=false;
            foreach(var runner in game.crowd.runners)runner.enabled=false;
            Check(Mathf.Approximately(station.printerSignal.speaker.volume,.35f),"Printer ring is reduced by exactly 30 percent from 0.5 to 0.35");
            Check(steps && steps.clips.Length==4 && steps.clips.All(c=>c && c.channels==1),"Four supplied mono player footstep variations are assigned");
            Check(steps.speaker.spatialBlend==1 && steps.speaker.volume==.30f && !steps.speaker.playOnAwake,"Footsteps use a quiet spatial source at the player");
            var ambient=game.GetComponentInChildren<FactoryMechanismAudio>();
            Check(ambient.speakers.Length==4 && ambient.speakers.All(s=>s.clip && s.clip.channels==1 && s.spatialBlend==1 && s.loop && s.dopplerLevel==0),"All four retained mechanism loops have spatial mono sources");
            Check(ambient.speakers.Select(s=>s.transform.position).Distinct().Count()==4 && ambient.speakers.Select(s=>s.clip).Distinct().Count()==4,"Mechanism emitters occupy four different gear locations and use four distinct clips");
            Check(sound.cuttingClips.Length==2 && sound.cuttingClips[0]!=sound.cuttingClips[1] && sound.cuttingFirst.spatialBlend==1 && sound.cuttingSecond.spatialBlend==1,"Both authored cardboard cutting clips have separate spatial sources");
            Check(sound.leverClip && sound.leverClip.name=="РЫчаг" && sound.leverSpeaker.spatialBlend==1,"Authored lever clip has a spatial source");
            Check(FactoryAudioInstaller.ApplePose(game.lunch)==File.ReadAllText("Library/FactoryAudioApplePose.txt"),"User-edited apple root, model position, rotation and scale survive a shift reset");
            game.lunch.TickSchedule(game.lunch.lunchAfter+10);
            Check(game.lunch.State==LunchState.Waiting && !game.lunch.TryTake() && !game.lunch.diners.Active,"An expired lunch timer cannot start lunch before the first manufactured repair");
            at=Time.time;
        }
        static void StartCut()
        {
            int count=steps.StepsPlayed;
            for(int i=0;i<5;i++)Move(player.transform.position+Vector3.right*.4f);
            Check(steps.StepsPlayed>count && steps.speaker.isPlaying,"Grounded player displacement produces audible footsteps");
            count=steps.StepsPlayed;for(int i=0;i<10;i++)steps.TickSteps();
            Check(steps.StepsPlayed==count,"Standing still produces no footsteps");
            var jump=typeof(CardboardRunner).GetField("jumpHeight",BindingFlags.Instance|BindingFlags.NonPublic);jump.SetValue(player,.5f);
            for(int i=0;i<5;i++)Move(player.transform.position+Vector3.left*.4f);
            Check(steps.StepsPlayed==count,"Airborne movement produces no footsteps");jump.SetValue(player,0f);
            while(station.SelectedType!=game.OrderedType)station.Operate(StationPart.Selector);
            Check(station.Operate(StationPart.Feed) && sound.leverSpeaker.isPlaying,"Accepted feed lever movement plays its authored clip immediately");
            for(int i=0;i<5;i++)Move(player.transform.position+Vector3.right*.4f);
            Check(steps.StepsPlayed==count,"Locked machine controls produce no player footsteps");
            MachineTime(4);Check(station.Operate(StationPart.Press),"Loaded sheet accepts the press lever");MachineTime(1.2f);
            Check(station.Operate(StationPart.Guard),"Guard is raised after the press closes");MachineTime(.8f);
            for(int i=0;i<4;i++)station.TryPump(station.pumpPlate.position);
            Check(station.CanCut && station.TurnWheel(1),"Full-pressure machine begins a valid cut");
            Check(sound.cuttingFirst.isPlaying && !sound.cuttingSecond.isPlaying,"First cutting layer starts immediately; second layer waits for its offset");
            at=Time.time;
        }
        static void CompleteRepair()
        {
            station.EndGrip(false);station.TurnWheel(2000);
            Check(station.Stage==StationStage.Cut,"Wheel completes the manufactured gear");
            station.Operate(StationPart.Guard);MachineTime(.8f);station.Operate(StationPart.Press);MachineTime(1.2f);
            Check(station.Operate(StationPart.Output) && game.HasPart,"Manufactured gear can be collected");
            int bath=game.OrderedType;Check(station.Operate(StationPart.Bath,bath),"Manufactured gear enters its correct finishing bath");MachineTime(1.6f);
            Check(game.TryInstall(game.Memorized) && game.Repairs==1,"First manufactured and finished gear installs successfully");
            game.lunch.TickSchedule(0);
            Check(game.lunch.State==LunchState.Ready,"First manufactured repair releases the pending lunch bell");
        }
        static void Finish()
        {
            if(game)
            {
                game.ResetShift();Move(home);player.transform.rotation=rotation;steps.enabled=true;game.enabled=true;station.enabled=true;game.mainMenu.enabled=true;
                foreach(var runner in game.crowd.runners)runner.enabled=true;
            }
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>35){lines.Add("FAIL audio verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;return;}
                if(Time.time-at<.1f)return;
                if(stage==1){StartCut();stage=2;return;}
                if(stage==2)
                {
                    station.TurnWheel(1);if(Time.time-at<.24f)return;
                    Check(sound.cuttingFirst.isPlaying && sound.cuttingSecond.isPlaying && sound.cuttingSecond.volume>0,"Both cutting layers overlap after a 120 ms stagger while the wheel advances");
                    at=Time.time;stage=3;return;
                }
                if(stage==3)
                {
                    if(Time.time-at<.5f)return;
                    Check(!sound.cuttingFirst.isPlaying && !sound.cuttingSecond.isPlaying,"Cutting layers fade out when the wheel stops advancing");
                    Check(station.BeginWheel(Vector2.zero),"The player can hold the wheel without turning it");at=Time.time;stage=4;return;
                }
                if(stage==4)
                {
                    if(Time.time-at<.3f)return;
                    Check(!sound.cuttingFirst.isPlaying && !sound.cuttingSecond.isPlaying,"A stationary held wheel stays silent");
                    CompleteRepair();station.ResetMachine();station.TurnWheel(20);
                    Check(!sound.cuttingFirst.isPlaying && !sound.cuttingSecond.isPlaying,"Turning an empty machine cannot start cutting audio");
                    Finish();
                }
            }
            catch(Exception error){lines.Add("FAIL "+error);Finish();}
        }
    }
}
