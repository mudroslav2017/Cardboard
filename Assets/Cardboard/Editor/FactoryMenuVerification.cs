using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    public static class FactoryMenuVerification
    {
        static Mouse mouse;
        static int step,lastFrame;
        static float since,started,initialClock,originalVolume;
        static Vector3 workerBefore;
        static bool black,leaving;
        static Vector2 Screen(FactoryMainMenu menu,Vector3 world)=>menu.lens.WorldToScreenPoint(world);
        static void Pointer(FactoryMainMenu menu,Vector3 world,bool down)
        {InputSystem.QueueStateEvent(mouse,new MouseState{position=Screen(menu,world),buttons=(ushort)(down?1:0)});}
        static void Next(int state){step=state;since=Time.unscaledTime;}
        public static void Reset(){Cleanup();step=lastFrame=0;since=started=0;black=leaving=false;}
        public static void Cleanup(){if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);mouse=null;}
        public static bool Tick(CardboardFactoryGame game,Action<bool,string> check)
        {
            var menu=game.mainMenu;
            if(!menu)throw new InvalidOperationException("Reapply the presentation update: menu is missing.");
            if(started==0)started=Time.realtimeSinceStartup;
            if(Time.realtimeSinceStartup-started>24)throw new InvalidOperationException("Menu input timed out at step "+step+", phase "+menu.Phase);
            if(lastFrame==Time.frameCount)return false;lastFrame=Time.frameCount;
            if(step==0 && menu.Phase==FactoryMenuPhase.Main)
            {
                mouse=InputSystem.AddDevice<Mouse>("Factory menu verification pointer");
                originalVolume=menu.Volume;initialClock=game.ShiftRemaining;workerBefore=game.crowd.runners[8].transform.position;
                check(game.MenuActive && game.PlayerBlocked && game.CameraOverride && Cursor.visible,"Main menu owns camera and pointer, with player controls blocked");
                check(menu.buttons.Length==3 && !menu.settingsPanel.gameObject.activeSelf && menu.shiftHud.alpha==0,"Three imported menu buttons replace shift HUD at startup");
                CardboardFactorySceneBuilder.Capture("FactoryMainMenu");
                Pointer(menu,menu.buttons[0].hit.bounds.center+menu.buttons[0].transform.right*.35f,false);Next(1);
            }
            else if(step==1 && Time.unscaledTime-since>.55f)
            {
                check(menu.buttons[0].Hovered && (menu.buttons[0].transform.localPosition-menu.buttons[0].home).magnitude>.01f,"Real pointer attracts hovered hanging button toward cursor");
                check(Vector3.Distance(workerBefore,game.crowd.runners[8].transform.position)>.01f,"Cardboard workers keep walking in the menu background");
                check(game.ShiftRemaining==initialClock && game.Mistakes==0 && !game.Inspection,"Menu time does not consume shift or trigger inspection and mistakes");
                Pointer(menu,menu.buttons[0].hit.bounds.center,true);Next(2);
            }
            else if(step==2 && Time.unscaledTime-since>.13f)
            {
                check(menu.buttons[0].PressTravel>.1f && menu.Phase==FactoryMenuPhase.Pressing,"Real click depresses and swings selected button before leaving");
                Pointer(menu,menu.buttons[0].hit.bounds.center,false);Next(3);
            }
            else if(step==3 && menu.Phase==FactoryMenuPhase.Settings)
            {
                check(menu.settingsPanel.gameObject.activeSelf && !menu.buttons[0].hit.enabled,"Settings panel enters after main buttons lift away");
                CardboardFactorySceneBuilder.Capture("FactoryMenuSettings");
                Pointer(menu,menu.volumeTrack.transform.TransformPoint(new Vector3(-.8f,0,0)),true);Next(4);
            }
            else if(step==4 && Time.unscaledTime-since>.13f)
            {Pointer(menu,menu.volumeTrack.transform.TransformPoint(new Vector3(.65f,0,0)),true);Next(5);}
            else if(step==5 && Time.unscaledTime-since>.13f)
            {Pointer(menu,menu.volumeTrack.transform.TransformPoint(new Vector3(.65f,0,0)),false);Next(6);}
            else if(step==6 && Time.unscaledTime-since>.13f)
            {
                check(menu.Volume>.76f && menu.Volume<.86f && Mathf.Abs(AudioListener.volume-menu.Volume)<.001f,"Real slider drag changes audible master volume");
                check(Mathf.Abs(PlayerPrefs.GetFloat(FactoryMainMenu.VolumeKey)-menu.Volume)<.001f,"Volume saves on slider release");
                Pointer(menu,menu.back.hit.bounds.center,true);Next(7);
            }
            else if(step==7)
            {Pointer(menu,menu.back.hit.bounds.center,false);Next(8);}
            else if(step==8 && menu.Phase==FactoryMenuPhase.Main && menu.buttons[1].hit.enabled)
            {
                check(!menu.settingsPanel.gameObject.activeSelf && menu.buttons[1].hit.enabled,"Back returns to three hanging buttons");
                Pointer(menu,menu.buttons[1].hit.bounds.center,true);Next(9);
            }
            else if(step==9)
            {Pointer(menu,menu.buttons[1].hit.bounds.center,false);Next(10);}
            else if(step==10)
            {
                if(!leaving && menu.Phase==FactoryMenuPhase.Leaving && menu.PhaseTime>.4f)
                {leaving=true;CardboardFactorySceneBuilder.Capture("FactoryMenuLiftAway");}
                if(!black && menu.Phase==FactoryMenuPhase.Black)
                {
                    black=true;check(menu.fade.color.a==1 && game.PlayerBlocked,"Play passes through a fully black screen while gameplay remains blocked");
                    CardboardFactorySceneBuilder.Capture("FactoryMenuBlack");
                }
                if(menu.Phase==FactoryMenuPhase.Closed)
                {
                    check(black && leaving && !game.MenuActive && !game.PlayerBlocked && menu.fade.color.a==0,"Play finishes bounce, fade out, black hold and fade in before enabling player");
                    check(game.Mistakes==0 && game.CurrentOrder==1 && game.ShiftRemaining>239,"A fresh shift starts only after Play");
                    check(Vector3.Distance(menu.lens.transform.position,CardboardRunner.Player.transform.position+Vector3.up*1.62f)<.02f && menu.shiftHud.alpha==1,"Menu camera returns to first person with shift HUD");
                    CardboardFactorySceneBuilder.Capture("FactoryMenuStartedShift");
                    menu.SuppressQuit=true;menu.Show();Next(11);
                }
            }
            else if(step==11 && menu.Phase==FactoryMenuPhase.Main)
            {Pointer(menu,menu.buttons[2].hit.bounds.center,true);Next(12);}
            else if(step==12)
            {Pointer(menu,menu.buttons[2].hit.bounds.center,false);Next(13);}
            else if(step==13 && menu.Phase==FactoryMenuPhase.Closed)
            {
                check(menu.QuitRequested,"Exit button reaches quit after its animation and black fade");
                menu.SuppressQuit=false;menu.SetVolume(originalVolume,true);Cleanup();game.ResetShift();
                menu.lens.GetComponent<CardboardCamera>().ResetView(0);
                return true;
            }
            return false;
        }
        public static void CheckWorkstations(CardboardFactoryGame game,Action<bool,string> check)
        {
            foreach(var station in game.stations)
            {
                check(station.wheel.name=="Shturval_Rotate" && station.wheel.GetComponent<MeshFilter>().sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Cardboard/Generated/WorkstationWheelRotate.asset"),"New rotating wheel mesh installed: "+station.name);
                check(station.wheelBase.GetComponentsInChildren<MeshFilter>().Length>=2,"Original wheel support remains present: "+station.name);
                check(station.selectorIcons.Length==3 && station.selectorNumber==null,"Selector uses three gear icons and no number label: "+station.name);
                for(int i=0;i<3;i++)
                {
                    var mesh=Resources.Load<GameObject>("FactoryGears/Gear"+(i+1)).GetComponent<MeshFilter>().sharedMesh;
                    check(station.selectorIcons[i].GetComponentInChildren<MeshFilter>().sharedMesh==mesh && station.baths[i].Find("Bath gear icon").GetComponentInChildren<MeshFilter>().sharedMesh==mesh,"Matching selector and bath gear type "+(i+1)+": "+station.name);
                    check(!station.baths[i].Find("Number "+(i+1)),"Bath number replaced with gear: "+(i+1));
                }
            }
            game.ResetShift();
            for(int i=0;i<3;i++)
            {
                int type=game.station.SelectedType;
                Vector3 direction=(game.station.selectorIcons[type-1].position-game.station.selector.position);direction.z=0;
                check(Vector3.Dot(game.station.selector.up,direction.normalized)>.999f,"Selector sharp end points at chosen gear "+type);
                game.station.Operate(StationPart.Selector);
            }
            game.ResetShift();
            int count=0,bad=0;
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
            foreach(var material in renderer.sharedMaterials)
                if(material && material.HasProperty("_Smoothness")){count++;if(material.GetFloat("_Smoothness")>.001f)bad++;}
            check(count>100 && bad==0,"All world and menu material slots have maximum roughness");
        }
    }
}
