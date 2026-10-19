using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    public static class FactoryNewAssetsVerification
    {
        static Keyboard keyboard;
        static Mouse mouse,previousMouse;
        static CardboardCamera view;
        static Vector3 playerHome;
        static Quaternion cameraHome;
        static bool cameraEnabled;
        static int step,lastFrame;
        static float since,started;
        static void Send(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        static void RightMouse(bool pressed)=>InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right,pressed));
        static void Next(int value){step=value;since=Time.unscaledTime;}
        public static void Reset(){Cleanup();step=lastFrame=0;since=started=0;}
        public static void Cleanup()
        {
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
            if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);mouse=null;
            if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();previousMouse=null;
            if(view)view.enabled=cameraEnabled;view=null;
        }
        static void CheckUV(string source,string generated,Action<bool,string> check)
        {
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(source).GetComponentInChildren<MeshFilter>().sharedMesh;
            var baked=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Cardboard/Generated/"+generated+".asset");
            check(baked && imported.uv.SequenceEqual(baked.uv),"Authored UVs preserved without remapping: "+generated);
        }
        public static void CheckAssets(CardboardFactoryGame game,Action<bool,string> check)
        {
            for(int i=1;i<=3;i++)CheckUV("Assets/Game_Assets/Buttons/"+i+"/"+i+".fbx","MenuButton"+i,check);
            CheckUV("Assets/Game_Assets/Buttons/Settings/Podloshka.fbx","MenuSettingsBacking",check);
            CheckUV("Assets/Game_Assets/Buttons/Settings/Back_Button.fbx","MenuBackButton",check);
            CheckUV("Assets/Game_Assets/Buttons/Volume_Bar/Volume_Bar.fbx","MenuVolumeBar",check);
            CheckUV("Assets/Game_Assets/Buttons/Volume_Bar/Volume_Gear.fbx","MenuVolumeGear",check);
            CheckUV("Assets/Game_Assets/Cardboard/Instrucion/Instruction.fbx","InstructionSheet",check);
            CheckUV("Assets/Game_Assets/Cardboard/Press/Model/Press.fbx","TexturedPress",check);
            CheckUV("Assets/Game_Assets/Cardboard/Under_Press/Model/Under_Press.fbx","TexturedUnderPress",check);
            foreach(var station in game.stations)
            {
                var press=station.press.GetComponent<MeshRenderer>();var bed=station.transform.Find("Under press").GetComponent<MeshRenderer>();
                var local=station.transform.InverseTransformPoint(press.bounds.center);
                check(press.sharedMaterial.GetTexture("_BaseMap") && bed.sharedMaterial.GetTexture("_BaseMap"),
                    "Both new press parts have authored base textures: "+station.name);
                check(Mathf.Abs(local.x-station.cuttingPosition.x)<.005f && Mathf.Abs(local.z-station.cuttingPosition.z)<.005f,
                    "Press centered over blank: "+station.name);
                float top=station.transform.InverseTransformPoint(new Vector3(bed.bounds.center.x,bed.bounds.max.y,bed.bounds.center.z)).y;
                check(Mathf.Abs(top-(station.cuttingPosition.y-station.sheet.localScale.y*.5f))<.005f,
                    "New under-press supports bottom of blank: "+station.name);
            }
            var material=game.instruction.sheet.GetComponent<Renderer>().sharedMaterial;
            check(material.GetFloat("_Cull")==0 && material.GetFloat("_Smoothness")==0 && material.GetColor("_BaseColor")==Color.white,
                "Instruction is double-sided, untinted and fully rough");
        }
        public static bool Tick(CardboardFactoryGame game,Action<bool,string> check)
        {
            if(started==0)started=Time.realtimeSinceStartup;
            if(Time.realtimeSinceStartup-started>12)throw new InvalidOperationException("Instruction input timed out at "+step);
            if(lastFrame==Time.frameCount)return false;lastFrame=Time.frameCount;
            var paper=game.instruction;
            if(!paper)throw new InvalidOperationException("Install new assets before verification.");
            if(step==0)
            {
                CheckAssets(game,check);
                keyboard=InputSystem.AddDevice<Keyboard>("Instruction verification keyboard");
                previousMouse=Mouse.current;mouse=InputSystem.AddDevice<Mouse>("Instruction verification mouse");
                view=Camera.main.GetComponent<CardboardCamera>();cameraEnabled=view.enabled;view.enabled=false;
                playerHome=CardboardRunner.Player.transform.position;cameraHome=Camera.main.transform.rotation;
                var body=CardboardRunner.Player.GetComponent<CharacterController>();body.enabled=false;
                CardboardRunner.Player.transform.position=paper.sheet.position+Vector3.back*.7f-Vector3.up*paper.sheet.position.y;body.enabled=true;
                var eyes=CardboardRunner.Player.transform.position+Vector3.up*1.62f;
                Camera.main.transform.SetPositionAndRotation(eyes,Quaternion.LookRotation(paper.sheet.position-eyes));
                Physics.SyncTransforms();
                check(game.TargetForRay(new Ray(eyes,(paper.sheet.position-eyes).normalized))?.action==FactoryAction.Instruction,
                    "Floor instruction is reachable by normal E interaction ray");
                CardboardFactorySceneBuilder.Capture("InstructionOnFloor");
                Send(Key.E);Next(1);
            }
            else if(step==1 && Time.unscaledTime-since>.12f){Send();Next(2);}
            else if(step==2 && Time.unscaledTime-since>.5f)
            {
                check(paper.Held && !paper.Reverse && !paper.Animating && paper.sheet.parent==paper.transform,
                    "Real E picks up instruction on its main side without flipping in same frame");
                check(game.HandsBusy && !game.PlayerBlocked && paper.pickup.enabled && !paper.physical.Body.useGravity,
                    "Held instruction blocks machine and drawing, allows walking and keeps physical collision");
                check(!game.station.Operate(StationPart.Feed),"Reading paper cannot operate feed lever");
                CardboardFactorySceneBuilder.Capture("InstructionFront");
                Send(Key.E);Next(3);
            }
            else if(step==3 && Time.unscaledTime-since>.12f){Send();Next(4);}
            else if(step==4 && Time.unscaledTime-since>.5f)
            {
                check(paper.Reverse && !paper.Animating && Quaternion.Angle(paper.physical.Body.rotation,Camera.main.transform.rotation*Quaternion.Euler(0,180,0))<4,
                    "Real E physically turns instruction to reverse");
                CardboardFactorySceneBuilder.Capture("InstructionBack");
                Send(Key.E);Next(5);
            }
            else if(step==5 && Time.unscaledTime-since>.12f){Send();Next(6);}
            else if(step==6 && Time.unscaledTime-since>.5f)
            {
                check(!paper.Reverse && Quaternion.Angle(paper.physical.Body.rotation,Camera.main.transform.rotation)<4,
                    "Next E returns to main side");
                RightMouse(true);Next(7);
            }
            else if(step==7 && Time.unscaledTime-since>.12f){RightMouse(false);Next(8);}
            else if(step==8 && Time.unscaledTime-since>.2f)
            {
                check(!paper.Held && !game.HandsBusy && paper.pickup.enabled && paper.physical.Body.useGravity && paper.sheet.parent==paper.transform,
                    "Real RMB releases instruction to gravity and restores normal interactions");
                var body=CardboardRunner.Player.GetComponent<CharacterController>();body.enabled=false;
                CardboardRunner.Player.transform.position=playerHome;body.enabled=true;
                Camera.main.transform.rotation=cameraHome;
                Cleanup();game.ResetShift();return true;
            }
            return false;
        }
    }
}
