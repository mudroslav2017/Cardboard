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
        static Material paper,ink,gold,lettering;
        static Transform BoxPart(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var part=Box(name,parent,position,size,material);part.gameObject.layer=10;return part;
        }
        static TextMesh Text(string value,Transform parent,Vector3 position,float size,float maxWidth,float maxHeight=0)
        {
            var root=Group(value,parent,position);
            var text=root.gameObject.AddComponent<TextMesh>();text.font=CardboardTypography.DefaultFont;text.fontSize=48;
            text.characterSize=size;text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=Hex("2B2116");text.font.RequestCharactersInTexture(value,48,FontStyle.Normal);
            var label=root.gameObject.AddComponent<CardboardWorldLabel>();label.ink=lettering;label.Refresh();
            var renderer=root.GetComponent<MeshRenderer>();
            if(maxHeight>0)
            {
                // TextMesh line bounds include padding; align the visible glyphs instead.
                var settings=new TextGenerationSettings
                {
                    font=text.font,fontSize=text.fontSize,fontStyle=FontStyle.Normal,color=Color.white,
                    scaleFactor=1,lineSpacing=1,textAnchor=TextAnchor.MiddleCenter,
                    horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow,
                    generationExtents=Vector2.zero,generateOutOfBounds=true
                };
                using(var generator=new TextGenerator())
                {
                    generator.Populate(value,settings);
                    var vertices=generator.verts;
                    Bounds inkBounds=default;bool first=true;
                    for(int quad=0;quad+3<vertices.Count;quad+=4)
                    {
                        if((vertices[quad].position-vertices[quad+2].position).sqrMagnitude<.001f)continue;
                        for(int corner=0;corner<4;corner++)
                        {
                            var point=vertices[quad+corner].position;
                            if(first){inkBounds=new Bounds(point,Vector3.zero);first=false;}else inkBounds.Encapsulate(point);
                        }
                    }
                    float units=Mathf.Min(maxWidth/inkBounds.size.x,maxHeight/inkBounds.size.y);
                    text.characterSize=units*10;
                    root.localPosition=position-new Vector3(inkBounds.center.x,inkBounds.center.y,0)*units;
                }
            }
            else
            {
                if(renderer.bounds.size.x>maxWidth)text.characterSize*=maxWidth/renderer.bounds.size.x;
                root.position+=parent.TransformPoint(position)-renderer.bounds.center;
            }
            root.gameObject.layer=10;return text;
        }
        static Transform ImportedCard(string name,Transform parent,string model,string texture,string generated,float width,Vector3 position)
        {
            const string root="Assets/Game_Assets/Buttons/";
            var mesh=Bake(root+model,generated,-90,Vector3.zero,true,width);
            var material=Paper(generated,Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(root+texture));
            var part=MeshObject(name,parent,mesh,material,position);part.gameObject.layer=10;return part;
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
            gold=Paper("MenuGold",Hex("E3B84F"));
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
            string[] words={"Settings","Play","Exit"};
            string[] textures={"1/knopka1_Texture.png","2/Button_2.png","3/3.png"};
            for(int i=0;i<3;i++)
            {
                var pivot=Group(words[i]+" • hanging pivot",menu.visuals,Vector3.zero);
                var button=pivot.gameObject.AddComponent<FactoryHangingButton>();button.action=(FactoryMenuAction)i;menu.buttons[i]=button;
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Buttons/"+textures[i]);
                var material=Paper("MenuButton"+(i+1),Color.white,texture);
                var mesh=Bake("Assets/Game_Assets/Buttons/"+(i+1)+"/"+(i+1)+".fbx","MenuButton"+(i+1),-90,Vector3.zero,true,2.6f);
                var card=Group("Cardboard button",pivot,Vector3.zero);
                var face=MeshObject("Imported button "+(i+1),card,mesh,material,Vector3.down*.65f);
                face.gameObject.layer=10;
                var hit=Group("Button pointer target",card,Vector3.down*.65f);hit.gameObject.layer=10;
                button.hit=hit.gameObject.AddComponent<BoxCollider>();button.hit.size=new Vector3(2.6f,.76f,.25f);
                Text(words[i],card,new Vector3(0,-.65f,mesh.bounds.min.z-.015f),.13f,2.12f,.28f);
                for(int side=-1;side<=1;side+=2)
                {
                    int variant=side<0?1:2;
                    var chainMesh=Bake("Assets/Game_Assets/Buttons/Chain/Chain_"+variant+".fbx","MenuChain"+variant,-90,Vector3.zero,true,.30f);
                    var chainMaterial=Paper("MenuChain"+variant,Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Buttons/Chain/Chain_"+variant+".png"));
                    float bottom=-.65f+mesh.bounds.max.y-.22f;
                    float length=chainMesh.bounds.size.y;
                    float depth=mesh.bounds.max.z+chainMesh.bounds.extents.z+.02f;
                    var chain=MeshObject("Imported chain "+variant,pivot,chainMesh,chainMaterial,new Vector3(side*.94f,bottom+length*.5f,depth));
                    chain.gameObject.layer=10;
                }
            }
            menu.settingsPanel=Group("Settings • cardboard volume panel",camera.transform,new Vector3(0,-.05f,5));
            ImportedCard("Settings backing",menu.settingsPanel,"Settings/Podloshka.fbx","Settings/Podlosha.png","MenuSettingsBacking",3.6f,Vector3.zero);
            Text("Volume",menu.settingsPanel,new Vector3(0,.70f,-.13f),.15f,2.7f);
            var slider=Group("Volume pointer track",menu.settingsPanel,new Vector3(0,-.10f,-.18f));slider.gameObject.layer=10;
            menu.volumeTrack=slider.gameObject.AddComponent<BoxCollider>();menu.volumeTrack.size=new Vector3(2.5f,.62f,.4f);
            ImportedCard("Volume bar",slider,"Volume_Bar/Volume_Bar.fbx","Volume_Bar/Volum_Bar_Texture.png","MenuVolumeBar",2.25f,Vector3.zero);
            menu.volumeKnob=Group("Volume cardboard gear",slider,Vector3.zero);
            ImportedCard("Volume gear",menu.volumeKnob,"Volume_Bar/Volume_Gear.fbx","Volume_Bar/Volume_Gear_Texture.png","MenuVolumeGear",.48f,Vector3.zero);
            menu.volumeText=Text("65",menu.settingsPanel,new Vector3(0,-.72f,-.13f),.075f,.8f);
            var backRoot=Group("Back to menu",menu.settingsPanel,Vector3.zero);
            menu.back=backRoot.gameObject.AddComponent<FactoryHangingButton>();menu.back.action=FactoryMenuAction.Back;menu.back.home=new Vector3(-2.25f,1.30f,-.03f);
            ImportedCard("Back cardboard",backRoot,"Settings/Back_Button.fbx","Settings/Back_Button.png","MenuBackButton",.55f,new Vector3(0,-.65f,0));
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
            FactoryPhysicsInstaller.Cursor(menu);
        }
    }
}
