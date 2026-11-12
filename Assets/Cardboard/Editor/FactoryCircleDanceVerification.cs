using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryCircleDanceVerification
    {
        const string Pending="Cardboard.CircleDance.Verification",Recording="Cardboard.CircleDance.Recording";
        static double started;
        static bool background;
        static readonly List<string> report=new List<string>();
        static FactoryCircleDanceVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);}
        [MenuItem("Cardboard/Start Toc Toc recording")]
        public static void StartRecording()
        {
            if(EditorApplication.isPlaying){var game=CardboardFactoryGame.Instance;if(game && game.circleDance)game.circleDance.Begin();return;}
            FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Recording,true);EditorApplication.isPlaying=true;
        }
        [MenuItem("Cardboard/Verify Toc Toc circle dance")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            started=EditorApplication.timeSinceStartup;report.Clear();background=Application.runInBackground;Application.runInBackground=true;
            File.WriteAllText("Library/FactoryCircleDanceVerification.txt","RUNNING\n");
            SessionState.SetBool(Pending,true);FactoryMainMenu.SkipNextMenu=false;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Advance(FactoryCircleDance dance,float seconds)
        {
            while(seconds>.0001f){float dt=Mathf.Min(.02f,seconds);dance.Tick(dt);seconds-=dt;}
        }
        static void Play(FactoryMainMenu menu)
        {menu.Tick(1);menu.Choose(FactoryMenuAction.Play);menu.Tick(.3f);menu.Tick(1.2f);menu.Tick(.3f);menu.Tick(.6f);}
        static void CapturePortrait(Camera camera,string name)
        {
            var target=new RenderTexture(720,1280,24);var oldTarget=camera.targetTexture;var active=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=target;
                var game=CardboardFactoryGame.Instance;int outside=0;
                foreach(var worker in game.circleDance.Workers)
                foreach(var renderer in worker.puppet.GetComponentsInChildren<Renderer>())
                {
                    var bounds=renderer.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        var viewport=camera.WorldToViewportPoint(point);
                        if(viewport.z<=0 || viewport.x<.015f || viewport.x>.985f || viewport.y<.015f || viewport.y>.985f)outside++;
                    }
                }
                report.Add("DETAIL portrait aspect="+camera.aspect+" fov="+camera.fieldOfView+" outside="+outside);
                Check(outside==0,"The vertical shot contains the complete bodies of all twelve dancers");
                var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target};
                if(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera,request))UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
                else camera.Render();
                RenderTexture.active=target;image=new Texture2D(720,1280,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,720,1280),0,0);image.Apply();
                string path=Path.GetFullPath("Screenshots/"+name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".png");
                File.WriteAllBytes(path,image.EncodeToPNG());File.WriteAllText("Library/"+name+"Capture.txt",path);
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=active;if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Verify(CardboardFactoryGame game)
        {
            var dance=game.circleDance;var workers=game.crowd.runners.Where(w=>!w.isPlayer).ToArray();
            var positions=workers.Select(w=>w.transform.position).ToArray();var rotations=workers.Select(w=>w.transform.rotation).ToArray();
            var puppetPositions=workers.Select(w=>w.puppet.localPosition).ToArray();var enabled=workers.Select(w=>w.enabled).ToArray();
            var camera=Camera.main;var rig=camera.GetComponent<CardboardCamera>();var cameraPlayer=rig.player;
            float clock=game.ShiftRemaining,marks=game.Mistakes;bool audioPaused=AudioListener.pause,wasEnabled=dance.enabled;
            var keyboard=InputSystem.AddDevice<Keyboard>("Circle dance verification");
            var inputBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            dance.enabled=false;
            try
            {
                Check(dance && workers.Length==12,"Recording controller is installed automatically for all twelve workers");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F8));InputSystem.Update();dance.SendMessage("Update");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                if(!dance.Active)report.Add("DETAIL begin "+dance.LastFailure);
                report.Add("DETAIL circle center="+dance.Center+" radius="+dance.Radius+" camera="+camera.transform.position+" fov="+camera.fieldOfView);
                Check(dance.Active && !game.MenuActive && dance.Workers.Count==12,"F8 starts recording directly from the main menu");
                Check(Time.timeScale==0 && AudioListener.pause && !rig.player,"Recording pauses the shift and audio and releases the camera from the player");
                Check(game.GetComponentsInChildren<Canvas>(true).All(c=>!c.enabled),"The recording shot hides the gameplay HUD");
                Check(workers.All(w=>!w.enabled),"Ordinary worker AI is suspended while the choreography owns movement");
                Advance(dance,.5f);
                Check(!dance.InCircle && workers.Select((w,i)=>Mathf.Abs(w.puppet.localPosition.y-puppetPositions[i].y)).Max()<.04f,
                    "Workers approach on foot before the column is ready");
                for(int frame=0;frame<2000 && !dance.ColumnReady;frame++)dance.Tick(.02f);
                Check(dance.ColumnReady && !dance.InCircle,"All twelve workers line up before marching into the circle");
                report.Add("DETAIL column ready at "+dance.Elapsed+" seconds, spacing="+dance.ColumnSpacing);
                var column=dance.Workers.ToArray();var marchingFrom=column.Select(w=>w.transform.position).ToArray();
                bool inFile=true;
                for(int i=1;i<column.Length;i++)
                {
                    float gap=Vector3.Distance(column[i-1].transform.position,column[i].transform.position);
                    if(gap<.85f || gap>dance.ColumnSpacing+.45f)inFile=false;
                }
                Check(inFile,"The column has one worker per place with consistent following distances");
                Advance(dance,1);
                Check(!dance.InCircle && column.Select((w,i)=>Vector3.Distance(w.transform.position,marchingFrom[i])).Min()>.8f,
                    "The complete column marches together along its shared route");
                float gatheringPeriod=60/dance.beatsPerMinute;
                Advance(dance,(Mathf.Ceil(dance.DancingTime/gatheringPeriod)+.25f)*gatheringPeriod-dance.DancingTime);
                var columnHeights=workers.Select((w,i)=>w.puppet.localPosition.y-puppetPositions[i].y).ToArray();
                Check(!dance.InCircle && columnHeights.Min()>.2f && columnHeights.Max()-columnHeights.Min()<.01f,
                    "All twelve workers hop in sync while the column is still forming the circle");
                bool beatContinuous=true;float biggestJump=0,biggestMove=0,biggestTurn=0,closureSpeed=0;
                for(int frame=0;frame<2500 && !dance.InCircle;frame++)
                {
                    var beforePositions=workers.Select(w=>w.transform.position).ToArray();
                    var beforeHeights=workers.Select(w=>w.puppet.localPosition.y).ToArray();
                    var beforeRotations=workers.Select(w=>w.transform.rotation).ToArray();float beforeBeat=dance.DancingTime;
                    dance.Tick(.02f);
                    if(Mathf.Abs(dance.DancingTime-beforeBeat-.02f)>.002f)beatContinuous=false;
                    for(int i=0;i<workers.Length;i++)
                    {
                        biggestJump=Mathf.Max(biggestJump,Mathf.Abs(workers[i].puppet.localPosition.y-beforeHeights[i]));
                        biggestMove=Mathf.Max(biggestMove,Vector3.Distance(workers[i].transform.position,beforePositions[i]));
                        biggestTurn=Mathf.Max(biggestTurn,Quaternion.Angle(workers[i].transform.rotation,beforeRotations[i]));
                    }
                    if(dance.InCircle)closureSpeed=workers.Select((w,i)=>Vector3.Distance(w.transform.position,beforePositions[i])/.02f).Min();
                }
                Check(beatContinuous,"The hop beat stays continuous when the last worker closes the circle");
                Check(biggestJump<.09f && biggestMove<.11f && biggestTurn<25,
                    "The complete transition has no jumps in position, height or facing between frames");
                Check(closureSpeed>.5f,"The circle continues moving through closure without stopping the dancers");
                report.Add("DETAIL transition max height step="+biggestJump+" move="+biggestMove+" turn="+biggestTurn+" closure speed="+closureSpeed);
                Check(dance.InCircle && dance.GatheredCount==12,"The column closes into a circle with all twelve workers");
                report.Add("DETAIL circle closed at "+dance.Elapsed+" seconds");
                if(!dance.InCircle)foreach(var worker in workers)report.Add("DETAIL "+worker.name+" "+dance.Describe(worker));
                Check(workers.All(w=>Mathf.Abs(Vector3.Distance(w.transform.position,dance.Center)-dance.Radius)<.4f),
                    "The formation is a circle in the center of the workshop");
                float minimum=float.PositiveInfinity;
                for(int i=0;i<workers.Length;i++)for(int j=i+1;j<workers.Length;j++)minimum=Mathf.Min(minimum,Vector3.Distance(workers[i].transform.position,workers[j].transform.position));
                Check(minimum>1,"The circle keeps distinct places instead of stacking workers");
                float period=60/dance.beatsPerMinute;
                float target=(Mathf.Ceil(dance.DancingTime/period)+.25f)*period;Advance(dance,target-dance.DancingTime);
                var left=workers.Select(w=>w.transform.rotation).ToArray();
                var heights=workers.Select((w,i)=>w.puppet.localPosition.y-puppetPositions[i].y).ToArray();
                Check(heights.Min()>.2f && heights.Max()-heights.Min()<.01f,"All workers jump in sync on the same beat");
                CapturePortrait(camera,"TocTocCircleLeft");
                Advance(dance,period);
                Check(workers.Select((w,i)=>Quaternion.Angle(left[i],w.transform.rotation)).Average()>55,
                    "The next hop visibly turns the workers toward the opposite side");
                CapturePortrait(camera,"TocTocCircleRight");
                var beforeOrbit=workers[0].transform.position;Advance(dance,3);
                float rotation=Vector3.SignedAngle(beforeOrbit-dance.Center,workers[0].transform.position-dance.Center,Vector3.up);
                Check(rotation>75 && rotation<100,"The ring turns about ninety degrees in three seconds at the faster speed");
                Check(game.ShiftRemaining==clock && game.Mistakes==marks,"The recording never consumes shift time or adds conduct penalties");
                game.mainMenu.ReturnToMenu();float paused=dance.Elapsed;var pausedPosition=workers[0].transform.position;dance.Tick(2);
                Check(dance.Elapsed==paused && workers[0].transform.position==pausedPosition,"Escape-menu pause freezes the choreography");
                Play(game.mainMenu);dance.Tick(.1f);
                Check(dance.Active && dance.Elapsed>paused && !game.MenuActive,"Play resumes recording without starting the introduction");
                Check(dance.Restart() && dance.Elapsed==0 && !dance.InCircle,"F9 can restart the gathering for another take");
                dance.Stop();
                Check(game.MenuActive && !dance.Active && Time.timeScale==0,"Stopping a take opened from the menu returns to the menu");
                Check(workers.Select((w,i)=>Vector3.Distance(w.transform.position,positions[i])<.001f &&
                    Quaternion.Angle(w.transform.rotation,rotations[i])<.01f && w.enabled==enabled[i] &&
                    Vector3.Distance(w.puppet.localPosition,puppetPositions[i])<.001f).All(ok=>ok),
                    "Stopping restores every worker's exact original pose and AI state");
                Check(rig.player==cameraPlayer && AudioListener.pause==audioPaused,"Stopping restores the player camera and original audio state");
                Play(game.mainMenu);game.intro.Tick(2);
                var phase=game.intro.Phase;var introPosition=CardboardRunner.Player.transform.position;var introView=game.intro.CameraPosition;
                Check(dance.Begin() && !game.PlayerBlocked && !game.CameraOverride,"Recording can temporarily take over an active introduction");
                Advance(dance,2);
                // The disabled introduction does not receive engine updates; direct calls are avoided after this point.
                dance.Stop(false);
                Check(game.intro.enabled && game.intro.Phase==phase &&
                    Vector3.Distance(CardboardRunner.Player.transform.position,introPosition)<.001f &&
                    Vector3.Distance(game.intro.CameraPosition,introView)<.001f && Time.timeScale==1,
                    "The introduction resumes from its original scene and player position after filming");
            }
            finally
            {
                if(dance.Active)dance.Stop(false);dance.enabled=wasEnabled;
                if(keyboard.added)InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=inputBackground;Time.timeScale=1;AudioListener.pause=audioPaused;
            }
        }
        static void Finish()
        {
            File.WriteAllLines("Library/FactoryCircleDanceVerification.txt",new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            SessionState.SetBool(Pending,false);Application.runInBackground=background;EditorApplication.isPlaying=false;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            if(SessionState.GetBool(Recording,false))
            {SessionState.SetBool(Recording,false);CardboardFactoryGame.Instance.circleDance.Begin();}
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.timeSinceStartup-started>90){report.Add("FAIL timeout");Finish();return;}
            try{Verify(CardboardFactoryGame.Instance);}catch(Exception error){report.Add("FAIL "+error);}finally{Finish();}
        }
    }
}