using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Cardboard
{
    // A reversible pause for recording. It contains no choreography or story-specific assets.
    public class FactoryFilmSession:MonoBehaviour
    {
        public bool Active {get;private set;}
        public Rect CameraRect {get;private set;}
        readonly Dictionary<Behaviour,bool> paused=new Dictionary<Behaviour,bool>();
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        CardboardFactoryGame game;Camera lens;CardboardCamera rig;Transform followed;
        Vector3 view;Quaternion rotation;float fov,near,far,timeScale;Color background,ambient,fogColor;
        CameraClearFlags clear;bool audioPaused,beganInMenu,drawing,post,fog;float fogStart,fogEnd;Material sky;
        void Awake(){game=GetComponent<CardboardFactoryGame>();}
        void Pause(Behaviour item){if(item && !paused.ContainsKey(item)){paused[item]=item.enabled;item.enabled=false;}}
        public void Hide(Renderer item){if(item && !hidden.ContainsKey(item)){hidden[item]=item.enabled;item.enabled=false;}}
        public bool Begin()
        {
            if(Active || !game || !CardboardRunner.Player || !Camera.main || game.Ended || game.CaptureActive || game.EndingBusy)return false;
            beganInMenu=game.MenuActive;if(beganInMenu)game.mainMenu.CloseForFilming();
            lens=Camera.main;rig=lens.GetComponent<CardboardCamera>();followed=rig.player;
            view=lens.transform.position;rotation=lens.transform.rotation;fov=lens.fieldOfView;near=lens.nearClipPlane;far=lens.farClipPlane;CameraRect=lens.rect;
            background=lens.backgroundColor;clear=lens.clearFlags;post=lens.GetUniversalAdditionalCameraData().renderPostProcessing;
            ambient=RenderSettings.ambientLight;fog=RenderSettings.fog;fogColor=RenderSettings.fogColor;fogStart=RenderSettings.fogStartDistance;fogEnd=RenderSettings.fogEndDistance;sky=RenderSettings.skybox;
            timeScale=Time.timeScale;audioPaused=AudioListener.pause;drawing=CardboardMarker.DrawingMode;
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            Time.timeScale=0;AudioListener.pause=true;Active=true;rig.player=null;
            Pause(game.intro);Pause(game.expressions);Pause(game.workbench);if(game.lunch){Pause(game.lunch);Pause(game.lunch.diners);}
            foreach(var worker in game.crowd.runners){Pause(worker);foreach(var renderer in worker.GetComponentsInChildren<Renderer>(true))Hide(renderer);}
            foreach(var station in game.stations)Pause(station);
            foreach(var canvas in game.GetComponentsInChildren<Canvas>(true))Pause(canvas);
            foreach(var hud in game.GetComponentsInChildren<FactoryHUD>(true))Pause(hud);
            foreach(var item in lens.GetComponents<FactoryInventorySlot>())Pause(item);
            foreach(var item in FindObjectsByType<FactoryVolumeCursor>(FindObjectsInactive.Include,FindObjectsSortMode.None))Pause(item);
            foreach(var item in lens.GetComponentsInChildren<Renderer>(true))Hide(item);
            foreach(var item in game.GetComponentsInChildren<LineRenderer>(true))Hide(item);
            if(game.objectCarry && game.objectCarry.Held)foreach(var item in game.objectCarry.Held.GetComponentsInChildren<Renderer>(true))Hide(item);
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))Pause(light);
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(camera!=lens)Pause(camera);
            lens.nearClipPlane=.04f;lens.farClipPlane=250;lens.clearFlags=CameraClearFlags.SolidColor;lens.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            RenderSettings.skybox=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public void Stop(bool restoreMenu=true)
        {
            if(!Active)return;bool menu=restoreMenu && (beganInMenu || game.MenuActive);
            if(game.MenuActive)game.mainMenu.CloseForFilming();Active=false;
            if(lens){lens.transform.SetPositionAndRotation(view,rotation);lens.fieldOfView=fov;lens.nearClipPlane=near;lens.farClipPlane=far;lens.rect=CameraRect;lens.ResetAspect();lens.backgroundColor=background;lens.clearFlags=clear;lens.GetUniversalAdditionalCameraData().renderPostProcessing=post;}
            if(rig){rig.player=followed;rig.AdoptCurrentView();}
            foreach(var pair in paused)if(pair.Key)pair.Key.enabled=pair.Value;
            foreach(var pair in hidden)if(pair.Key)pair.Key.enabled=pair.Value;
            paused.Clear();hidden.Clear();Time.timeScale=timeScale;AudioListener.pause=audioPaused;
            RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;RenderSettings.skybox=sky;
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(drawing);
            Cursor.lockState=drawing?CursorLockMode.None:CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(drawing);
            if(menu)game.mainMenu.Show();
        }
        void OnDisable(){Stop(false);}
    }
}