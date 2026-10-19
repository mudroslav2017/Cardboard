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
    public static class FactoryWorkerVisualVerification
    {
        const string Active="Cardboard.WorkerVisualVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;
        static CardboardRunner[] workers;
        static FactoryWorkerAppearance[] people;
        static FactoryWorkerImpact reaction;
        static Vector3[] homes;static Quaternion[] rotations;
        static int stage;static double began;static float at;static bool background;
        static FactoryWorkerVisualVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);}
        static void Save(){File.WriteAllLines("Library/FactoryWorkerVisualVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;began=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Finish()
        {
            if(game){for(int i=0;homes!=null && i<workers.Length;i++){workers[i].transform.SetPositionAndRotation(homes[i],rotations[i]);}foreach(var w in game.crowd.runners)w.enabled=true;game.ResetShift();game.enabled=true;game.station.enabled=true;game.mainMenu.enabled=true;foreach(var w in workers)w.enabled=true;var camera=Camera.main.GetComponent<CardboardCamera>();if(camera)camera.enabled=true;}
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static void Score(float value)
        {
            game.ResetShift();if(value>0)game.RegisterMistake("Expression test",value);
            foreach(var w in workers)w.GetComponent<FactoryWorkerImpact>().ResetReaction();game.expressions.Refresh();
        }
        static void Frame(CardboardRunner worker)
        {
            var camera=Camera.main;
            for(int i=0;i<workers.Length;i++)workers[i].transform.SetPositionAndRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX+(i-System.Array.IndexOf(workers,worker))*2,0,0),Quaternion.identity);
            camera.transform.position=worker.transform.position+new Vector3(.2f,1.7f,4.4f);
            camera.transform.rotation=Quaternion.LookRotation(worker.transform.position+Vector3.up*1.7f-camera.transform.position);
            at=Time.time;
        }
        static void MarkerPersistence(FactoryWorkerAppearance person)
        {
            var surface=person.GetComponent<CardboardDrawingSurface>();var renderer=person.GetComponent<Renderer>();
            var uv=new Vector2(.22f,.45f);person.SetState(FactoryFaceState.Normal);
            surface.PaintStroke(uv,uv,Color.blue,.08f);
            var tex=surface.DrawingTexture;var ink=tex.GetPixelBilinear(uv.x,uv.y);
            Check(ink.b>ink.r+.4f,"Marker paints the imported atlas: "+person.name);
            person.SetState(FactoryFaceState.Suspicious);var after=tex.GetPixelBilinear(uv.x,uv.y);
            Check(after.b>after.r+.4f && renderer.sharedMaterial.mainTexture==tex,"Marker survives suspicious face: "+person.name);
            person.SetState(FactoryFaceState.Angry);after=tex.GetPixelBilinear(uv.x,uv.y);
            Check(after.b>after.r+.4f,"Marker survives angry face: "+person.name);
            var atlas=(Texture2D)person.angry.mainTexture;surface.ClearDrawing();
            Check(Vector4.Distance(atlas.GetPixelBilinear(uv.x,uv.y),tex.GetPixelBilinear(uv.x,uv.y))<.01f,"Clear drawing restores the current angry atlas: "+person.name);
            person.SetState(FactoryFaceState.Normal);
        }
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;game.enabled=false;game.station.enabled=false;
            typeof(FactoryMainMenu).GetMethod("Close",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.mainMenu,null);game.mainMenu.enabled=false;
            workers=game.crowd.runners.Where(w=>!w.isPlayer).ToArray();homes=workers.Select(w=>w.transform.position).ToArray();rotations=workers.Select(w=>w.transform.rotation).ToArray();foreach(var w in game.crowd.runners)w.enabled=false;
            Camera.main.GetComponent<CardboardCamera>().enabled=false;
            people=workers.Select(w=>w.puppet.GetComponentInChildren<FactoryWorkerAppearance>()).ToArray();
            Check(workers.Length==11 && people.All(p=>p && p.gameObject.activeInHierarchy),"All eleven workers are visible and use new expressions");
            Check(people.Any(p=>p.name=="HumanLera") && people.Any(p=>p.name=="HumanKolya"),"Both new character models are present");
            foreach(var p in people)
            {
                Check(new[]{p.normal,p.suspicious,p.angry}.All(m=>m && m.GetFloat("_Cull")==0 && m.doubleSidedGI && m.color==Color.white),"All expression materials are double sided: "+p.name);
                Check(p.GetComponent<MeshFilter>().sharedMesh.bounds.size.y>2.30f && p.GetComponent<MeshFilter>().sharedMesh.bounds.size.y<2.32f,"Authored character proportions are preserved at 2.31m height: "+p.name);
            }
            ImportedCharacterInstaller.VerifyRuntime(Check);
            foreach(var p in people.GroupBy(p=>p.name).Select(g=>g.First()))MarkerPersistence(p);
            Score(0);Check(people.All(p=>p.State==FactoryFaceState.Normal),"Score zero: all normal");
            Score(.5f);Check(people.All(p=>p.State==FactoryFaceState.Normal),"Half a mark: all normal");
            Score(1);Check(people.Count(p=>p.State==FactoryFaceState.Suspicious)==6,"One complete mark: six of eleven workers suspicious");
            var selected=workers.Where(game.expressions.IsSuspicious).ToArray();
            game.RegisterMistake("Second half mark",.5f);foreach(var w in workers)w.GetComponent<FactoryWorkerImpact>().ResetReaction();game.expressions.Refresh();
            Check(selected.SequenceEqual(workers.Where(game.expressions.IsSuspicious)) && people.Count(p=>p.State==FactoryFaceState.Suspicious)==6,"The same six stay suspicious as score increases");
            foreach(var w in workers)
            {
                var r=w.GetComponent<FactoryWorkerImpact>();var bounds=r.message.GetComponent<MeshFilter>().sharedMesh.bounds;
                float top=w.puppet.GetComponentsInChildren<Renderer>(true).Max(m=>m.bounds.max.y);
                Check(Mathf.Abs(bounds.size.x-1)<.001f && r.message.GetComponent<Renderer>().bounds.min.y>=top+.295f,"Larger message stays above the head: "+w.name);
                Check(r.reactionIcons.All(t=>t.GetComponent<MeshFilter>().sharedMesh.bounds.size.x<=.681f && t.GetComponent<MeshFilter>().sharedMesh.bounds.size.y<=.401f) && r.reactionIcons.Max(t=>t.GetComponent<MeshFilter>().sharedMesh.bounds.size.x)>.5f,"Large proportional icons fit the message: "+w.name);
            }
            var label=game.instruction.controlHint.GetComponent<TextMesh>();
                        var settings=new TextGenerationSettings{font=label.font,fontSize=label.fontSize,fontStyle=FontStyle.Normal,color=Color.white,scaleFactor=1,lineSpacing=1,textAnchor=TextAnchor.LowerLeft,horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow,generationExtents=Vector2.zero,generateOutOfBounds=true};
            using(var generator=new TextGenerator())
            {
                generator.Populate(label.text,settings);Bounds ink=default;bool first=true;var verts=generator.verts;
                for(int q=0;q+3<verts.Count;q+=4){if((verts[q].position-verts[q+2].position).sqrMagnitude<.001f)continue;for(int c=0;c<4;c++){if(first){ink=new Bounds(verts[q+c].position,Vector3.zero);first=false;}else ink.Encapsulate(verts[q+c].position);}}
                var sheet=game.instruction.sheet.GetComponent<MeshFilter>().sharedMesh.bounds;
                float previous=Mathf.Min((sheet.size.x-.16f)/ink.size.x,.022f/ink.size.y)*10;
                Check(label.text=="LBM - TROW, RBM - PUT" && Mathf.Abs(label.characterSize-previous*2)<.00001f,"Instruction print is exactly twice the previous size: actual="+label.characterSize+" old="+previous+" sheet="+sheet.size+" print="+ink.size*(label.characterSize*.1f));
            }
            Score(0);Frame(workers.First(w=>w.puppet.GetComponentInChildren<FactoryWorkerAppearance>().name=="HumanKolya"));
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-began>60){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;Save();return;}
                if(Time.time-at<.25f)return;
                if(stage==1){CardboardFactorySceneBuilder.Capture("NewKolyaNormal");Score(1);Frame(workers.First(game.expressions.IsSuspicious));stage=2;return;}
                if(stage==2)
                {
                    CardboardFactorySceneBuilder.Capture("WorkerSuspicious");reaction=workers.First(game.expressions.IsSuspicious).GetComponent<FactoryWorkerImpact>();
                    reaction.Notice(4,FactoryWorkerEmotion.Thrown);game.expressions.Refresh();
                    Check(reaction.GetComponentInChildren<FactoryWorkerAppearance>().State==FactoryFaceState.Angry,"Message overrides suspicious face with angry face");Frame(reaction.GetComponent<CardboardRunner>());stage=3;return;
                }
                if(stage==3)
                {
                    CardboardFactorySceneBuilder.Capture("NewAngryWorkerMessage");reaction.Notice(.02f);at=Time.time;stage=4;return;
                }
                if(stage==4)
                {
                    game.expressions.Refresh();Check(!reaction.Reacting && reaction.GetComponentInChildren<FactoryWorkerAppearance>().State==FactoryFaceState.Suspicious,"After speech the selected worker returns to suspicious");
                    var normal=workers.First(w=>!game.expressions.IsSuspicious(w)).GetComponent<FactoryWorkerImpact>();normal.Notice();game.expressions.Refresh();
                    Check(normal.GetComponentInChildren<FactoryWorkerAppearance>().State==FactoryFaceState.Angry,"An unselected worker also becomes angry while speaking");normal.ResetReaction();game.expressions.Refresh();
                    Check(normal.GetComponentInChildren<FactoryWorkerAppearance>().State==FactoryFaceState.Normal,"An unselected worker returns to normal after speaking");
                    game.captureSequence.Begin();game.expressions.Refresh();
                    Check(game.expressions.people.All(p=>p.State==FactoryFaceState.Angry),"Capture makes every character angry, including the press victim");
                    game.captureSequence.Cancel();Score(0);Check(people.All(p=>p.State==FactoryFaceState.Normal),"Reset restores normal faces and clears capture");Finish();
                }
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}