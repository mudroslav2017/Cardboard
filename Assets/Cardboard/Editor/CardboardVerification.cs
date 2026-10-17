using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    // Small integration check that runs against the real Unity player loop.
    [InitializeOnLoad]
    public static class CardboardVerification
    {
        static readonly List<string> results = new List<string>();
        static bool running;
        static double started;
        static int stage;
        static Keyboard keyboard;
        static Mouse mouse;
        static Vector3 npcStart, playerStart;
        static float walkSpeed;
        static CardboardRunner player;
        static CardboardRunner npc;
        static bool previousBackground;
        static InputSettings.BackgroundBehavior previousInputBackground;
        static CardboardVerification() { EditorApplication.update += Tick; }

        [MenuItem("Cardboard/Verify movement and controls")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            results.Clear(); stage=0; running=true;
            previousBackground=Application.runInBackground;
            previousInputBackground=InputSystem.settings.backgroundBehavior;
            Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool("CardboardVerification",true);
            EditorApplication.isPlaying=true;
        }

        static void Check(bool condition,string message)
        {
            results.Add((condition ? "PASS " : "FAIL ")+message);
        }

        static void Tick()
        {
            // File commands allow tooling to request the same explicit menu actions.
            const string request="Library/CardboardEditorCommand.txt";
            if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                string command=File.ReadAllText(request).Trim(); File.Delete(request);
                if (command=="build") { CardboardSceneBuilder.Build(); return; }
                if (command=="preview") { CardboardSceneBuilder.Preview(); return; }
                if (command=="verify") { Verify(); return; }
                if (command=="play") { EditorApplication.isPlaying=true; return; }
            }
            if (!running && SessionState.GetBool("CardboardVerification",false)) { running=true; stage=0; }
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (stage==0)
                {
                    player=CardboardRunner.Player;
                    if (!player || !CardboardWorld.Instance) return;
                    var runners=CardboardWorld.Instance.runners;
                    Check(runners.Count==12,"First-person player and 11 cardboard cutouts");
                    foreach(var runner in runners)
                    {
                        Check(runner && runner.puppet && runner.puppet.GetComponentsInChildren<MeshFilter>(true).Length==1,"One single sheet: "+runner.name);
                        if(runner && runner.puppet)
                        {
                            runner.Pose(0,1);float ground=runner.puppet.localPosition.y;
                            runner.Pose(Mathf.PI*.5f,1);
                            float left=runner.puppet.localEulerAngles.z;
                            if(!runner.isPlayer) Check(runner.puppet.localPosition.y>ground+.13f,"Whole sheet hops while walking: "+runner.name);
                            runner.Pose(Mathf.PI*1.5f,1);
                            float right=runner.puppet.localEulerAngles.z;
                            Check(Mathf.DeltaAngle(left,right)<-10,"Whole sheet sways left and right: "+runner.name);
                        }
                    }
                    Check(Camera.main && !Camera.main.orthographic,"Perspective first-person camera");
                    Check(player.puppet && !player.puppet.gameObject.activeSelf,"Local player silhouette is hidden");
                    npc=runners[1]; npcStart=npc.transform.position; playerStart=player.transform.position;
                    var surface=npc.GetComponentInChildren<CardboardDrawingSurface>();
                    Check(surface && surface.GetComponent<MeshCollider>(),"NPC has a drawable mesh collider");
                    if(surface)
                    {
                        Physics.SyncTransforms();
                        Vector3 origin=surface.transform.TransformPoint(new Vector3(0,1.17f,1));
                        bool hit=Physics.Raycast(origin,-surface.transform.forward,out RaycastHit contact,1.1f);
                        Check(hit && contact.collider==surface.GetComponent<MeshCollider>() && Mathf.Abs(contact.textureCoord.y-.5f)<.02f,"Raycast maps cardboard surface to drawing UV");
                        var source=(Texture2D)surface.GetComponent<MeshRenderer>().sharedMaterials[0].mainTexture;
                        Color baseline=source.GetPixel(source.width/2,source.height/2);
                        var shared=runners[7].GetComponentInChildren<CardboardDrawingSurface>();
                        var neighborTexture=shared.GetComponent<MeshRenderer>().sharedMaterials[0].mainTexture;
                        surface.PaintStroke(new Vector2(.42f,.5f),new Vector2(.58f,.5f),Color.magenta,.018f);
                        var painted=surface.DrawingTexture;
                        Check(painted && painted!=source,"Drawing uses an individual runtime texture");
                        Color middle=painted.GetPixel(painted.width/2,painted.height/2);
                        Check(middle.r>.8f && middle.b>.8f && middle.g<.2f,"Marker leaves visible colored ink");
                        Color between=painted.GetPixelBilinear(.53f,.5f);
                        Check(between.b>.8f && between.g<.2f,"Continuous marker stroke has no gaps");
                        Check(shared.GetComponent<MeshRenderer>().sharedMaterials[0].mainTexture==neighborTexture && source.GetPixel(source.width/2,source.height/2)==baseline,"Painting does not modify matching neighbors or source assets");
                        surface.ClearDrawing();
                        Check(painted.GetPixel(painted.width/2,painted.height/2)==baseline,"Clear restores original cardboard drawing");
                        var rearSource=(Texture2D)surface.GetComponent<MeshRenderer>().sharedMaterials[1].mainTexture;
                        Check(rearSource!=source && surface.GetComponent<MeshFilter>().sharedMesh.subMeshCount==3,"Separate front, rear and cardboard edge surfaces");
                        origin=surface.transform.TransformPoint(new Vector3(0,1.17f,-1));
                        hit=Physics.Raycast(origin,surface.transform.forward,out contact,1.1f);
                        Check(hit && contact.collider==surface.GetComponent<MeshCollider>() && CardboardDrawingSurface.SideOf(contact)==1,"Rear side can be raycast for drawing");
                        Color frontBefore=painted.GetPixel(painted.width/2,painted.height/2);
                        Color rearBefore=rearSource.GetPixel(rearSource.width/2,rearSource.height/2);
                        surface.PaintStroke(new Vector2(.42f,.5f),new Vector2(.58f,.5f),Color.blue,.018f,1);
                        var rear=surface.BackDrawingTexture;
                        Check(rear && rear!=painted && rear.GetPixel(rear.width/2,rear.height/2).b>.8f,"Marker draws onto the rear face");
                        Check(painted.GetPixel(painted.width/2,painted.height/2)==frontBefore,"Rear drawing does not bleed onto the front");
                        surface.ClearDrawing();
                        Check(rear.GetPixel(rear.width/2,rear.height/2)==rearBefore,"Clear also restores the rear face");
                    }
                    keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                    started=Time.time; stage=1;
                }
                double elapsed=Time.time-started;
                if (stage==1)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
                    if (elapsed<1.3) return;
                    walkSpeed=player.speed;
                    Check(Vector3.Distance(playerStart,player.transform.position)>1,"WASD moves the player relative to camera");
                    Check(Vector3.Distance(npcStart,npc.transform.position)>.5f,"NPC runs autonomously");
                    Check(npc.Velocity.sqrMagnitude>.1f && Vector3.Dot(npc.transform.forward,npc.Velocity.normalized)>.95f,"NPC faces its walking direction");
                    playerStart=player.transform.position; started=Time.time; stage=2;
                }
                else if (stage==2)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.LeftShift));
                    if (elapsed<1.3) return;
                    // Measure the locomotion speed: the island boundary intentionally clamps position.
                    Check(player.speed>5.8f && player.speed>walkSpeed*1.3f,"Shift increases speed");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space,Key.E,Key.R));
                    started=Time.time; stage=3;
                }
                else if (stage==3)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    if (elapsed<.25) return;
                    Check(player.JumpHeight>.2f,"Space jumps");
                    Check(Mathf.Abs(Camera.main.transform.position.y-player.transform.position.y-1.62f-player.JumpHeight)<.05f,"Camera follows eye height and jump");
                    Check(CardboardRunner.FollowUntil>Time.time,"E summons the crowd");
                    Check(CardboardRunner.PartyUntil>Time.time,"R starts the funny panic run");
                    started=Time.time; stage=4;
                }
                else if (stage==4 && elapsed>1.5)
                {
                    Check(player.JumpHeight<.05f,"Player lands after jumping");
                    foreach(var runner in CardboardWorld.Instance.runners)
                    {
                        Vector3 p=runner.transform.position;
                        Check(p.x*p.x/(10.7f*10.7f)+p.z*p.z/(7.2f*7.2f)<1.01f,"Runner stays on island: "+runner.name);
                    }
                    npc.PauseFor(5);npcStart=npc.transform.position;
                    started=Time.time;stage=5;
                }
                else if(stage==5 && elapsed>4.7)
                {
                    Check(npc.IsResting && Vector3.Distance(npcStart,npc.transform.position)<.01f,"NPC stands still for five seconds");
                    Check(npc.speed<.01f,"Resting NPC stops swaying with locomotion");
                    stage=6;
                }
                else if(stage==6 && elapsed>5.8)
                {
                    Check(!npc.IsResting && Vector3.Distance(npcStart,npc.transform.position)>.2f,"NPC resumes walking after its rest");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));
                    started=Time.time;stage=7;
                }
                else if(stage==7 && elapsed>.25)
                {
                    Check(CardboardMarker.DrawingMode && Cursor.lockState==CursorLockMode.None,"F enables free-cursor marker mode");
                    var marker=CardboardMarker.Instance;
                    var hand=Camera.main.GetComponent<CardboardMarkerHand>();
                    Check(hand && hand.Visible && hand.Palm,"Visible hand grips the equipped marker");
                    marker.TracePointer(new Vector2(Screen.width*.35f,Screen.height*.35f),false);hand.RefreshPose(1);
                    Vector3 first=Camera.main.WorldToViewportPoint(hand.Tip.position);
                    marker.TracePointer(new Vector2(Screen.width*.65f,Screen.height*.65f),false);hand.RefreshPose(1);
                    Vector3 second=Camera.main.WorldToViewportPoint(hand.Tip.position);
                    Check(second.x-first.x>.25f && second.y-first.y>.25f,"Marker follows mouse horizontally and vertically");
                    npc.PauseFor(5);npc.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);npc.Pose(0,0);
                    player.transform.position=new Vector3(0,0,1.2f);
                    Camera.main.transform.SetPositionAndRotation(player.transform.position+Vector3.up*1.62f,Quaternion.Euler(0,180,0));
                    var surface=npc.GetComponentInChildren<CardboardDrawingSurface>();
                    Vector3 screen=Camera.main.WorldToScreenPoint(surface.transform.TransformPoint(new Vector3(0,1.22f,.0225f)));
                    marker.TracePointer(new Vector2(screen.x,screen.y),true);hand.RefreshPose(1);
                    Check(marker.IsTouching && marker.Target==surface,"Held marker makes physical contact with a nearby NPC");
                    Check(Vector3.Distance(hand.Tip.position,marker.ContactPoint)<.005f,"Visible nib touches the exact ink position");
                    Check(npc.IsResting,"NPC holds its pose during marker contact");
                    // Render a complete player frame with actual mouse input before capturing the hand.
                    mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
                    InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(screen.x,screen.y),buttons=1});
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit3));
                    started=Time.time;stage=8;
                }
                else if(stage==8 && elapsed>.25)
                {
                    Check(CardboardMarker.Instance && CardboardMarker.Instance.colorIndex==2,"Number keys select marker colors");
                    var marker=CardboardMarker.Instance;
                    var hand=Camera.main.GetComponent<CardboardMarkerHand>();
                    Check(marker.IsTouching && marker.Target && marker.Target.Runner==npc,"Mouse button input draws through the equipped marker");
                    CardboardSceneBuilder.Preview();
                    File.Copy("Screenshots/CardboardPreview.png","Screenshots/MarkerInHand.png",true);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));
                    started=Time.time;stage=9;
                }
                else if(stage==9 && elapsed>.25)
                {
                    Check(!CardboardMarker.DrawingMode,"F returns to first-person walking");
                    Finish();
                }
            }
            catch(Exception error) { results.Add("FAIL "+error); Finish(); }
        }
        static void Finish()
        {
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if(mouse!=null && mouse.added) InputSystem.RemoveDevice(mouse);
            mouse=null;
            keyboard=null; running=false; SessionState.SetBool("CardboardVerification",false);
            File.WriteAllLines("Library/CardboardVerification.txt",results);
            InputSystem.settings.backgroundBehavior=previousInputBackground;
            Application.runInBackground=previousBackground;
            EditorApplication.isPlaying=false;
        }
    }
}
