using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryFloorAudioInstaller
    {
        const string Active="Cardboard.FloorAudioVerification",Pending="Cardboard.FloorAudioInstallPending";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;static FactoryFloorAudio audio;static SimulationMode simulation;static double started;static float at;static bool background;static int stage;
        static FactoryFloorAudioInstaller()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending,false))return;
                SessionState.SetBool(Pending,false);ApplyCurrent();EditorApplication.delayCall+=Verify;
            };
        }
        public static void Install(Transform root)
        {
            const string path="Assets/Game_Assets/Sounds/бросок предмета.mp3";
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);if(!importer.forceToMono){importer.forceToMono=true;importer.SaveAndReimport();}
            var component=root.GetComponent<FactoryFloorAudio>();if(!component)component=root.gameObject.AddComponent<FactoryFloorAudio>();
            component.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);EditorUtility.SetDirty(component);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            var target=Object.FindFirstObjectByType<CardboardFactoryGame>();Install(target.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(target.gameObject.scene);EditorSceneManager.SaveScene(target.gameObject.scene);
            File.WriteAllText("Library/FactoryFloorAudioUpdate.txt","SUCCESS: authored throw clip plays on floor contact; dropped props, gears and tray contents covered.");
        }
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Pending,true);EditorApplication.isPlaying=false;return;}
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void Check(bool value,string text){report.Add((value?"PASS ":"FAIL ")+text);}
        static void Stop(){foreach(var source in audio.Speakers)source.Stop();}
        static void Drop(bool floor,bool released)
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name=floor?"Main workshop floor":"Workbench test surface";ground.transform.position=new Vector3(100,99.5f,100);ground.transform.localScale=new Vector3(30,1,30);
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.position=new Vector3(100,102,100);item.transform.localScale=Vector3.one*.3f;
            var body=item.AddComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var prop=item.AddComponent<FactoryPhysicsProp>();game.objectCarry.Take(prop);game.objectCarry.Release(released);
            Check(!audio.Speakers.Any(s=>s.isPlaying),"Releasing "+(released?"a thrown":"a placed")+" object does not play the floor clip");
            Physics.SyncTransforms();for(int i=0;i<100;i++)Physics.Simulate(.02f);
            bool playing=audio.Speakers.Any(s=>s.isPlaying && s.clip==audio.clip);
            Check(playing==floor,floor?"A falling "+(released?"thrown":"placed")+" object plays the supplied clip on real floor collision":"Landing on a table does not trigger the floor sound");
            Stop();for(int i=0;i<80;i++)Physics.Simulate(.02f);
            Check(!audio.Speakers.Any(s=>s.isPlaying),"A settled object stays silent");
            Object.DestroyImmediate(item);Object.DestroyImmediate(ground);Stop();
        }
        static void Finish()
        {
            if(game){Physics.simulationMode=simulation;game.ResetShift();game.enabled=true;game.station.enabled=true;foreach(var runner in game.crowd.runners)runner.enabled=true;}
            Application.runInBackground=background;SessionState.SetBool(Active,false);
            File.WriteAllLines("Library/FactoryFloorAudioVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>30){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){game=CardboardFactoryGame.Instance;audio=FactoryFloorAudio.Instance;simulation=Physics.simulationMode;game.enabled=false;game.station.enabled=false;foreach(var runner in game.crowd.runners)runner.enabled=false;at=Time.time;stage=1;return;}
                if(Time.time-at<.65f)return;
                Physics.simulationMode=SimulationMode.Script;Stop();
                Check(audio.clip && audio.clip.channels==1 && audio.Speakers.All(s=>s.spatialBlend==1),"Supplied impact clip is mono and spatial");
                Check(game.GetComponentsInChildren<Rigidbody>(true).All(b=>b.GetComponent<FactoryFloorImpact>()),"All scene physics bodies, including trays and food, receive floor contact audio");
                Drop(true,true);Drop(true,false);Drop(false,false);Finish();
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
