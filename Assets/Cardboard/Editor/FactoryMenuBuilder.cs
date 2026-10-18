using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;

namespace Cardboard.Editor
{
    public static class FactoryMenuBuilder
    {
        static Material paper,ink,rope,gold,lettering;
        static Transform BoxPart(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var part=Box(name,parent,position,size,material);part.gameObject.layer=10;return part;
        }
        static TextMesh Text(string value,Transform parent,Vector3 position,float size,float maxWidth)
        {
            var root=Group(value,parent,position);
            var text=root.gameObject.AddComponent<TextMesh>();text.font=CardboardTypography.DefaultFont;text.fontSize=48;
            text.characterSize=size;text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=Hex("2B2116");text.font.RequestCharactersInTexture(value,48,FontStyle.Normal);
            var label=root.gameObject.AddComponent<CardboardWorldLabel>();label.ink=lettering;label.Refresh();
            var renderer=root.GetComponent<MeshRenderer>();
            if(renderer.bounds.size.x>maxWidth)text.characterSize*=maxWidth/renderer.bounds.size.x;
            root.position+=parent.TransformPoint(position)-renderer.bounds.center;
            root.gameObject.layer=10;return text;
        }
        public static void RebuildCurrent()
        {
            var world=Object.FindFirstObjectByType<CardboardFactoryWorld>();
            Create(world.transform);AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(world.gameObject.scene);
        }
        public static void Create(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();var camera=world.GetComponentInChildren<Camera>(true);
            if(game.mainMenu)
            {
                var old=game.mainMenu;
                if(old.uiCamera){camera.GetUniversalAdditionalCameraData().cameraStack.Remove(old.uiCamera);Object.DestroyImmediate(old.uiCamera.gameObject);}
                if(old.visuals)Object.DestroyImmediate(old.visuals.gameObject);
                if(old.settingsPanel)Object.DestroyImmediate(old.settingsPanel.gameObject);
                if(old.fade)Object.DestroyImmediate(old.fade.canvas.gameObject);
                Object.DestroyImmediate(old.gameObject);
            }
            var oldLight=camera.transform.Find("Menu button fill light");if(oldLight)Object.DestroyImmediate(oldLight.gameObject);
            paper=Paper("MenuPaper",Hex("D8B378"));ink=Paper("MenuInk",Hex("392819"));
            rope=Paper("MenuSuspension",Hex("665039"));gold=Paper("MenuGold",Hex("E3B84F"));
            lettering=Paper("MenuLettering",Color.white,null,true);
            var menu=Group("Factory main menu",world,Vector3.zero).gameObject.AddComponent<FactoryMainMenu>();
            menu.game=game;menu.lens=camera;game.mainMenu=menu;
            menu.uiCamera=Group("Menu • overlay camera",camera.transform,Vector3.zero).gameObject.AddComponent<Camera>();
            menu.uiCamera.cullingMask=1<<10;menu.uiCamera.clearFlags=CameraClearFlags.Depth;menu.uiCamera.nearClipPlane=.05f;menu.uiCamera.farClipPlane=30;
            menu.uiCamera.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Overlay;
            camera.GetUniversalAdditionalCameraData().cameraStack.Add(menu.uiCamera);camera.cullingMask &= ~(1<<10);
            menu.uiCamera.enabled=false;
            menu.viewPosition=new Vector3(CardboardFactoryWorld.WorkshopCenterX+11,2.5f,-6.5f);
            menu.viewRotation=Quaternion.LookRotation(new Vector3(CardboardFactoryWorld.WorkshopCenterX-5,1.6f,8)-menu.viewPosition);
            menu.shiftHud=world.GetComponentInChildren<FactoryMistakeHUD>(true).GetComponent<CanvasGroup>();
            menu.visuals=Group("Menu • hanging imported cardboard buttons",camera.transform,Vector3.zero);
            menu.buttons=new FactoryHangingButton[3];
            string[] words={"Настройки","Играть","Выход"};
            string[] textures={"1/knopka1_Texture.png","2/Button_2.png","3/3.png"};
            for(int i=0;i<3;i++)
            {
                var pivot=Group(words[i]+" • hanging pivot",menu.visuals,Vector3.zero);
                var button=pivot.gameObject.AddComponent<FactoryHangingButton>();button.action=(FactoryMenuAction)i;menu.buttons[i]=button;
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Buttons/"+textures[i]);
                var material=Paper("MenuButton"+(i+1),Color.white,texture);
                var mesh=Bake("Assets/Game_Assets/Buttons/"+(i+1)+"/"+(i+1)+".fbx","MenuButton"+(i+1),i==0?90:-90,Vector3.zero,true,2.6f);
                var face=MeshObject("Imported button "+(i+1),pivot,mesh,material,Vector3.down*.65f);
                face.gameObject.layer=10;
                var hit=Group("Button pointer target",pivot,Vector3.down*.65f);hit.gameObject.layer=10;
                button.hit=hit.gameObject.AddComponent<BoxCollider>();button.hit.size=new Vector3(2.6f,.76f,.25f);
                Text(words[i],pivot,new Vector3(0,-.83f,-.10f),.13f,2.05f);
                for(int side=-1;side<=1;side+=2)
                {
                    var cord=FactoryGeometry.Rod("Temporary suspension • Unity cylinder",pivot,new Vector3(side*.94f,.55f,.02f),new Vector3(side*.94f,-.30f,.02f),.027f,rope);
                    cord.gameObject.layer=10;
                }
            }
            menu.settingsPanel=Group("Settings • cardboard volume panel",camera.transform,new Vector3(0,-.05f,5));
            BoxPart("Settings cardboard slab",menu.settingsPanel,Vector3.zero,new Vector3(3.6f,2.35f,.14f),paper);
            BoxPart("Settings upper ink",menu.settingsPanel,new Vector3(0,1.08f,-.082f),new Vector3(3.40f,.022f,.015f),ink);
            BoxPart("Settings lower ink",menu.settingsPanel,new Vector3(0,-1.08f,-.082f),new Vector3(3.40f,.022f,.015f),ink);
            foreach(int side in new[]{-1,1})BoxPart("Settings side ink",menu.settingsPanel,new Vector3(side*1.70f,0,-.082f),new Vector3(.022f,2.16f,.015f),ink);
            Text("Громкость",menu.settingsPanel,new Vector3(0,.70f,-.10f),.15f,2.7f);
            var slider=Group("Volume pointer track",menu.settingsPanel,new Vector3(0,-.10f,-.13f));slider.gameObject.layer=10;
            menu.volumeTrack=slider.gameObject.AddComponent<BoxCollider>();menu.volumeTrack.size=new Vector3(2.5f,.6f,.4f);
            BoxPart("Volume bar",slider,Vector3.zero,new Vector3(2.15f,.075f,.055f),ink);
            menu.volumeKnob=Group("Volume cardboard knob",slider,Vector3.zero);
            var knob=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(menu.volumeKnob,false);knob.transform.localRotation=Quaternion.Euler(90,0,0);knob.transform.localScale=new Vector3(.32f,.045f,.32f);
            knob.GetComponent<Renderer>().sharedMaterial=gold;knob.layer=10;
            menu.volumeText=Text("65",menu.settingsPanel,new Vector3(0,-.72f,-.10f),.075f,.8f);
            var backRoot=Group("Back to menu",menu.settingsPanel,Vector3.zero);
            menu.back=backRoot.gameObject.AddComponent<FactoryHangingButton>();menu.back.action=FactoryMenuAction.Back;menu.back.home=new Vector3(-2.25f,1.30f,-.03f);
            BoxPart("Back cardboard",backRoot,new Vector3(0,-.65f,0),new Vector3(.55f,.55f,.13f),paper);
            Text("<",backRoot,new Vector3(0,-.75f,-.09f),.15f,.38f);
            var backHit=Group("Back pointer target",backRoot,new Vector3(0,-.65f,0));backHit.gameObject.layer=10;
            menu.back.hit=backHit.gameObject.AddComponent<BoxCollider>();menu.back.hit.size=new Vector3(.60f,.60f,.22f);
            var overlay=new GameObject("Menu transition • black fade",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            overlay.transform.SetParent(world,false);var canvas=overlay.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.051f;canvas.sortingOrder=1000;
            overlay.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var black=new GameObject("Black screen",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));black.transform.SetParent(overlay.transform,false);
            var rect=black.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            menu.fade=black.GetComponent<Image>();menu.fade.color=Color.clear;menu.fade.raycastTarget=false;
            var light=Group("Menu button fill light",camera.transform,new Vector3(-1,2,-.5f)).gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.range=10;light.intensity=1.6f;light.color=Hex("FFE8C2");light.cullingMask=1<<10;
            menu.visuals.gameObject.SetActive(false);menu.settingsPanel.gameObject.SetActive(false);
        }
    }
}
