using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryReactionPresentationVerification
    {
        const string Active="Cardboard.ReactionPresentationVerification";
        static readonly List<string> report=new List<string>();
        static int stage;static double began;static float at;static bool background;
        static CardboardFactoryGame game;static FactoryMistakeHUD hud;static FactoryWorkerImpact reaction;
        static FactoryReactionPresentationVerification(){EditorApplication.update+=Tick;}
        static void Check(bool value,string text){report.Add((value?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactoryReactionPresentationVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;began=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Finish()
        {
            if(game)
            {
                game.ResetShift();game.enabled=true;game.station.enabled=true;game.mainMenu.enabled=true;
                foreach(var worker in game.crowd.runners)worker.enabled=true;
                var view=Camera.main?Camera.main.GetComponent<CardboardCamera>():null;if(view)view.enabled=true;
            }
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void SetScore(float count)
        {game.ResetShift();if(count>0)game.RegisterMistake("Проверка интерфейса",count);hud.Refresh();Canvas.ForceUpdateCanvases();}
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;game.enabled=false;game.station.enabled=false;
            typeof(FactoryMainMenu).GetMethod("Close",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.mainMenu,null);game.mainMenu.enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;
            var view=Camera.main.GetComponent<CardboardCamera>();if(view)view.enabled=false;
            hud=game.GetComponentInChildren<FactoryMistakeHUD>();
            Check(hud.UsesFaces && hud.stars.Length==3 && hud.fills.Length==3,"Three imported suspicion faces replace all three stars");
            Check(hud.normalFace.rect==hud.angryFace.rect && hud.normalFace.rect.width<700,"Both art variants share a trimmed and aligned transparent rectangle");
            foreach(float count in new[]{0,.5f,1,1.5f,2.5f,3})
            {
                SetScore(count);bool correct=true;
                for(int i=0;i<3;i++)
                {
                    float fill=Mathf.Clamp01(count-i);var sprite=fill>=.999f?hud.angryFace:hud.normalFace;
                    correct &= hud.stars[i].sprite==sprite && hud.fills[i].sprite==sprite && Mathf.Abs(hud.fills[i].fillAmount-fill)<.001f;
                }
                Check(correct,"Correct normal/angry artwork and horizontal fill at score "+count);
            }
            Check(hud.stars.All(i=>i.preserveAspect && i.color==new Color(.43f,.40f,.34f,.65f) && i.GetComponent<Shadow>()),"Original dimming, proportions and drop shadows remain on the new art");
            Check(hud.stars.All(i=>i.GetComponentsInChildren<Image>(true).Length==2),"Each slot has one dim base and one bright overlay, without duplicate star graphics");
            game.ResetShift();
            foreach(var worker in game.crowd.runners.Where(w=>!w.isPlayer))
            {
                var r=worker.GetComponent<FactoryWorkerImpact>();
                Check(r.voice.spatialBlend==1 && r.voice.transform.parent==worker.transform && r.voice.transform.localPosition.y==1.6f && r.voice.rolloffMode==AudioRolloffMode.Logarithmic,"Voice is spatial and follows the worker's chest: "+worker.name);
                Check(r.voiceVolume==.55f && r.voices.All(c=>c.channels==1) && r.reactionIcons.Length==5,"Worker has quieter mono voices and all five message reactions: "+worker.name);
            }
            reaction=game.crowd.runners.First(w=>!w.isPlayer).GetComponent<FactoryWorkerImpact>();
            foreach(var type in new[]{FactoryWorkerEmotion.Machine,FactoryWorkerEmotion.Jump,FactoryWorkerEmotion.Run})
            {
                game.ResetShift();game.RegisterMistake("Проверка эмоции",.5f,reaction.GetComponent<CardboardRunner>(),type);
                int index=type==FactoryWorkerEmotion.Machine?0:type==FactoryWorkerEmotion.Jump?1:2;
                Check(reaction.Emotion==type && reaction.EmotionVariant==index && reaction.reactionIcons.Count(t=>t.gameObject.activeSelf)==1 && reaction.reactionIcons[index].gameObject.activeSelf,"Violation selects the matching message illustration: "+type);
            }
            var variants=new HashSet<int>();bool throwOnly=true;
            for(int i=0;i<64;i++){reaction.ReceiveImpact(Vector3.forward,1);variants.Add(reaction.EmotionVariant);throwOnly &= reaction.Emotion==FactoryWorkerEmotion.Thrown && reaction.EmotionVariant>=3 && reaction.EmotionVariant<=4;}
            Check(throwOnly && variants.SetEquals(new[]{3,4}),"Thrown-object hits randomly use both Throw and Throw_2, and no unrelated illustration");
            Check(reaction.reactionIcons.All(t=>t.GetComponent<Renderer>().sharedMaterial.GetFloat("_Cull")==0),"Message illustrations are double sided");
            game.ResetShift();reaction.Notice();float now=Time.time;
            Check(reaction.voice.isPlaying && reaction.voice.volume==0,"Reaction voice starts silently");
            reaction.TickVoice(now+.1f,.1f,true);float attack=reaction.voice.volume;
            Check(attack>.1f && attack<.45f,"Voice fades in during the first 0.2 seconds");
            reaction.TickVoice(now+.3f,.2f,true);Check(Mathf.Abs(reaction.voice.volume-.55f)<.001f,"Sustained voice is quieter than the former 0.8 volume");
            reaction.TickVoice(now+2.5f,.1f,true);float release=reaction.voice.volume;
            Check(release>.1f && release<.45f,"Voice fades out during the last 0.4 seconds");
            reaction.TickVoice(now+2.69f,.1f,true);Check(reaction.voice.volume<.003f,"Voice reaches almost silence before the message disappears");
            float before=reaction.voice.volume;reaction.Notice();Check(Mathf.Abs(reaction.voice.volume-before)<.001f,"A repeated notice does not jump the current voice gain");
            reaction.TickVoice(Time.time+.3f,.3f,true);reaction.TickVoice(Time.time+.3f,.2f,false);Check(reaction.voice.volume>.1f && reaction.voice.volume<.45f,"Hiding a message also fades its voice without a hard mute");
            SetScore(2.5f);reaction.Notice();
            var workerPosition=reaction.transform.position;Camera.main.transform.position=workerPosition+new Vector3(1.4f,1.8f,-3.8f);
            Camera.main.transform.rotation=Quaternion.LookRotation(workerPosition+Vector3.up*1.65f-Camera.main.transform.position);at=Time.time;
        }
        static void Capture(string name)
        {reaction.TickReaction(.05f);reaction.SendMessage("LateUpdate");CardboardFactorySceneBuilder.Capture(name);}
        static void ThrowVariant(int index)
        {for(int i=0;i<64;i++){reaction.Notice(2.7f,FactoryWorkerEmotion.Thrown);if(reaction.EmotionVariant==index)break;}}
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-began>45){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;Save();return;}
                if(Time.time-at<.2f)return;
                if(stage==1){Capture("ReactionMachineAndSuspicionFaces");reaction.Notice(2.7f,FactoryWorkerEmotion.Jump);stage=2;at=Time.time;return;}
                if(stage==2){Capture("ReactionJump");reaction.Notice(2.7f,FactoryWorkerEmotion.Run);stage=3;at=Time.time;return;}
                if(stage==3){Capture("ReactionRun");ThrowVariant(3);stage=4;at=Time.time;return;}
                if(stage==4){Capture("ReactionThrow");ThrowVariant(4);stage=5;at=Time.time;return;}
                if(stage==5){Capture("ReactionThrow2");stage=6;return;}
                if(stage==6 && Time.time-at>2.9f)
                {
                    Check(!reaction.Reacting && !reaction.message.gameObject.activeSelf && !reaction.voice.isPlaying,"Message expiration stops the faded voice and clears the reaction");Finish();
                }
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}
