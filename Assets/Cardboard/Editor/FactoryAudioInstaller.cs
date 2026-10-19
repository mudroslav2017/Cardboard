using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryAudioInstaller
    {
        const string Sounds="Assets/Game_Assets/Sounds/";
        static AudioClip Clip(string name)
        {
            string path=Sounds+name+".mp3";
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(!importer)throw new FileNotFoundException(path);
            if(!importer.forceToMono){importer.forceToMono=true;importer.SaveAndReimport();}
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(!clip)throw new FileNotFoundException(path);return clip;
        }
        static AudioSource Speaker(Transform parent,string name,Vector3 position,float volume,float range,bool loop,AudioClip clip=null)
        {
            var child=parent.Find(name);
            if(!child){child=new GameObject(name).transform;child.SetParent(parent,false);}
            child.position=position;
            var source=child.GetComponent<AudioSource>();if(!source)source=child.gameObject.AddComponent<AudioSource>();
            source.clip=clip;source.volume=volume;source.loop=loop;source.playOnAwake=false;
            source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.2f;source.maxDistance=range;
            source.dopplerLevel=0;source.spread=0;source.priority=loop?150:90;return source;
        }
        public static string ApplePose(FactoryLunch lunch)
        {
            var apple=lunch.tray.Find("Lunch apple");
            return string.Join("\n",apple.GetComponentsInChildren<Transform>(true).Select(t=>t.name+" | "+t.localPosition.ToString("F6")+" | "+t.localRotation.ToString("F6")+" | "+t.localScale.ToString("F6")));
        }
        public static void Install(Transform root)
        {
            var game=root.GetComponent<CardboardFactoryGame>();
            var clips=Enumerable.Range(1,4).Select(i=>Clip("Footstep-on-wood-"+i)).ToArray();
            var player=game.crowd.runners.First(r=>r.isPlayer);
            var steps=player.GetComponent<FactoryPlayerFootsteps>();if(!steps)steps=player.gameObject.AddComponent<FactoryPlayerFootsteps>();
            steps.clips=clips;steps.speaker=Speaker(player.transform,"Player footsteps",player.transform.position+Vector3.up*.1f,.30f,8,false);EditorUtility.SetDirty(steps);
            var ambience=root.Find("Spatial mechanism sounds");if(!ambience){ambience=new GameObject("Spatial mechanism sounds").transform;ambience.SetParent(root,false);}
            var audio=ambience.GetComponent<FactoryMechanismAudio>();if(!audio)audio=ambience.gameObject.AddComponent<FactoryMechanismAudio>();
            string[] names={"большой механизм","большой механизм.mp3","большой механизм3","механищм"};
            var gears=game.gears.OrderBy(g=>g.transform.position.x).ThenBy(g=>g.transform.position.y).ToArray();
            audio.volumes=new[]{.18f,.16f,.14f,.12f};audio.speakers=new AudioSource[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                var gear=gears[Mathf.RoundToInt(i*(gears.Length-1f)/(names.Length-1))];
                audio.speakers[i]=Speaker(ambience,"Mechanism emitter "+(i+1),gear.transform.position+Vector3.forward*.12f,audio.volumes[i],13,true,Clip(names[i]));
            }
            foreach(var stale in ambience.GetComponentsInChildren<AudioSource>().Where(s=>!audio.speakers.Contains(s)).ToArray())Object.DestroyImmediate(stale.gameObject);
            EditorUtility.SetDirty(audio);
            var cut=new[]{Clip("вырезка картона"),Clip("вырезка картона3")};var lever=Clip("РЫчаг");
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var sound=station.GetComponent<FactoryWorkbenchSound>();if(!sound)sound=station.gameObject.AddComponent<FactoryWorkbenchSound>();
                sound.leverClip=lever;sound.cuttingClips=cut;
                sound.cuttingFirst=Speaker(station.transform,"Cardboard cutting layer 1",station.transform.TransformPoint(station.cuttingPosition),0,11,true,cut[0]);
                sound.cuttingSecond=Speaker(station.transform,"Cardboard cutting layer 2",station.WheelCenter,0,11,true,cut[1]);
                sound.leverSpeaker=Speaker(station.transform,"Lever sound",station.feedLever.position,.36f,10,false,lever);EditorUtility.SetDirty(sound);
            }
            foreach(var signal in root.GetComponentsInChildren<FactoryPrinterSignal>(true))
            {signal.speaker.volume=.35f;EditorUtility.SetDirty(signal.speaker);}
            // Preserve the user's current layout as the reset pose, without moving any tray item.
            var tray=game.lunch.physicsTray;
            if(tray)
            {
                tray.homes=tray.contents.Select(item=>tray.transform.InverseTransformPoint(item.position)).ToArray();
                tray.rotations=tray.contents.Select(item=>Quaternion.Inverse(tray.transform.rotation)*item.rotation).ToArray();EditorUtility.SetDirty(tray);
            }
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install audio in Edit Mode.");
            try
            {
                AssetDatabase.Refresh();var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
                string before=ApplePose(game.lunch);File.WriteAllText("Library/FactoryAudioApplePose.txt",before);
                Install(game.transform);
                if(ApplePose(game.lunch)!=before)throw new InvalidOperationException("Apple pose unexpectedly changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                var sounds=game.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip && s.transform.IsChildOf(game.transform.Find("Spatial mechanism sounds")));
                File.WriteAllText("Library/FactoryAudioUpdate.txt","SUCCESS: footsteps, four spatial mechanism emitters, layered cutting, lever, printer at 70%, current apple pose saved.\n"+string.Join("\n",sounds.Select(s=>s.clip.name+": "+s.clip.length.ToString("F2")+"s @ "+s.transform.position)));
            }
            catch(Exception error){File.WriteAllText("Library/FactoryAudioUpdate.txt",error.ToString());Debug.LogException(error);}
        }
    }
}
