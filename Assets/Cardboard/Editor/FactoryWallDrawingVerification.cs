using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryWallDrawingVerification
    {
        const string Pending="Cardboard.WallDrawingVerification";
        static readonly List<string> report=new List<string>();
        static double started;
        static FactoryWallDrawingVerification(){EditorApplication.update+=Tick;}
        public static void Batch()
        {
            EditorSceneManager.OpenScene(CardboardFactorySceneBuilder.ScenePath);FactoryWallDrawingInstaller.ApplyCurrent();Run();
        }
        [MenuItem("Cardboard/Verify marker drawing on walls")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();started=EditorApplication.timeSinceStartup;File.WriteAllText("Library/FactoryWallDrawingVerification.txt","RUNNING");
            SessionState.SetBool(Pending,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string message){report.Add((condition?"PASS ":"FAIL ")+message);}
        static Vector3 Point(CardboardDrawingSurface surface,float u=.5f,float v=.35f)=>surface.transform.TransformPoint(new Vector3(u-.5f,v-.5f,0));
        static void Aim(CardboardDrawingSurface surface,Vector3 target)
        {
            Camera.main.transform.SetPositionAndRotation(target+surface.transform.forward*.85f,Quaternion.LookRotation(-surface.transform.forward));
            Physics.SyncTransforms();
        }
        static void Stroke(CardboardMarker marker,CardboardDrawingSurface surface,Vector3 from,Vector3 to)
        {
            for(int i=0;i<=12;i++)
            {
                var screen=Camera.main.WorldToScreenPoint(Vector3.Lerp(from,to,i/12f));marker.TracePointer(new Vector2(screen.x,screen.y),true);
            }
            var end=Camera.main.WorldToScreenPoint(to);marker.TracePointer(new Vector2(end.x,end.y),false);
        }
        static void PressC(CardboardMarker marker,Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));InputSystem.Update();marker.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();marker.SendMessage("Update");
        }
        static Color32[] Frame()
        {
            var target=new RenderTexture(640,400,24);var active=RenderTexture.active;var texture=new Texture2D(640,400,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera.main,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,640,400),0,0);texture.Apply();return texture.GetPixels32();
            }
            finally{RenderTexture.active=active;Object.DestroyImmediate(texture);target.Release();Object.DestroyImmediate(target);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.timeSinceStartup-started>60){Check(false,"Verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player || Time.time<.3f)return;
            SessionState.SetBool(Pending,false);Keyboard keyboard=null;var previousKeyboard=Keyboard.current;
            try
            {
                var game=CardboardFactoryGame.Instance;game.ResetShift();game.enabled=false;game.mainMenu.enabled=false;game.intro.enabled=false;
                foreach(var runner in game.crowd.runners)runner.enabled=false;
                Camera.main.GetComponent<CardboardCamera>().enabled=false;
                var marker=Camera.main.GetComponent<CardboardMarker>();
                var hand=Camera.main.GetComponent<CardboardMarkerHand>();
                var walls=game.GetComponentsInChildren<CardboardWallDrawing>();
                Check(walls.Length>=15 && walls.All(w=>w.Faces!=null && w.Faces.Length==4),"Workshop, canteen, corridor and doorway walls have independent vertical drawing faces");
                Check(walls.SelectMany(w=>w.Faces).All(f=>!f.DrawingTexture && !f.GetComponent<Renderer>().enabled),"Wall textures allocate only when first painted and empty canvases do not render");
                marker.SetDrawingMode(true,false);
                var wall=walls.First(w=>w.name=="Canteen south wall");var face=wall.Faces[0];var point=Point(face);
                var wallRenderer=wall.GetComponent<MeshRenderer>();var materials=wallRenderer.sharedMaterials;var source=materials[0].mainTexture;
                Aim(face,point);hand.RefreshPose(.02f);hand.enabled=false;
                var before=Frame();marker.colorIndex=2;marker.radius=.035f;
                Stroke(marker,face,point-face.transform.right*.3f,point+face.transform.right*.3f);
                var screen=Camera.main.WorldToScreenPoint(point);marker.TracePointer(new Vector2(screen.x,screen.y),true);hand.enabled=true;hand.RefreshPose(.02f);
                Check(marker.Target==face && marker.IsTouching && Vector3.Distance(marker.ContactPoint,point)<.01f,"The ordinary marker pointer touches and paints the canteen wall");
                Check(Vector3.Distance(hand.Tip.position,marker.ContactPoint)<.015f,"Visible marker nib meets the wall ink contact");
                hand.enabled=false;var after=Frame();int changed=0;
                for(int i=0;i<before.Length;i++)if(Math.Abs(before[i].r-after[i].r)+Math.Abs(before[i].g-after[i].g)+Math.Abs(before[i].b-after[i].b)>12)changed++;
                Check(changed>100,"GPU renders the ink over the original wall texture; changed pixels="+changed);
                var texture=face.DrawingTexture;var ink=texture.GetPixelBilinear(.5f,.35f);
                Check(ink.b>ink.r+.25f && ink.a>.5f,"Selected blue marker ink appears in the wall canvas");
                Check(wallRenderer.sharedMaterials.SequenceEqual(materials) && materials[0].mainTexture==source,"Painting preserves all authored wall materials and source textures");
                Check(wall.Faces.Skip(1).All(f=>!f.DrawingTexture) && walls.Where(w=>w!=wall).SelectMany(w=>w.Faces).All(f=>!f.DrawingTexture),"Ink does not repeat on the back, wall ends, other panels or other walls");
                keyboard=InputSystem.AddDevice<Keyboard>();PressC(marker,keyboard);
                Check(marker.Erasing && Vector4.Distance(ink,texture.GetPixelBilinear(.5f,.35f))<.01f,"Pressing C equips the cloth without erasing any existing ink");
                hand.enabled=true;hand.RefreshPose(.3f);
                Check(hand.ClothVisible && !hand.Tip.Find("Marker and grip").gameObject.activeSelf,"The character visibly holds a cloth instead of the marker");
                marker.TracePointer(new Vector2(screen.x,screen.y),true);hand.RefreshPose(.02f);
                Check(texture.GetPixelBilinear(.5f,.35f).a<.01f && texture.GetPixelBilinear(.5f+.22f/face.physicalSize.x,.35f).a>.5f,
                    "The cloth erases only its contact patch and preserves ink elsewhere on the same wall");
                Check(Vector3.Distance(hand.Tip.position,marker.ContactPoint)<.015f && Vector3.Dot(hand.Tip.forward,-marker.ContactNormal)>.98f,
                    "The cloth rests flat against the surface at the actual wiping contact");
                CardboardFactorySceneBuilder.Capture("ClothWallWiping");
                PressC(marker,keyboard);hand.RefreshPose(.3f);
                Check(!marker.Erasing && !hand.ClothVisible && texture.GetPixelBilinear(.5f,.35f).a<.01f,"C returns to the marker without changing or restoring the wiped drawing");
                var other=walls.First(w=>w.name=="West workshop wall").Faces[2];var otherPoint=Point(other,.5f,.4f);Aim(other,otherPoint);
                marker.colorIndex=1;Stroke(marker,other,otherPoint-other.transform.right*.2f,otherPoint+other.transform.right*.2f);
                Check(other.DrawingTexture && other.DrawingTexture.GetPixelBilinear(.5f,.4f).r>.4f,"Rotated workshop walls accept the same colored marker strokes");
                Aim(face,point);marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),false);
                marker.SetErasing(true);Stroke(marker,face,point-face.transform.right*.4f,point+face.transform.right*.4f);marker.SetErasing(false);
                Check(!face.GetComponent<Renderer>().enabled && face.DrawingTexture.GetPixelBilinear(.5f,.35f).a==0,"The cloth wipes the ink along its path and restores the visible wall artwork");
                Check(other.DrawingTexture.GetPixelBilinear(.5f,.4f).a>.5f,"Clearing one wall preserves drawings on another wall");
                Aim(face,point);Camera.main.transform.position+=face.transform.forward*2;
                marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),true);
                Check(marker.Target==null && !marker.IsTouching,"Walls beyond the ordinary marker reach cannot be painted");
                Aim(face,point);var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position=point+face.transform.forward*.4f;blocker.transform.localScale=Vector3.one*.25f;Physics.SyncTransforms();
                marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),true);
                Check(marker.Target==null,"Solid objects in front of a wall block marker strokes");Object.DestroyImmediate(blocker);
                Aim(face,point);marker.colorIndex=2;
                Stroke(marker,face,point-face.transform.right*.25f,point+face.transform.right*.25f);
                Stroke(marker,face,point+Vector3.up*.18f-face.transform.right*.25f,point+Vector3.up*.18f+face.transform.right*.25f);
                marker.colorIndex=1;Stroke(marker,face,point-Vector3.up*.2f-face.transform.right*.25f,point-Vector3.up*.2f+face.transform.right*.25f);
                Check(face.GetComponent<Renderer>().enabled && face.DrawingTexture.GetPixelBilinear(.5f,.35f).a>.5f,"Drawing again after wiping makes the wall ink visible again");
                marker.TracePointer(new Vector2(Screen.width*.5f,Screen.height*.5f),false);hand.enabled=true;hand.RefreshPose(.02f);
                CardboardFactorySceneBuilder.Capture("MarkerWallDrawing");
                // Existing character painting still uses its supplied UV atlas and restores it on clear.
                var person=game.GetComponentsInChildren<CardboardDrawingSurface>().First(s=>!s.transparentInk && s.Runner && !s.Runner.isPlayer);
                var original=(Texture2D)person.GetComponent<Renderer>().sharedMaterial.mainTexture;var baseline=original.GetPixelBilinear(.22f,.45f);
                person.PaintStroke(new Vector2(.22f,.45f),new Vector2(.23f,.45f),Color.magenta,.03f);
                Check(person.DrawingTexture && person.DrawingTexture!=original,"Existing character drawing continues to use a private UV atlas");
                person.PaintStroke(new Vector2(.55f,.45f),new Vector2(.55f,.45f),Color.blue,.03f);
                var distantInk=person.DrawingTexture.GetPixelBilinear(.55f,.45f);
                person.EraseStroke(new Vector2(.225f,.45f),new Vector2(.225f,.45f),.06f);
                Check(Vector4.Distance(baseline,person.DrawingTexture.GetPixelBilinear(.22f,.45f))<.01f &&
                    Vector4.Distance(distantInk,person.DrawingTexture.GetPixelBilinear(.55f,.45f))<.01f,
                    "Wiping a character restores its supplied artwork only under the cloth");
                var appearance=person.GetComponent<FactoryWorkerAppearance>();
                if(appearance && appearance.angry)
                {
                    appearance.SetState(FactoryFaceState.Angry);person.EraseStroke(new Vector2(.55f,.45f),new Vector2(.55f,.45f),.06f);
                    var current=(Texture2D)appearance.angry.mainTexture;
                    Check(Vector4.Distance(current.GetPixelBilinear(.55f,.45f),person.DrawingTexture.GetPixelBilinear(.55f,.45f))<.01f,
                        "Wiping after a facial expression change restores the current face texture");
                    appearance.SetState(FactoryFaceState.Normal);
                }
                person.ClearDrawing();Check(Vector4.Distance(baseline,person.DrawingTexture.GetPixelBilinear(.22f,.45f))<.01f,"Clearing a character still restores the original atlas");
                marker.SetErasing(true);marker.SetDrawingMode(false,false);hand.RefreshPose(.02f);
                Check(!marker.Erasing && !hand.Visible,"Putting tools away hides the cloth and resets the tool selection");
            }
            catch(Exception error){Check(false,error.ToString());Debug.LogException(error);}
            finally
            {
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null)previousKeyboard.MakeCurrent();Finish();
            }
        }
        static void Finish()
        {
            SessionState.SetBool(Pending,false);
            File.WriteAllLines("Library/FactoryWallDrawingVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));
            EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.Exit(report.Any(l=>l.StartsWith("FAIL"))?1:0);
        }
    }
}
