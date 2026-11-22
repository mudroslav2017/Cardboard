using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Cardboard
{
    [DefaultExecutionOrder(-70),DisallowMultipleComponent]
    public partial class FactoryPaperDrama:MonoBehaviour
    {
        public const float Duration=43;
        public const float GreetingEnd=4.2f,SplitAt=5.7f,SplitEnd=8,RecognitionAt=10,BadgeAt=12.2f,BadgeEnd=13.7f,ShockAt=15.9f,BossTalkAt=17.4f,RoofAt=19,FarewellAt=23.4f,FallAt=25.5f,DiveAt=26.3f,GlideAt=28.7f,LandingAt=36.6f,FadeAt=42;
        public bool Active {get;private set;}
        public bool Playing {get;private set;}
        public bool ControlsVisible {get;private set;}=true;
        public float Elapsed {get;private set;}
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public bool SplitScreen=>Active && Elapsed>=SplitAt && Elapsed<SplitEnd;
        public string Chapter=>Elapsed<GreetingEnd?"Возвращение домой":Elapsed<SplitEnd?"Разоблачение":Elapsed<RecognitionAt?"Хуанг?":Elapsed<ShockAt?"Начальник":Elapsed<RoofAt?"Неловкая пауза":Elapsed<FallAt?"Крыша":Elapsed<GlideAt?"Падение":Elapsed<LandingAt?"Картонная физика":Elapsed<FadeAt?"Финал":"Затемнение";
        public Transform Hero=>hero?.root;
        public Transform Boss=>boss?.root;
        public Transform Wife=>wife?.root;
        public Transform BossBadge=>badge;
        public bool Recognition=>Active && Elapsed>=RecognitionAt && Elapsed<ShockAt;
        public bool BadgeShot=>Elapsed>=BadgeAt && Elapsed<BadgeEnd;
        public bool FastFall=>Active && Elapsed>=DiveAt && Elapsed<GlideAt;
        public float BadgeSweep=>BadgeShot?Mathf.InverseLerp(BadgeAt,BadgeEnd,Elapsed):0;
        public float RedAccent=>redlight?redlight.intensity:0;
        public float FadeOpacity=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(FadeAt,Duration,Elapsed));
        public float KissDistance=>Vector3.Distance(wife.Contact(true),boss.Contact(false));
        public Transform Bouquet=>bouquet;
        public Camera SecondCamera=>secondCamera;
        public Bounds HomeBounds=>homeBounds;
        public Renderer ExistingHouse {get;private set;}
        public float RoofHeight=>roof.y;
        public float StreetHeight=>ground;
        public float ColdGrade=>cold?cold.weight:0;
        public TextMesh HeroLine=>heroLine;
        public TextMesh QuestionLine=>questionLine;
        public TextMesh BossLine=>bossLine;
        public TextMesh FinalLine=>finalLine;
        public TextMesh RoofLine=>roofLine;
        public bool RainVisible=>rainRoot && rainRoot.gameObject.activeSelf;
        public Vector3 GlideStart {get;private set;}
        public Vector3 GlideEnd {get;private set;}
        CardboardFactoryGame game;FactoryFilmSession session;FactoryPaperDramaSettings config;
        Camera lens,secondCamera;int filmMask;Transform stage;
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        Volume cold;VolumeProfile coldProfile,bloomProfile;Light underlight,moon,interiorKey,streetKey,flash,redlight,badgeLight;
        Material fadeMaterial,glintMaterial;Transform fadeRoot,badgeGlint;Renderer fadeRenderer;
        bool dragging,resumeAfterDrag;
        void Awake(){game=GetComponent<CardboardFactoryGame>();}
        void Update()
        {
            var keys=Keyboard.current;
            if(keys!=null && keys.f6Key.wasPressedThisFrame){if(Active)Stop();else Begin();}
            if(!Active)return;
            if(keys!=null && keys.hKey.wasPressedThisFrame){ControlsVisible=!ControlsVisible;SetCursor();}
            if(keys!=null && keys.f7Key.wasPressedThisFrame){Seek(0);SetPlaying(true);}
            if(keys!=null && keys.spaceKey.wasPressedThisFrame && !game.MenuActive)SetPlaying(!Playing);
            if(game.MenuActive){StopAudio();lens.rect=session.CameraRect;if(secondCamera)secondCamera.enabled=false;return;}
            Tick(Time.unscaledDeltaTime);ApplyViewport();
        }
        void LateUpdate(){if(Active && !game.MenuActive)ApplyViewport();}
        public bool Begin()
        {
            if(Active || !game)return false;config=Resources.Load<FactoryPaperDramaSettings>("PaperDrama/Settings");
            if(!config){Debug.LogError("Prepare paper drama assets first.");return false;}
            session=game.filmSession;if(!session.Begin())return false;
            lens=Camera.main;filmMask=lens.cullingMask;Active=true;Playing=true;ControlsVisible=true;Elapsed=0;CueCount=0;
            try
            {
                stage=new GameObject("Paper drama • 43 second recording").transform;
                BuildStage();BuildSound();ApplyStory();ApplyShot();ApplyViewport();SetCursor();return true;
            }
            catch(Exception error){Debug.LogException(error);Stop(false);return false;}
        }
        public void Tick(float dt)
        {
            if(!Active || game.MenuActive)return;
            float before=Elapsed;if(Playing)Elapsed=Mathf.Clamp(Elapsed+Mathf.Max(0,dt),0,Duration);
            ApplyStory();ApplyShot();if(Playing)PlayCrossedCues(before,Elapsed);RefreshAmbience();
            if(Elapsed>=Duration)SetPlaying(false);
        }
        public void Seek(float time)
        {
            if(!Active)return;Playing=false;StopAudio();Elapsed=Mathf.Clamp(time,0,Duration);ApplyStory();ApplyShot();ApplyViewport();
        }
        public void SeekNormalized(float value){Seek(Mathf.Clamp01(value)*Duration);}
        public void SetPlaying(bool value)
        {
            if(!Active)return;if(value && Elapsed>=Duration)Seek(0);Playing=value;if(!value)StopAudio();else RefreshAmbience();
        }
        void SetCursor(){Cursor.lockState=ControlsVisible?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=ControlsVisible;}
        public Rect FilmPixels
        {
            get{float w=Screen.width,h=w*16/9f;if(h>Screen.height){h=Screen.height;w=h*9/16f;}return new Rect((Screen.width-w)*.5f,(Screen.height-h)*.5f,w,h);}
        }
        void ApplyViewport()
        {
            if(!lens)return;var p=FilmPixels;var rect=new Rect(p.x/Screen.width,p.y/Screen.height,p.width/Screen.width,p.height/Screen.height);
            lens.rect=SplitScreen?new Rect(rect.x,rect.y+rect.height*.5f,rect.width,rect.height*.5f):rect;
            bool heroShot=Elapsed<GreetingEnd || SplitScreen || (Elapsed>=SplitEnd && Elapsed<RecognitionAt) || (Elapsed>=ShockAt && Elapsed<BossTalkAt);
            bool bossShot=Recognition || (Elapsed>=BossTalkAt && Elapsed<RoofAt);
            lens.cullingMask=heroShot?filmMask & ~((1<<15)|(1<<16)):bossShot?filmMask & ~(1<<16):filmMask;
            secondCamera.cullingMask=filmMask & ~(1<<14);secondCamera.enabled=SplitScreen;if(SplitScreen)secondCamera.rect=new Rect(rect.x,rect.y,rect.width,rect.height*.5f);
        }
        public Rect TimelineBar=>new Rect(Mathf.Max(10,(Screen.width-Mathf.Min(660,Screen.width-20))*.5f)+14,Screen.height-77,Mathf.Min(660,Screen.width-20)-28,16);
        void OnGUI()
        {
            if(!Active || game.MenuActive)return;
            var p=FilmPixels;var old=GUI.color;GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,p.x,Screen.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(p.xMax,0,Screen.width-p.xMax,Screen.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(p.x,0,p.width,p.y),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(p.x,p.yMax,p.width,Screen.height-p.yMax),Texture2D.whiteTexture);
            if(SplitScreen)GUI.DrawTexture(new Rect(p.x,Screen.height*.5f-2,p.width,4),Texture2D.whiteTexture);GUI.color=old;
            if(!ControlsVisible)return;
            float width=Mathf.Min(660,Screen.width-20),left=(Screen.width-width)*.5f;var panel=new Rect(left,Screen.height-121,width,111);
            GUI.color=new Color(.07f,.055f,.04f,.94f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=Color.white;
            var label=new GUIStyle(GUI.skin.label){font=CardboardTypography.DefaultFont,fontSize=Mathf.Clamp(Screen.width/40,14,20),alignment=TextAnchor.MiddleLeft};label.normal.textColor=new Color(1,.91f,.71f);
            GUI.Label(new Rect(left+13,panel.y+4,width-24,28),Elapsed.ToString("00.0")+" / "+Duration.ToString("00.0")+"  ·  "+Chapter,label);
            var bar=TimelineBar;GUI.color=new Color(.32f,.25f,.16f);GUI.DrawTexture(bar,Texture2D.whiteTexture);
            GUI.color=new Color(.94f,.71f,.3f);GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Elapsed/Duration,bar.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(bar.x+bar.width*Elapsed/Duration-2,bar.y-3,4,bar.height+6),Texture2D.whiteTexture);
            foreach(float marker in new[]{SplitAt,SplitEnd,RecognitionAt,ShockAt,RoofAt,FallAt,DiveAt,GlideAt,LandingAt,FadeAt}){GUI.color=new Color(1,1,1,.5f);GUI.DrawTexture(new Rect(bar.x+bar.width*marker/Duration-1,bar.y,2,bar.height),Texture2D.whiteTexture);}GUI.color=Color.white;
            var e=Event.current;var hit=new Rect(bar.x,bar.y-7,bar.width,bar.height+14);
            if(e.type==EventType.MouseDown && e.button==0 && hit.Contains(e.mousePosition)){dragging=true;resumeAfterDrag=Playing;SeekNormalized((e.mousePosition.x-bar.x)/bar.width);e.Use();}
            else if(dragging && e.type==EventType.MouseDrag){SeekNormalized((e.mousePosition.x-bar.x)/bar.width);e.Use();}
            else if(dragging && e.type==EventType.MouseUp){dragging=false;if(resumeAfterDrag)SetPlaying(true);e.Use();}
            var button=new GUIStyle(GUI.skin.button){font=CardboardTypography.DefaultFont,fontSize=14};float bw=(width-34)/4,y=panel.yMax-32;
            if(GUI.Button(new Rect(left+10,y,bw,25),Playing?"Пауза":"Играть",button))SetPlaying(!Playing);
            if(GUI.Button(new Rect(left+14+bw,y,bw,25),"Сначала",button)){Seek(0);SetPlaying(true);}
            if(GUI.Button(new Rect(left+18+bw*2,y,bw,25),"Скрыть [H]",button)){ControlsVisible=false;SetCursor();}
            if(GUI.Button(new Rect(left+22+bw*3,y,bw,25),"Выход [F6]",button))Stop();
        }
        public void Stop(bool restoreMenu=true)
        {
            if(!Active)return;Active=false;Playing=false;StopAudio();
            if(stage){stage.gameObject.SetActive(false);Destroy(stage.gameObject);}if(coldProfile)Destroy(coldProfile);if(bloomProfile)Destroy(bloomProfile);if(fadeMaterial)Destroy(fadeMaterial);if(glintMaterial)Destroy(glintMaterial);
            foreach(var mesh in ownedMeshes)if(mesh)Destroy(mesh);ownedMeshes.Clear();rainLines.Clear();speedLines.Clear();rainSeeds=null;stage=null;
            if(lens)lens.cullingMask=filmMask;
            if(session && session.Active)session.Stop(restoreMenu);
        }
        void OnDisable(){Stop(false);}
    }
}