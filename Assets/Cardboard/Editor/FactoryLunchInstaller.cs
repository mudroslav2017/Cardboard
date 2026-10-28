using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Cardboard.CardboardArt;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryLunchInstaller
    {
        static Transform Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid=false)
        {
            var cube=Box(name,parent,position,size,material);
            if(solid) {cube.gameObject.layer=8;cube.gameObject.AddComponent<BoxCollider>();}
            return cube;
        }
        static Bounds BoundsOf(Transform model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("podnos.fbx contains no rendered mesh.");
            var bounds=renderers[0].bounds;foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);return bounds;
        }
        static Transform Tray(Transform root,Material material,out float height)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/podnos.fbx");
            if(!asset) throw new InvalidOperationException("Missing Assets/podnos.fbx.");
            var tray=Group("Lunch tray with food",root,Vector3.zero);
            var pose=Group("podnos • normalized model",tray,Vector3.zero);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,pose);
            foreach(var camera in model.GetComponentsInChildren<Camera>()) Object.DestroyImmediate(camera);
            foreach(var light in model.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light);
            foreach(var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var bounds=BoundsOf(pose);
            if(bounds.size.z<bounds.size.y && bounds.size.z<bounds.size.x) pose.localRotation=Quaternion.Euler(90,0,0);
            else if(bounds.size.x<bounds.size.y && bounds.size.x<bounds.size.z) pose.localRotation=Quaternion.Euler(0,0,90);
            bounds=BoundsOf(pose);
            if(bounds.size.z>bounds.size.x) pose.localRotation=Quaternion.Euler(0,90,0)*pose.localRotation;
            bounds=BoundsOf(pose);pose.localScale=Vector3.one*(.72f/bounds.size.x);
            bounds=BoundsOf(pose);pose.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) materials[i]=material;
                renderer.sharedMaterials=materials;
            }
            bounds=BoundsOf(pose);height=bounds.size.y;
            File.WriteAllText("Library/LunchTrayModelAudit.txt","Source: Assets/podnos.fbx\nNormalized bounds: "+bounds+"\n");
            FactoryTrayInstaller.Visual(tray);
            return tray;
        }
        static void Control(Transform parent,FactoryLunch lunch,bool serve,Vector3 position,Vector3 size)
        {
            var root=Group(serve?"Lunch delivery interaction":"Lunch pickup interaction",parent,position);
            root.gameObject.AddComponent<BoxCollider>().size=size;
            var interaction=root.gameObject.AddComponent<FactoryInteractable>();interaction.action=FactoryAction.Lunch;
            var control=root.gameObject.AddComponent<FactoryLunchControl>();control.lunch=lunch;control.serve=serve;
            if(serve) lunch.destinationHit=root.gameObject;else lunch.pickupHit=root.gameObject;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static Image Panel(RectTransform rect,Color color)
        { var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image; }
        static Text Label(RectTransform rect,int size)
        {
            var text=rect.gameObject.AddComponent<Text>();text.font=CardboardTypography.DefaultFont;text.fontSize=size;
            text.color=new Color(1,.94f,.8f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        static void HUD(FactoryLunch lunch,FactoryMistakeHUD parent)
        {
            var old=parent.GetComponent<FactoryLunchHUD>();if(old) Object.DestroyImmediate(old);
            var oldPanel=parent.transform.Find("Lunch instructions");if(oldPanel) Object.DestroyImmediate(oldPanel.gameObject);
            var hud=parent.gameObject.AddComponent<FactoryLunchHUD>();hud.lunch=lunch;
            var panel=Rect("Lunch instructions",parent.transform,new Vector2(0,35),new Vector2(650,230));
            Panel(panel,new Color(.09f,.15f,.12f,.92f));hud.panel=panel.gameObject;
            hud.title=Label(Rect("Lunch title",panel,new Vector2(0,163),new Vector2(620,50)),24);
            hud.hint=Label(Rect("Lunch instructions text",panel,new Vector2(0,65),new Vector2(620,85)),18);
            hud.hint.lineSpacing=1.7f;
            var track=Rect("Tray balance",panel,new Vector2(0,24),new Vector2(400,15));
            Panel(track,new Color(.65f,.27f,.18f));hud.balance=track.gameObject;
            Panel(Rect("Safe center",track,Vector2.zero,new Vector2(160,15)),new Color(.4f,.61f,.35f));
            hud.needle=Rect("Tray tilt indicator",track,Vector2.zero,new Vector2(7,27));
            Panel(hud.needle,new Color(1,.94f,.75f));hud.needle.pivot=new Vector2(.5f,.2f);
            var aim=Rect("Lunch interaction reticle",panel,new Vector2(0,465),new Vector2(5,5));
            Panel(aim,new Color(1,.94f,.8f));hud.aim=aim.gameObject;
            // The nested balance pieces are centered in their bar rather than at the panel's lower edge.
            foreach(RectTransform child in track) {child.anchorMin=child.anchorMax=new Vector2(.5f,.5f);child.pivot=new Vector2(.5f,.5f);child.anchoredPosition=Vector2.zero;}
            panel.gameObject.SetActive(false);
        }
        public static void Create(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();
            ExpandCanteen(world);
            var previous=world.Find("Shift lunch • canteen");if(previous) Object.DestroyImmediate(previous.gameObject);
            var root=Group("Shift lunch • canteen",world,Vector3.zero);
            var lunch=root.gameObject.AddComponent<FactoryLunch>();lunch.game=game;game.lunch=lunch;
            var wood=FactoryPresentationInstaller.Paper("LunchFurniture",Hex("8F6440"));
            var trayMaterial=FactoryPresentationInstaller.Paper("LunchTray",Hex("AEB5A7"));
            var gold=FactoryPresentationInstaller.Paper("LunchMarker",Hex("DEB55C"));
            Cube("Lunch serving counter • placeholder",root,new Vector3(-19,.42f,-19.5f),new Vector3(3,1.15f,1.3f),wood,true);
            Cube("Lunch table • placeholder",root,new Vector3(-7,.32f,-24),new Vector3(4.4f,.95f,1.8f),wood,true);
            Cube("Shared lunch table west • placeholder",root,new Vector3(-20,.32f,-29),new Vector3(5,.95f,1.8f),wood,true);
            Cube("Shared lunch table east • placeholder",root,new Vector3(-7,.32f,-30),new Vector3(5,.95f,1.8f),wood,true);
            lunch.pickup=Group("Lunch pickup pose",root,new Vector3(-19,1.01f,-19.2f));
            lunch.destination=Group("Lunch served pose",root,new Vector3(-7,.83f,-30.45f));
            lunch.tray=Tray(root,trayMaterial,out float trayHeight);
            lunch.food=new[]{FactoryLunchProps.Cup(lunch.tray)};
            lunch.tray.position=lunch.pickup.position;
            Control(root,lunch,false,new Vector3(-19,1.08f,-19.2f),new Vector3(.85f,.28f,.6f));
            Control(root,lunch,true,new Vector3(-7,.98f,-30.45f),new Vector3(1.4f,.45f,.9f));lunch.destinationHit.SetActive(false);
            CreateDiners(root,lunch);
            foreach(float x in new[]{-20f,-7f})
            {
                Cube("Canteen light • placeholder",root,new Vector3(x,4.5f,-27),new Vector3(2,.1f,.6f),gold);
                var light=Group("Canteen warm light",root,new Vector3(x,4.2f,-27)).gameObject.AddComponent<Light>();
                light.type=LightType.Point;light.range=14;light.intensity=2;light.color=Hex("FFE2B0");
            }
            FactoryCanteenInstaller.DeliveryLighting(game);
            HUD(lunch,world.GetComponentInChildren<FactoryMistakeHUD>(true));
        }
        static void ExpandCanteen(Transform world)
        {
            foreach(var part in world.GetComponentsInChildren<Transform>(true))
            {
                switch(part.name)
                {
                    case "Canteen floor":part.localPosition=new Vector3(-13,-.32f,-25);part.localScale=new Vector3(26,.3f,18);break;
                    case "Canteen floor surface":part.localPosition=new Vector3(-13,-.16f,-25);part.localScale=new Vector3(26,.025f,18);break;
                    case "Canteen south wall":part.localPosition=new Vector3(-13,2.7f,-34);part.localScale=new Vector3(26,5.8f,.4f);break;
                    case "Canteen west wall":part.localPosition=new Vector3(-26,2.7f,-25);part.localScale=new Vector3(.4f,5.8f,18);break;
                    case "Canteen east wall":part.localPosition=new Vector3(0,2.7f,-25);part.localScale=new Vector3(.4f,5.8f,18);break;
                    case "Canteen north wall":
                        part.localPosition=new Vector3(-13+(part.localPosition.x<-13?-7.25f:7.25f),2.7f,-16);
                        part.localScale=new Vector3(11.5f,5.8f,.4f);break;
                }
            }
            FactoryFloorInstaller.Apply(world);
        }
        static void CreateDiners(Transform root,FactoryLunch lunch)
        {
            var crowd=root.gameObject.AddComponent<FactoryLunchCrowd>();crowd.lunch=lunch;lunch.diners=crowd;
            var workers=new System.Collections.Generic.List<CardboardRunner>();
            foreach(var worker in lunch.game.crowd.runners) if(!worker.isPlayer) workers.Add(worker);
            crowd.workers=workers.ToArray();crowd.places=new Transform[workers.Count];
            crowd.meals=new Transform[workers.Count];crowd.bites=new Transform[workers.Count];
            int index=0;
            void Place(float x,float z,float mealZ,bool southFacing)
            {
                var place=Group("Worker lunch place "+(index+1),root,new Vector3(x,0,z));
                place.localRotation=Quaternion.Euler(0,southFacing?180:0,0);crowd.places[index]=place;
                var meal=Object.Instantiate(lunch.tray,root);meal.name="Worker meal "+(index+1);
                meal.SetPositionAndRotation(new Vector3(x,.83f,mealZ),place.rotation);
                crowd.meals[index]=meal;crowd.bites[index]=meal.Find(lunch.food[0].name);meal.gameObject.SetActive(false);
                index++;
            }
            foreach(float x in new[]{-8.45f,-7f,-5.55f}) Place(x,-25.7f,-24.45f,false);
            foreach(float x in new[]{-21.2f,-18.8f}) Place(x,-27.4f,-28.5f,true);
            foreach(float x in new[]{-21.2f,-18.8f}) Place(x,-30.6f,-29.5f,false);
            foreach(float x in new[]{-8.9f,-5.1f}) Place(x,-28.4f,-29.5f,true);
            foreach(float x in new[]{-8.9f,-5.1f}) Place(x,-31.6f,-30.5f,false);
        }
        [MenuItem("Cardboard/Install shift lunch")]
        public static void InstallCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before installing lunch.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            if(!game) throw new InvalidOperationException("Open FactoryShift first.");
            Create(game.transform);FactorySurfaceInstaller.Apply(game.transform);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryLunchInstall.txt","SUCCESS: shift lunch, imported podnos.fbx, textured water glass, serving counter, delivery table and balance HUD installed.\n");
        }
    }
}
