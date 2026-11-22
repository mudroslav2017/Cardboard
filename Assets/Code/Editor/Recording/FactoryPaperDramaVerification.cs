using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using D=Cardboard.FactoryPaperDrama;
namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryPaperDramaVerification
    {
        const string Pending="Cardboard.PaperDrama.Verify",Recording="Cardboard.PaperDrama.Record";
        static readonly List<string> report=new List<string>();
        static int controlsFrames;static string controlsPath;
        static FactoryPaperDramaVerification(){EditorApplication.update+=Update;}
        [MenuItem("Cardboard/Start paper drama recording")]
        public static void Record()
        {
            if(EditorApplication.isPlaying){CardboardFactoryGame.Instance.paperDrama.Begin();return;}
            if(!AssetDatabase.LoadAssetAtPath<FactoryPaperDramaSettings>(FactoryPaperDramaAssets.Root+"/Settings.asset"))FactoryPaperDramaAssets.Prepare();
            FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Recording,true);EditorApplication.isPlaying=true;
        }
        [MenuItem("Cardboard/Verify paper drama recording")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();File.WriteAllText("Library/FactoryPaperDramaVerification.txt","RUNNING");FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        [MenuItem("Cardboard/Preview paper drama timeline")]
        public static void Controls()
        {
            SessionState.SetBool("Cardboard.PaperDrama.Controls",true);controlsFrames=0;
            Record();EditorApplication.ExecuteMenuItem("Window/General/Game");
        }        static void Check(bool value,string text){report.Add((value?"PASS ":"FAIL ")+text);}
        static Texture2D Render(Camera camera,int width,int height)
        {
            var target=new RenderTexture(width,height,24);var original=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
                var request=new RenderPipeline.StandardRequest{destination=target};if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();
                RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image;
            }
            finally{camera.targetTexture=original;camera.rect=rect;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Capture(FactoryPaperDrama movie,string name)
        {
            Texture2D image=null,top=null,bottom=null;
            try
            {
                if(movie.SplitScreen)
                {
                    top=Render(Camera.main,540,480);bottom=Render(movie.SecondCamera,540,480);image=new Texture2D(540,960,TextureFormat.RGB24,false);
                    image.SetPixels(0,480,540,480,top.GetPixels());image.SetPixels(0,0,540,480,bottom.GetPixels());image.Apply();
                }
                else image=Render(Camera.main,540,960);
                Directory.CreateDirectory("Screenshots");string path=Path.GetFullPath("Screenshots/PaperDrama-"+name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".png");
                if(!movie.SplitScreen)
                {
                    foreach(var label in new[]{movie.HeroLine,movie.QuestionLine,movie.BossLine,movie.FinalLine,movie.RoofLine})
                    {
                        if(!label.gameObject.activeInHierarchy)continue;var b=label.GetComponent<Renderer>().bounds;bool fits=true;
                        for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var v=Camera.main.WorldToViewportPoint(p);if(v.z<=0 || v.x<.025f || v.x>.975f || v.y<.025f || v.y>.975f)fits=false;}
                        Check(fits,name+" dialogue stays inside the vertical frame");
                    }
                }
                File.WriteAllBytes(path,image.EncodeToPNG());File.WriteAllText("Library/PaperDrama"+name+"Capture.txt",path);
            }
            finally{if(image)UnityEngine.Object.DestroyImmediate(image);if(top)UnityEngine.Object.DestroyImmediate(top);if(bottom)UnityEngine.Object.DestroyImmediate(bottom);}
        }
        static void PlayMenu(FactoryMainMenu menu){menu.Tick(1);menu.Choose(FactoryMenuAction.Play);menu.Tick(.3f);menu.Tick(1.2f);menu.Tick(.3f);menu.Tick(.6f);}
        static void Verify(CardboardFactoryGame game)
        {
            var movie=game.paperDrama;var camera=Camera.main;var rig=camera.GetComponent<CardboardCamera>();var originalPlayer=rig.player;var originalView=camera.transform.position;float fov=camera.fieldOfView;
            var sourceMesh=Resources.Load<GameObject>("CardboardCharacters/HumanNastya").GetComponent<MeshFilter>().sharedMesh;var sourceVertices=sourceMesh.vertices;
            var workers=game.crowd.runners.ToArray();var positions=workers.Select(w=>w.transform.position).ToArray();var enabled=workers.Select(w=>w.enabled).ToArray();
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);var lightStates=lights.Select(l=>l.enabled).ToArray();
            float clock=game.ShiftRemaining,marks=game.Mistakes;bool audio=AudioListener.pause;var cameraRect=camera.rect;movie.enabled=false;
            try
            {
                Check(movie.Begin() && game.FilmActive,"The film starts from the menu and pauses gameplay");if(!movie.Active)return;
                Check(movie.ExistingHouse && movie.ExistingHouse.name.StartsWith("house_01"),"The film uses the placed house 1");
                Check(movie.Hero.GetComponentInChildren<MeshFilter>().sharedMesh==sourceMesh,"The original Nastya geometry is preserved");
                Check(Mathf.Abs(movie.FilmPixels.width/movie.FilmPixels.height-9f/16)<.001f,"The camera frame remains 9:16");
                Check(movie.ControlsVisible && movie.TimelineBar.width>100 && D.Duration==43,"The timeline includes the extended 43 second cut");
                var settings=Resources.Load<FactoryPaperDramaSettings>("PaperDrama/Settings");Check(new[]{settings.whoosh,settings.thunder,settings.rainLoop}.All(c=>c && c.length>.4f && AssetDatabase.GetAssetPath(c).EndsWith(".wav")),"Sound effects remain persistent WAV assets");
                movie.Seek(.95f);Check(movie.HeroLine.gameObject.activeSelf,"The greeting begins while the hero enters");Capture(movie,"HomeStart");
                movie.Seek(3.9f);Check(movie.HeroLine.gameObject.activeSelf && movie.HeroLine.font==CardboardTypography.DefaultFont,"The complete greeting is held longer in the game font");Capture(movie,"Home");
                movie.Seek(3);var idleLeft=movie.Hero.rotation;float idleLow=sourceVertices.Min(v=>movie.Hero.GetComponentInChildren<MeshFilter>().transform.TransformPoint(v).y);
                movie.Seek(3.5f);float idleHigh=sourceVertices.Min(v=>movie.Hero.GetComponentInChildren<MeshFilter>().transform.TransformPoint(v).y);
                Check(Quaternion.Angle(idleLeft,movie.Hero.rotation)>3 && idleHigh>idleLow+.005f && idleHigh-movie.StreetHeight<.03f,"After arriving home the hero rocks between feet and makes small hops");
                movie.Seek(2.199f);var beforeIdle=movie.Hero.position;var beforeIdleRotation=movie.Hero.rotation;movie.Seek(2.201f);Check(Vector3.Distance(beforeIdle,movie.Hero.position)<.005f && Quaternion.Angle(beforeIdleRotation,movie.Hero.rotation)<.1f,"Walking blends into the idle without an abrupt freeze");movie.Seek(3.9f);
                var room=movie.Hero.parent.Find("Home interior • cutaway");var sofa=room.Find("Sofa2(Clone)");
                Check(movie.Hero.position.z>sofa.position.z+2 && Mathf.Abs(movie.Hero.position.x-sofa.position.x)<.1f && movie.Hero.forward.z<-.95f,"The hero enters from directly opposite the sofa and faces it");
                Check(movie.Boss.Find("Boss necktie")==null && movie.BossBadge.Find("Boss badge text")==null && movie.BossBadge.Find("Clean boss name card")==null,"Only the authored tie and badge graphics remain");
                Check(movie.Hero.parent.GetComponentsInChildren<TextMesh>(true).Length==5,"Only dialogue text is created in the film");
                movie.Seek(D.SplitAt+.6f);Check(movie.SplitScreen && movie.SecondCamera.enabled,"The reveal retains the split screen");
                Check(movie.KissDistance<.02f && Vector3.Dot(movie.Wife.forward,movie.Boss.forward)<-.55f,"The printed face surfaces meet face to face, cardboard against cardboard");Capture(movie,"Split");
                movie.Seek(6.35f);var kissA=movie.Wife.rotation;Capture(movie,"KissLeft");movie.Seek(7.1f);var kissB=movie.Wife.rotation;Capture(movie,"KissRight");movie.Seek(7.85f);Check(Quaternion.Angle(kissA,kissB)>25 && Quaternion.Angle(kissB,movie.Wife.rotation)>25 && Quaternion.Angle(kissA,movie.Wife.rotation)<3 && movie.KissDistance<.02f,"The lovers rotate back and forth while keeping cardboard contact");
                Check(D.BadgeAt-D.RecognitionAt>=2.1f && D.ShockAt-D.BadgeEnd>=2.1f,"Both red close-ups hold for more than two seconds");
                movie.Seek(D.SplitEnd+.5f);Check(movie.QuestionLine.gameObject.activeSelf && movie.QuestionLine.text=="Хуанг?" && !movie.Recognition && movie.RedAccent==0,"Nastya asks Huang before the boss recognition montage");Capture(movie,"Question");
                movie.Seek(D.RecognitionAt+.4f);Check(movie.Recognition && !movie.BadgeShot && movie.RedAccent>3 && camera.fieldOfView>90,"The question cuts to a red-lit shaken close-up of Kolya");Capture(movie,"BossRed");
                movie.Seek(D.BadgeAt+.1f);float left=camera.WorldToViewportPoint(movie.BossBadge.Find("Green reflection across badge").position).x;Check(movie.BadgeShot && movie.RedAccent==0,"The next insert isolates the badge");Capture(movie,"Badge");
                movie.Seek(D.BadgeEnd-.1f);float right=camera.WorldToViewportPoint(movie.BossBadge.Find("Green reflection across badge").position).x;Check(right>left+.1f,"The green reflection travels across the badge from left to right");
                movie.Seek(D.BadgeEnd+.3f);Check(movie.Recognition && !movie.BadgeShot && movie.RedAccent>3,"The edit returns to Kolya's red face before the awkward pause");Capture(movie,"BossReturn");
                movie.Seek(D.ShockAt-.1f);var held=movie.Bouquet.position;movie.Seek(D.ShockAt+.6f);
                Check(movie.Bouquet.position.y<held.y-.3f && movie.ColdGrade>.99f && camera.fieldOfView>100,"The bouquet drops during the subsequent blue awkward-pause shot");Capture(movie,"Shock");
                movie.Seek(D.BossTalkAt+.9f);var direction=movie.Wife.position-movie.Boss.position;direction.y=0;
                Check(movie.ColdGrade==0 && movie.BossLine.gameObject.activeSelf && Vector3.Dot(movie.Boss.forward,direction.normalized)>.95f,"The boss speaks to the wife after the cold grade clears");Check((camera.cullingMask & (1<<16))==0 && camera.WorldToViewportPoint(movie.Boss.position+Vector3.up*1.76f).x>.3f && camera.WorldToViewportPoint(movie.Boss.position+Vector3.up*1.76f).x<.7f,"The wife cannot obscure Huang in his dialogue shot");Capture(movie,"Boss");
                movie.Seek(D.RoofAt+.7f);Check(movie.RainVisible && movie.Hero.parent.Find("Roof edge • cardboard flashing")==null,"The hero stands on the roof with no artificial support");
                Check(movie.RoofLine.gameObject.activeSelf && movie.RoofLine.text=="Хуанг,\nкак ты мог","The first roof line appears exactly as requested");Capture(movie,"RoofFirst");
                movie.Seek(D.RoofAt+2.7f);Check(movie.RoofLine.text=="это уже 3-я\nлюбовница","The second roof line follows the first");Capture(movie,"Roof");
                movie.Seek(D.FarewellAt+.6f);Check(movie.RoofLine.gameObject.activeSelf && movie.RoofLine.text=="Не поминайте\nлихом" && movie.Elapsed<D.FallAt,"The farewell follows the third-mistress line before the fall");Capture(movie,"RoofFarewell");
                movie.Seek(D.FallAt-.001f);var roofPosition=movie.Hero.position;var roofRotation=movie.Hero.rotation;var roofCamera=movie.CameraPosition;var roofCameraRotation=movie.CameraRotation;
                movie.Seek(D.FallAt+.001f);Check(Vector3.Distance(roofPosition,movie.Hero.position)<.02f && Quaternion.Angle(roofRotation,movie.Hero.rotation)<.2f,"The roof sway continues into the fall without teleporting");
                movie.Seek(D.FallAt+.65f);Check(!movie.FastFall && movie.Hero.position.y<roofPosition.y-1 && Vector3.Distance(roofCamera,movie.CameraPosition)<.01f && Quaternion.Angle(roofCameraRotation,movie.CameraRotation)<.1f,"The roof camera holds while the hero visibly drops before the next cut");movie.Seek(D.FallAt+.35f);Capture(movie,"RoofFall");movie.Seek(D.FallAt+.65f);
                var fallPosition=movie.Hero.position;var fallRotation=movie.Hero.rotation;movie.Seek(1);movie.Seek(D.FallAt+.65f);Check(Vector3.Distance(fallPosition,movie.Hero.position)<.00001f && Quaternion.Angle(fallRotation,movie.Hero.rotation)<.01f,"Scrubbing reconstructs the roof departure independently of previous frames");
                movie.Seek(D.DiveAt-.001f);var beforeCut=movie.Hero.position;var rotationBeforeCut=movie.Hero.rotation;movie.Seek(D.DiveAt+.001f);Check(Vector3.Distance(beforeCut,movie.Hero.position)<.01f && Quaternion.Angle(rotationBeforeCut,movie.Hero.rotation)<.1f,"The falling pose stays continuous across the later speed-shot cut");
                Check(Mathf.Abs((D.GlideAt-D.DiveAt)-2.4f)<.001f,"The high-speed fall lasts three times as long");
                movie.Seek(D.DiveAt+.4f);Check(movie.FastFall && Vector3.Dot(movie.Hero.forward,Vector3.down)>.99f && camera.fieldOfView>=100,"The fall begins with a face-down high-speed close-up");Capture(movie,"FastFall");
                float previousTurn=float.PositiveInfinity;bool descendsEachTurn=true;foreach(float turn in new[]{.8f,2.4f,4f,5.6f,7.2f}){movie.Seek(D.GlideAt+turn);if(!float.IsPositiveInfinity(previousTurn) && previousTurn-movie.Hero.position.y<.55f)descendsEachTurn=false;previousTurn=movie.Hero.position.y;}
                Check(descendsEachTurn,"Each fwoosh turn carries the hero visibly lower");
                movie.Seek(D.GlideAt+.8f);float firstTurnScreenY=camera.WorldToViewportPoint(movie.Hero.position).y;movie.Seek(D.GlideAt+2.4f);Check(camera.WorldToViewportPoint(movie.Hero.position).y<firstTurnScreenY-.04f,"The camera no longer cancels out the first gliding descent");Capture(movie,"FloatLower");
                movie.Seek(D.GlideAt+.7f);var early=movie.Hero.position;Check(!movie.FastFall,"The speed illusion cuts to slow paper gliding");Capture(movie,"Float");
                movie.Seek(D.LandingAt-1.2f);Check(early.y>movie.Hero.position.y && early.y-movie.Hero.position.y>2.5f && movie.Hero.position.y>movie.StreetHeight+1,"The glide visibly descends several metres before the reveal");
                Check(Vector3.Distance(movie.CameraPosition,movie.Hero.position)>15,"The camera pulls away for the slow-fall reveal");Capture(movie,"Reveal");
                var savedPosition=movie.Hero.position;var savedCamera=movie.CameraPosition;movie.Seek(1);movie.Seek(D.LandingAt-1.2f);Check(Vector3.Distance(savedPosition,movie.Hero.position)<.00001f && Vector3.Distance(savedCamera,movie.CameraPosition)<.00001f,"Reverse seeking reconstructs the exact same pose and camera");
                movie.Seek(D.LandingAt+.3f);var flatMesh=movie.Hero.GetComponentInChildren<MeshFilter>();float bottom=sourceVertices.Min(v=>flatMesh.transform.TransformPoint(v).y);Check(Mathf.Abs(bottom-movie.StreetHeight)<.015f,"The sheet lands on the street floor");Capture(movie,"Landing");
                movie.Seek(D.LandingAt+3.2f);Check(movie.Hero.up.y>.98f && movie.FinalLine.gameObject.activeSelf && movie.FinalLine.text=="Почему я такой\nнеудачник","The new final line follows the hero standing up");Capture(movie,"Final");
                movie.Seek(D.FadeAt+.5f);Check(movie.FadeOpacity>.4f && movie.FadeOpacity<.6f,"The ending fades smoothly through a half-black frame");Capture(movie,"Fade");
                movie.Seek(D.Duration);var black=Render(camera,64,128);Check(black.GetPixels32().All(c=>c.r<=2 && c.g<=2 && c.b<=2),"The final image is fully black");UnityEngine.Object.DestroyImmediate(black);
                Check(sourceMesh.vertices.SequenceEqual(sourceVertices),"No part of the montage deforms the original character mesh");
                int cues=movie.CueCount;movie.SeekNormalized(.5f);Check(Mathf.Abs(movie.Elapsed-D.Duration*.5f)<.0001f && !movie.Playing && movie.CueCount==cues,"Mouse-style scrubbing pauses without replaying sound cues");
                movie.Seek(0);movie.SetPlaying(true);movie.Tick(.9f);Check(movie.CueCount>cues,"Playing across a dialogue cue triggers paper audio");
                var dialogue=movie.Hero.parent.Find("Dialogue • game worker remarks").GetComponent<AudioSource>();var workerVoices=game.crowd.runners.Where(w=>!w.isPlayer && w.GetComponent<FactoryWorkerImpact>()).SelectMany(w=>w.GetComponent<FactoryWorkerImpact>().voices).Distinct().ToArray();
                Check(settings.dialogueVoices.Length==3 && settings.dialogueVoices.All(c=>c && workerVoices.Contains(c)) && workerVoices.Contains(dialogue.clip),"Film dialogue uses the exact clips assigned to workers remarks");
                movie.Tick(.8f);Check(dialogue.isPlaying && dialogue.loop && Mathf.Abs(dialogue.volume-.55f)<.01f,"Dialogue plays beyond the old short cutoff with the worker voice volume");
                movie.SetPlaying(false);float paused=movie.Elapsed;movie.Tick(1);Check(movie.Elapsed==paused,"Pause freezes the film");
                game.mainMenu.ReturnToMenu();movie.SetPlaying(true);movie.Tick(1);Check(movie.Elapsed==paused,"The menu freezes the timeline");PlayMenu(game.mainMenu);movie.Tick(.1f);Check(movie.Elapsed>paused && movie.Active,"Play resumes the same film");
                movie.Seek(D.Duration-.05f);movie.SetPlaying(true);movie.Tick(1);Check(movie.Elapsed==D.Duration && !movie.Playing,"Playback stops exactly at the end of the extended cut");
                Check(game.ShiftRemaining==clock && game.Mistakes==marks,"Recording preserves shift progress");
                movie.Stop();Check(!game.FilmActive && game.MenuActive,"Stopping the recording returns to the original menu");
                Check(workers.Select((w,i)=>w.transform.position==positions[i] && w.enabled==enabled[i]).All(v=>v),"All original workers retain their positions and AI state");
                Check(lights.Select((l,i)=>l.enabled==lightStates[i]).All(v=>v),"Original lighting is restored");
                Check(camera.rect==cameraRect && camera.fieldOfView==fov && rig.player==originalPlayer && camera.transform.position==originalView && AudioListener.pause==audio,"The camera, viewport, player and audio are restored");
                Check(movie.Begin(),"The film can be launched again in the same play session");movie.Seek(D.RoofAt+.7f);Check(movie.RainVisible,"Rain also works on repeated recording sessions");movie.Stop();
            }
            finally{if(movie.Active)movie.Stop(false);movie.enabled=true;}
        }
        static void Update()
        {
            if(!EditorApplication.isPlaying || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            if(SessionState.GetBool(Recording,false)){SessionState.SetBool(Recording,false);CardboardFactoryGame.Instance.paperDrama.Begin();}
            if(SessionState.GetBool("Cardboard.PaperDrama.Controls",false))
            {
                controlsFrames++;EditorApplication.QueuePlayerLoopUpdate();
                if(controlsFrames==8)
                {
                    CardboardFactoryGame.Instance.paperDrama.Seek(D.SplitAt+.8f);controlsPath=Path.GetFullPath("Screenshots/PaperDrama-Timeline-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".png");
                    ScreenCapture.CaptureScreenshot(controlsPath);File.WriteAllText("Library/PaperDramaTimelineCapture.txt",controlsPath);
                }
                if(controlsFrames>35){SessionState.SetBool("Cardboard.PaperDrama.Controls",false);EditorApplication.isPlaying=false;}
            }
            if(!SessionState.GetBool(Pending,false))return;SessionState.SetBool(Pending,false);
            try{Verify(CardboardFactoryGame.Instance);}catch(Exception error){report.Add("FAIL "+error);}
            File.WriteAllLines("Library/FactoryPaperDramaVerification.txt",new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));EditorApplication.isPlaying=false;
        }
    }
}