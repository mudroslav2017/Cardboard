using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryGuitarVerification
    {
        const string Pending="Cardboard.Guitar.Verify",Recording="Cardboard.Guitar.Record";
        static readonly List<string> report=new List<string>();
        static FactoryGuitarVerification(){EditorApplication.update+=Update;}
        [MenuItem("Cardboard/Start Nastya guitar recording")]
        public static void Record()
        {
            if(EditorApplication.isPlaying){CardboardFactoryGame.Instance.guitarPerformance.Begin();return;}
            FactoryGuitarAssets.Prepare();FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Recording,true);EditorApplication.isPlaying=true;
        }
        [MenuItem("Cardboard/Verify Nastya guitar recording")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            FactoryGuitarAssets.Prepare();report.Clear();File.WriteAllText("Library/FactoryGuitarVerification.txt","RUNNING");
            FactoryMainMenu.SkipNextMenu=false;SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        static void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);}
        static void At(FactoryGuitarPerformance movie,float time){movie.Tick(Mathf.Max(0,time-movie.Elapsed));}
        static void Play(FactoryMainMenu menu){menu.Tick(1);menu.Choose(FactoryMenuAction.Play);menu.Tick(.3f);menu.Tick(1.2f);menu.Tick(.3f);menu.Tick(.6f);}
        static void Capture(Camera camera,FactoryGuitarPerformance movie,string name,bool fullBody=true)
        {
            var target=new RenderTexture(540,960,24);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=target;int outside=0;
                foreach(var renderer in (fullBody || movie.Shot==5?movie.Performer:movie.Guitar).GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer)continue;
                    var filter=renderer.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh)continue;
                    // Project the actual cutout, since an axis-aligned box includes empty corners around the leaning sheet.
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var v=camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                        if(v.z<=0 || v.x<.015f || v.x>.985f || v.y<.015f || v.y>.985f)outside++;
                    }
                }
                report.Add("DETAIL "+name+" outside="+outside);
                if(fullBody)Check(outside==0,name+" contains Nastya and the guitar in a vertical frame");
                else if(movie.Shot==5)Check(camera.fieldOfView>=100,name+" uses a wide-angle close-up with an intentional body crop");
                else Check(outside==0,name+" keeps the entire guitar visible in the instrument detail shot");
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);else camera.Render();
                RenderTexture.active=target;image=new Texture2D(540,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,540,960),0,0);image.Apply();
                Directory.CreateDirectory("Screenshots");var path=Path.GetFullPath("Screenshots/"+name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".png");
                File.WriteAllBytes(path,image.EncodeToPNG());File.WriteAllText("Library/"+name+"Capture.txt",path);
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Verify(CardboardFactoryGame game)
        {
            var sourceMesh=Resources.Load<GameObject>("CardboardCharacters/HumanNastya").GetComponent<MeshFilter>().sharedMesh;
            var sourceVertices=sourceMesh.vertices;
            var movie=game.guitarPerformance;var camera=Camera.main;var rig=camera.GetComponent<CardboardCamera>();
            var workers=game.crowd.runners.ToArray();var positions=workers.Select(w=>w.transform.position).ToArray();var enabled=workers.Select(w=>w.enabled).ToArray();
            var originalView=camera.transform.position;var originalPlayer=rig.player;float clock=game.ShiftRemaining,marks=game.Mistakes;bool audio=AudioListener.pause;
            var renderers=workers.SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).ToArray();var visible=renderers.Select(r=>r.enabled).ToArray();
            movie.enabled=false;
            try
            {
                Check(movie.Begin() && game.DanceActive && game.circleDance.SessionOnly,"Guitar recording starts directly from the main menu");
                Check(movie.Performer.GetComponentInChildren<MeshFilter>().sharedMesh.name.Contains("HumanNastya"),"The performer uses Nastya's original cardboard model");
                Check(Mathf.Abs(movie.Center.x-game.transform.TransformPoint(new Vector3(CardboardFactoryWorld.WorkshopCenterX,0,0)).x)<.001f && Mathf.Abs(movie.Center.z-game.transform.position.z)<.001f,"Nastya stands in the center of the workshop");
                Check(Time.timeScale==0 && AudioListener.pause && game.CameraOverride && game.PlayerBlocked,"Recording suspends the shift and owns the camera");
                Check(renderers.All(r=>!r.enabled),"Other workers are hidden for the solo performance");
                Check(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(f=>f.sharedMesh && (f.sharedMesh.name.ToLowerInvariant().Contains("guitar") || f.name.ToLowerInvariant().Contains("guitar")) && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled)==1,"Only the held guitar is visible, including when the source guitar was placed in the scene");
                var firstView=camera.transform.position;var firstPose=movie.Performer.localRotation;At(movie,3);
                Check(Vector3.Distance(firstView,camera.transform.position)>1 && Quaternion.Angle(firstPose,movie.Performer.localRotation)>.1f,"The first shot flies around Nastya while she sways with the guitar");
                Capture(camera,movie,"NastyaGuitarFirst");
                At(movie,movie.firstShotSeconds+.1f);Check(movie.Shot==1 && camera.transform.position.x>movie.Center.x,"The second shot starts from the opposite side");
                At(movie,9);Capture(camera,movie,"NastyaGuitarSecond");
                At(movie,movie.SoloAt-.01f);movie.Tick(.02f);
                Check(movie.Solo && movie.Shot==5 && camera.fieldOfView>=100,"The very first solo frame cuts straight to the wide-angle face close-up");
                At(movie,movie.SoloAt+.4f);var guitarPose=movie.Guitar.localRotation;var mesh=movie.Performer.GetComponentInChildren<MeshFilter>().sharedMesh;var bodyPose=movie.Performer.localRotation;
                movie.Tick(.1f);
                Check(Quaternion.Angle(guitarPose,movie.Guitar.localRotation)>.2f && Quaternion.Angle(bodyPose,movie.Performer.localRotation)>.1f,"The solo moves the intact figure and guitar as rigid objects");
                Check(mesh==sourceMesh && mesh.vertices.SequenceEqual(sourceVertices),"Nastya keeps the original imported mesh without changing any vertices");
                Check(movie.Face==FactoryFaceState.Angry,"The solo begins with the angry expression");
                At(movie,movie.SoloAt+movie.emotionSeconds+.05f);Check(movie.Face==FactoryFaceState.Surprised,"The angry face switches to surprise during the solo");
                Capture(camera,movie,"NastyaGuitarSurprised",false);
                At(movie,movie.SoloAt+movie.emotionSeconds*2+.05f);Check(movie.Face==FactoryFaceState.Angry,"The surprised face alternates back to anger");
                Capture(camera,movie,"NastyaGuitarAngry",false);
                Check(Vector3.Distance(new Vector3(movie.Performer.position.x,0,movie.Performer.position.z),new Vector3(movie.Center.x,0,movie.Center.z))<.001f,"Nastya stays planted at the center throughout the solo");
                At(movie,movie.SoloAt+2);
                var person=movie.Performer.GetComponentInChildren<FactoryWorkerAppearance>().transform;var faceCenter=person.TransformPoint(new Vector3(0,1.76f,0));
                Check(movie.Shot==5 && camera.fieldOfView>=100 && Vector3.Distance(camera.transform.position,faceCenter)<1,"The wide-angle camera comes right up to Nastya's face");
                var steady=Quaternion.LookRotation(faceCenter-camera.transform.position);
                Check(Quaternion.Angle(steady,camera.transform.rotation)>.1f,"The face close-up includes handheld angular shake");
                Capture(camera,movie,"NastyaGuitarFaceWide",false);
                At(movie,movie.SoloAt+4.5f);
                Check(movie.Shot==3 && camera.transform.position.y>movie.FloorHeight+3.8f && camera.transform.forward.y<-.8f,"The solo includes a high overhead camera angle");
                Capture(camera,movie,"NastyaGuitarTop");
                At(movie,movie.SoloAt+8);
                Check(movie.Shot==4 && camera.transform.position.y<movie.FloorHeight+.3f && camera.transform.forward.y>.35f,"The solo includes an upward angle from near the floor");
                Capture(camera,movie,"NastyaGuitarLow");
                At(movie,movie.SoloAt+15);Capture(camera,movie,"NastyaGuitarDetail",false);
                At(movie,movie.SoloAt+19);Check(movie.Shot==7 && camera.fieldOfView<60,"The montage returns to a wide view of the room");
                movie.Restart();bool grounded=true,unaltered=true;float lowestGap=float.PositiveInfinity,highestGap=float.NegativeInfinity;
                person=movie.Performer.GetComponentInChildren<FactoryWorkerAppearance>().transform;
                for(int frame=0;frame<260;frame++)
                {
                    movie.Tick(.24f);float lowest=sourceVertices.Min(v=>person.TransformPoint(v).y);float gap=lowest-movie.FloorHeight;
                    lowestGap=Mathf.Min(lowestGap,gap);highestGap=Mathf.Max(highestGap,gap);
                    if(gap<-.003f || gap>.005f)grounded=false;
                    if(movie.Performer.localScale!=Vector3.one || person.localScale!=Vector3.one || person.GetComponent<MeshFilter>().sharedMesh!=sourceMesh)unaltered=false;
                }
                Check(grounded,"The lowest foot stays on the visible floor throughout the complete performance");
                report.Add("DETAIL floor="+movie.FloorHeight+" foot gap min="+lowestGap+" max="+highestGap);
                Check(unaltered && sourceMesh.vertices.SequenceEqual(sourceVertices),"All shots preserve Nastya's original geometry and proportions");
                Check(game.ShiftRemaining==clock && game.Mistakes==marks,"Filming does not consume progress or add penalties");
                game.mainMenu.ReturnToMenu();float paused=movie.Elapsed;movie.Tick(2);rig.RefreshPlayerPose(0);
                Check(movie.Elapsed==paused && camera.transform.position==game.mainMenu.CameraPosition,"Escape pauses the take and shows the menu camera");
                Play(game.mainMenu);movie.Tick(.1f);Check(movie.Active && movie.Elapsed>paused && !game.MenuActive,"Play resumes the same take");
                Check(movie.Restart() && movie.Elapsed==0 && movie.Shot==0,"The retake restarts both camera flights and the solo timeline");
                movie.Stop();
                Check(!movie.Active && !game.DanceActive && game.MenuActive,"Stopping the performance returns to the main menu");
                Check(workers.Select((w,i)=>w.transform.position==positions[i] && w.enabled==enabled[i]).All(v=>v) &&
                    renderers.Select((r,i)=>r.enabled==visible[i]).All(v=>v),"Every original worker and visibility state is restored");
                Check(rig.player==originalPlayer && camera.transform.position==originalView && AudioListener.pause==audio,"The original camera, player and audio state are restored");
            }
            finally{if(movie.Active)movie.Stop();movie.enabled=true;}
        }
        static void Update()
        {
            if(!EditorApplication.isPlaying || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            if(SessionState.GetBool(Recording,false)){SessionState.SetBool(Recording,false);CardboardFactoryGame.Instance.guitarPerformance.Begin();}
            if(!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);
            try{Verify(CardboardFactoryGame.Instance);}catch(Exception error){report.Add("FAIL "+error);}
            File.WriteAllLines("Library/FactoryGuitarVerification.txt",new[]{"RESULT: "+report.Count(r=>r.StartsWith("PASS"))+" PASS, "+report.Count(r=>r.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;
        }
    }
}