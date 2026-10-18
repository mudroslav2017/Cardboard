using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cardboard
{
    public enum FactoryMenuPhase { Opening, Main, Pressing, ToSettings, Settings, ToMain, Leaving, Black, Revealing, Closed }

    [DefaultExecutionOrder(-90)]
    public class FactoryMainMenu : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Camera lens,uiCamera;
        public Transform visuals,settingsPanel,volumeKnob;
        public FactoryHangingButton[] buttons;
        public FactoryHangingButton back;
        public BoxCollider volumeTrack;
        public TextMesh volumeText;
        public Image fade;
        public CanvasGroup shiftHud;
        public Vector3 viewPosition;
        public Quaternion viewRotation=Quaternion.identity;
        public FactoryMenuPhase Phase {get;private set;}=FactoryMenuPhase.Opening;
        public float PhaseTime {get;private set;}
        public float Volume {get;private set;}
        public bool QuitRequested {get;private set;}
        public bool SuppressQuit;
        public static bool SkipNextMenu;
        public const string VolumeKey="Cardboard.MasterVolume";
        public bool BlocksGameplay => Phase!=FactoryMenuPhase.Closed;
        public bool OwnsCamera => BlocksGameplay && Phase!=FactoryMenuPhase.Revealing;
        public Vector3 CameraPosition => viewPosition+new Vector3(Mathf.Sin(Time.unscaledTime*.13f)*.08f,Mathf.Sin(Time.unscaledTime*.19f)*.025f,0);
        public Quaternion CameraRotation => viewRotation*Quaternion.Euler(0,Mathf.Sin(Time.unscaledTime*.11f)*.35f,0);
        FactoryMenuAction pending;
        bool sliderDragging;

        void Awake()
        {
            SetVolume(PlayerPrefs.GetFloat(VolumeKey,.65f),false);
            if(SkipNextMenu){SkipNextMenu=false;Close();}else Show();
        }
        public void Show()
        {
            Change(FactoryMenuPhase.Opening);QuitRequested=false;sliderDragging=false;
            visuals.gameObject.SetActive(true);settingsPanel.gameObject.SetActive(false);
            foreach(var button in buttons)button.ResetMotion();back.ResetMotion();
            if(shiftHud)shiftHud.alpha=0;
            lens.fieldOfView=60;if(uiCamera){uiCamera.enabled=true;uiCamera.fieldOfView=60;uiCamera.aspect=lens.aspect;}SetFade(0);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Update()
        {
            if(!BlocksGameplay)return;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(OwnsCamera)lens.fieldOfView=60;
            if(uiCamera){uiCamera.fieldOfView=lens.fieldOfView;uiCamera.aspect=lens.aspect;}
            Tick(Time.unscaledDeltaTime);
            if(Phase!=FactoryMenuPhase.Main && Phase!=FactoryMenuPhase.Settings)return;
            var mouse=Mouse.current;
            if(mouse==null)return;
            Ray ray=lens.ScreenPointToRay(mouse.position.ReadValue());
            Physics.SyncTransforms();
            FactoryHangingButton hover=null;
            if(Physics.Raycast(ray,out var hit,15,1<<10,QueryTriggerInteraction.Collide))
                hover=hit.collider.GetComponentInParent<FactoryHangingButton>();
            float depth=5;
            Vector3 pointer=lens.transform.InverseTransformPoint(ray.origin+ray.direction*(depth/Mathf.Max(.001f,Vector3.Dot(ray.direction,lens.transform.forward))));
            foreach(var button in buttons)button.Hover(Phase==FactoryMenuPhase.Main && hover==button,pointer);
            back.Hover(Phase==FactoryMenuPhase.Settings && hover==back,settingsPanel.InverseTransformPoint(lens.transform.TransformPoint(pointer)));
            if(mouse.leftButton.wasPressedThisFrame)
            {
                if(hover)Choose(hover.action);
                if(Phase==FactoryMenuPhase.Settings && volumeTrack.Raycast(ray,out var contact,15))sliderDragging=true;
            }
            if(sliderDragging)
            {
                var plane=new Plane(volumeTrack.transform.forward,volumeTrack.transform.position);
                if(plane.Raycast(ray,out float enter))
                    SetVolume(Mathf.InverseLerp(-1.05f,1.05f,volumeTrack.transform.InverseTransformPoint(ray.GetPoint(enter)).x),false);
                if(!mouse.leftButton.isPressed){sliderDragging=false;SaveVolume();}
            }
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame && Phase==FactoryMenuPhase.Settings)Choose(FactoryMenuAction.Back);
        }
        public void Choose(FactoryMenuAction action)
        {
            if(Phase!=FactoryMenuPhase.Main && Phase!=FactoryMenuPhase.Settings)return;
            if(Phase==FactoryMenuPhase.Settings && action!=FactoryMenuAction.Back)return;
            if(Phase==FactoryMenuPhase.Main && action==FactoryMenuAction.Back)return;
            pending=action;
            if(action==FactoryMenuAction.Back)back.Press();else buttons[(int)action].Press();
            Change(FactoryMenuPhase.Pressing);
        }
        public void Tick(float dt)
        {
            if(!BlocksGameplay)return;
            PhaseTime+=Mathf.Max(0,dt);
            Layout();
            float lift=0;
            if(Phase==FactoryMenuPhase.Opening)lift=7*(1-OutBack(Mathf.Clamp01(PhaseTime/.85f)));
            else if(Phase==FactoryMenuPhase.ToSettings || Phase==FactoryMenuPhase.Leaving)lift=7*InBack(Mathf.Clamp01(PhaseTime/.85f));
            else if(Phase==FactoryMenuPhase.ToMain)lift=7*(1-OutBack(Mathf.Clamp01(PhaseTime/.85f)));
            if(Phase==FactoryMenuPhase.Settings || (Phase==FactoryMenuPhase.Pressing && pending==FactoryMenuAction.Back))lift=7;
            foreach(var button in buttons)button.Pose(dt,lift,Phase==FactoryMenuPhase.Main?1:0);
            if(settingsPanel.gameObject.activeSelf)
            {
                float travel=Phase==FactoryMenuPhase.ToSettings?7*(1-OutBack(Mathf.Clamp01((PhaseTime-.25f)/.8f))):
                    Phase==FactoryMenuPhase.ToMain?7*InBack(Mathf.Clamp01(PhaseTime/.65f)):0;
                settingsPanel.localPosition=new Vector3(0,-.05f+travel,5);
                back.Pose(dt,0,Phase==FactoryMenuPhase.Settings?1:0);
            }
            if(Phase==FactoryMenuPhase.Opening && PhaseTime>=.85f)Change(FactoryMenuPhase.Main);
            else if(Phase==FactoryMenuPhase.Pressing && PhaseTime>=.28f)
            {
                if(pending==FactoryMenuAction.Settings){settingsPanel.gameObject.SetActive(true);Change(FactoryMenuPhase.ToSettings);}
                else if(pending==FactoryMenuAction.Back){SaveVolume();Change(FactoryMenuPhase.ToMain);}
                else Change(FactoryMenuPhase.Leaving);
            }
            else if(Phase==FactoryMenuPhase.ToSettings && PhaseTime>=1.05f)Change(FactoryMenuPhase.Settings);
            else if(Phase==FactoryMenuPhase.ToMain && PhaseTime>=.9f){settingsPanel.gameObject.SetActive(false);Change(FactoryMenuPhase.Main);}
            else if(Phase==FactoryMenuPhase.Leaving)
            {
                SetFade(Mathf.SmoothStep(0,1,(PhaseTime-.55f)/.6f));
                if(PhaseTime>=1.15f){visuals.gameObject.SetActive(false);SetFade(1);Change(FactoryMenuPhase.Black);}
            }
            else if(Phase==FactoryMenuPhase.Black && PhaseTime>=.22f)
            {
                SaveVolume();
                if(pending==FactoryMenuAction.Exit)
                {
                    QuitRequested=true;
                    if(!SuppressQuit)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying=false;
#else
                        Application.Quit();
#endif
                    }
                    Close();return;
                }
                visuals.gameObject.SetActive(false);settingsPanel.gameObject.SetActive(false);
                game.ResetShift();lens.GetComponent<CardboardCamera>().ResetView(0);
                lens.transform.position=CardboardRunner.Player.transform.position+Vector3.up*1.62f;
                lens.fieldOfView=72;if(shiftHud)shiftHud.alpha=1;
                Change(FactoryMenuPhase.Revealing);
            }
            else if(Phase==FactoryMenuPhase.Revealing)
            {
                SetFade(1-Mathf.SmoothStep(0,1,PhaseTime/.55f));
                if(PhaseTime>=.55f)Close();
            }
        }
        void Layout()
        {
            float halfHeight=5*Mathf.Tan(lens.fieldOfView*Mathf.Deg2Rad*.5f);
            float scale=Mathf.Min(1,halfHeight*lens.aspect*2/9.4f);
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].scale=scale;
                buttons[i].home=new Vector3((i-1)*2.95f*scale,halfHeight*.76f+.65f*scale,5);
            }
        }
        public void SetVolume(float value,bool save=true)
        {
            Volume=Mathf.Clamp01(value);AudioListener.volume=Volume;
            if(volumeKnob)volumeKnob.localPosition=new Vector3(Mathf.Lerp(-1.05f,1.05f,Volume),0,-.10f);
            if(volumeText)volumeText.text=Mathf.RoundToInt(Volume*100).ToString();
            if(save)SaveVolume();
        }
        void SaveVolume(){PlayerPrefs.SetFloat(VolumeKey,Volume);PlayerPrefs.Save();}
        void SetFade(float alpha){if(fade)fade.color=new Color(0,0,0,Mathf.Clamp01(alpha));}
        void Close()
        {
            Change(FactoryMenuPhase.Closed);visuals.gameObject.SetActive(false);settingsPanel.gameObject.SetActive(false);
            SetFade(0);lens.fieldOfView=72;if(uiCamera)uiCamera.enabled=false;if(shiftHud)shiftHud.alpha=1;
        }
        static float OutBack(float t){float u=t-1;return 1+2.70158f*u*u*u+1.70158f*u*u;}
        static float InBack(float t)=>2.70158f*t*t*t-1.70158f*t*t;
        void Change(FactoryMenuPhase phase){Phase=phase;PhaseTime=0;}
    }
}
