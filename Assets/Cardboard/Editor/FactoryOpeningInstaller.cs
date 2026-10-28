using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryOpeningInstaller
    {
        static Transform Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.transform.SetParent(parent,false);item.transform.localPosition=position;item.transform.localScale=size;item.layer=8;
            item.GetComponent<Renderer>().sharedMaterial=material;return item.transform;
        }
        static Bounds BoundsIn(Transform source,Transform space)
        {
            Bounds result=default;bool first=true;
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            foreach(var point in filter.sharedMesh.vertices)
            {var p=space.InverseTransformPoint(filter.transform.TransformPoint(point));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
            return result;
        }
        static void Population(CardboardFactoryGame game)
        {
            while(game.crowd.runners.Count(w=>!w.isPlayer)<12)
            {
                int number=game.crowd.runners.Count(w=>!w.isPlayer)+1;
                var worker=Object.Instantiate(game.crowd.runners.Last(w=>!w.isPlayer),game.transform);worker.name="Cardboard factory worker "+number;worker.personality=number;
                worker.transform.position=new Vector3(CardboardFactoryWorld.WorkshopCenterX,0,-3.2f);game.crowd.runners.Add(worker);
            }
            int index=0;foreach(var worker in game.crowd.runners.Where(w=>!w.isPlayer))worker.personality=index++;
            ImportedCharacterInstaller.Prepare();ImportedCharacterInstaller.ReplaceInScene(game.gameObject.scene);
            game.captureSequence.workers=game.crowd.runners.Where(w=>!w.isPlayer).ToArray();EditorUtility.SetDirty(game.captureSequence);
            var diners=game.lunch.diners;
            if(diners.workers.Length<12)
            {
                var place=Object.Instantiate(diners.places[7],diners.transform);place.name="Worker lunch place 12";place.position=new Vector3(-7,0,-28.4f);
                var meal=Object.Instantiate(diners.meals[7],diners.transform);meal.name="Worker meal 12";meal.position=new Vector3(-7,.83f,-29.5f);meal.gameObject.SetActive(false);
                diners.places=diners.places.Concat(new[]{place}).ToArray();diners.meals=diners.meals.Concat(new[]{meal}).ToArray();diners.bites=diners.bites.Concat(new[]{meal.Find("Lunch apple")}).ToArray();
                diners.workers=game.crowd.runners.Where(w=>!w.isPlayer).ToArray();EditorUtility.SetDirty(diners);
            }
            FactoryWorkerVisualInstaller.Apply(game.transform);FactoryReactionPresentationInstaller.RefreshMessages(game.transform);
        }
        static void Corridor(CardboardFactoryGame game)
        {
            var old=game.transform.Find("Arrival and escape corridor");if(old)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Arrival and escape corridor").transform;root.SetParent(game.transform,false);
            float east=CardboardFactoryWorld.WorkshopEast;var walls=FactoryWallsBoxInstaller.WallMaterials();var material=walls[0];
            var floor=game.GetComponentsInChildren<Renderer>().First(r=>r.name=="Main workshop floor surface");float y=floor.bounds.max.y;
            Box("Corridor floor",root,new Vector3(east+7,y-.12f,-2),new Vector3(14.4f,.24f,3.2f),FactoryPresentationInstaller.Paper("ArrivalCorridorFloor",Color.white,floor.sharedMaterial.mainTexture));
            foreach(float z in new[]{-3.7f,-.3f})
            {
                var wall=Box("Corridor cardboard wall",root,new Vector3(east+7,1.55f,z),new Vector3(14.4f,3.45f,.2f),material);
                FactoryWallsBoxInstaller.Paint(wall,z< -2?"ArrivalCorridorWallSouth":"ArrivalCorridorWallNorth",walls,z< -2?0:1,3.45f);
            }
            Box("Corridor ceiling",root,new Vector3(east+7,3.35f,-2),new Vector3(14.4f,.18f,3.6f),AssetDatabase.LoadAssetAtPath<Material>("Assets/Cardboard/Generated/DoorFrameCardboard.mat")??material);
            for(int i=0;i<5;i++)
            {
                var lamp=new GameObject("Corridor warm ceiling light");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(east+1.5f+i*2.7f,2.9f,-2);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.82f,.54f);light.intensity=1.6f;light.range=4.8f;light.shadows=LightShadows.None;
            }
            var gate=new GameObject("Escape at end of corridor");gate.transform.SetParent(root,false);gate.transform.localPosition=new Vector3(east+14.6f,1.3f,-2);
            var trigger=gate.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(1.3f,3.2f,3);var escape=gate.AddComponent<FactoryCorridorExit>();escape.game=game;escape.barrier=Box("Corridor exit barrier",root,new Vector3(east+14.1f,1.5f,-2),new Vector3(.18f,3.4f,3.4f),material).gameObject;
            var intro=game.GetComponent<FactoryIntro>();if(!intro)intro=game.gameObject.AddComponent<FactoryIntro>();game.intro=intro;intro.game=game;intro.glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Cardboard/Generated/GearPlacementOutline.mat");
            var canvasRoot=game.transform.Find("Opening tutorial captions");if(canvasRoot)Object.DestroyImmediate(canvasRoot.gameObject);
            var ui=new GameObject("Opening tutorial captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));ui.transform.SetParent(game.transform,false);
            var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=.065f;canvas.sortingOrder=90;
            var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=1;
            var text=new GameObject("English opening objective",typeof(RectTransform),typeof(Text),typeof(Shadow));text.transform.SetParent(ui.transform,false);
            var rect=text.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,45);rect.sizeDelta=new Vector2(1250,85);
            intro.hint=text.GetComponent<Text>();intro.hint.font=CardboardTypography.DefaultFont;intro.hint.fontSize=27;intro.hint.alignment=TextAnchor.MiddleCenter;intro.hint.color=new Color(1,.94f,.78f);intro.hint.raycastTarget=false;intro.hint.text="";
            text.GetComponent<Shadow>().effectDistance=new Vector2(2,-3);EditorUtility.SetDirty(intro);EditorUtility.SetDirty(game);
        }
        static void Interactions(CardboardFactoryGame game)
        {
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            foreach(var control in station.GetComponentsInChildren<FactoryStationControl>(true))
            {
                var collider=control.GetComponent<BoxCollider>();if(!collider)continue;collider.isTrigger=true;
                if(control.part==StationPart.Bath)
                {
                    var mesh=control.transform.parent.Find("Imported Box vessel");var bounds=BoundsIn(mesh,control.transform.parent);
                    control.transform.localPosition=new Vector3(bounds.center.x,bounds.max.y+.08f,bounds.center.z);collider.center=Vector3.zero;collider.size=new Vector3(bounds.size.x+.16f,.50f,bounds.size.z+.16f);
                }
                else if(control.part==StationPart.Guard)
                {
                    float buttonHeight=station.transform.Find("Grille lift button left of press").localPosition.y;
                    control.transform.localPosition=new Vector3(-.67f,buttonHeight+.07f,-.49f);collider.size=new Vector3(.5f,.36f,.5f);
                }
                EditorUtility.SetDirty(control.transform);EditorUtility.SetDirty(collider);
            }
            // The portal side infills duplicate the continuous passage walls exactly.
            var portal=game.transform.Find("Factory doorways/Canteen door");
            if(portal)foreach(var wall in portal.Cast<Transform>().Where(t=>t.name=="Doorway infill" && t.localScale.x<.5f).ToArray())Object.DestroyImmediate(wall.gameObject);
        }
        static void Apples(CardboardFactoryGame game)
        {
            var mesh=FactoryPresentationInstaller.Bake("Assets/Game_Assets/Cardboard/Apple/Apple.fbx","LunchApple",0,Vector3.zero,true,.115f);
            var material=FactoryPresentationInstaller.Paper("LunchApple",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Cardboard/Apple/Apple.png"));CardboardSurface.Configure(material);
            foreach(var tray in new[]{game.lunch.tray}.Concat(game.lunch.diners.meals))
            {
                var item=tray.Find("Lunch apple");if(!item)continue;var visual=item.GetComponentInChildren<MeshFilter>(true);
                visual.sharedMesh=mesh;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;visual.transform.localPosition=Vector3.zero;visual.GetComponent<Renderer>().sharedMaterial=material;
                foreach(var shape in item.GetComponentsInChildren<MeshCollider>()){shape.sharedMesh=mesh;shape.convex=true;EditorUtility.SetDirty(shape);}
                var plate=tray.Find("Plate • textured model");var plateBounds=BoundsIn(plate,tray);
                item.localPosition=new Vector3(-.10f,plateBounds.max.y+.008f-mesh.bounds.min.y,0);EditorUtility.SetDirty(item.transform);EditorUtility.SetDirty(visual);EditorUtility.SetDirty(visual.GetComponent<Renderer>());
            }
            var physics=game.lunch.physicsTray;physics.homes=physics.contents.Select(item=>item.transform.localPosition).ToArray();physics.rotations=physics.contents.Select(item=>item.transform.localRotation).ToArray();EditorUtility.SetDirty(physics);
        }
        static void English(CardboardFactoryGame game)
        {
            foreach(var text in game.GetComponentsInChildren<TextMesh>(true)){text.text=FactoryEnglish.Text(text.text);EditorUtility.SetDirty(text);}
            foreach(var text in game.GetComponentsInChildren<Text>(true)){text.text=FactoryEnglish.Text(text.text);EditorUtility.SetDirty(text);}
            foreach(var target in game.GetComponentsInChildren<FactoryInteractable>(true)){target.prompt=FactoryEnglish.Text(target.prompt);EditorUtility.SetDirty(target);}
            foreach(var prop in game.GetComponentsInChildren<FactoryPhysicsProp>(true)){prop.itemName=FactoryEnglish.Text(prop.itemName);EditorUtility.SetDirty(prop);}
        }
        [MenuItem("Cardboard/Fix grille lift button orientation and tint")]
        public static void FixGrilleButtonCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Fix grille buttons in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();int count=0;
            foreach(var station in game.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var button=station.transform.Find("Grille lift button left of press");if(!button)continue;
                button.localRotation=Quaternion.Euler(-90,0,0);EditorUtility.SetDirty(button);
                var renderer=button.Find("Grille lift button").GetComponent<Renderer>();
                var materials=renderer.sharedMaterials;materials[0]=FactoryWorkstationBuilder.GrilleButtonMaterial(materials[0]);
                renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);
                FactoryWorkstationBuilder.SeatGrilleButton(station);count++;
            }
            Physics.SyncTransforms();
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try
            {
                var button=game.station.transform.Find("Grille lift button left of press");
                var eye=game.station.transform.TransformPoint(new Vector3(-.67f,1.45f,-1.1f));
                camera.fieldOfView=45;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(button.position+Vector3.up*.035f-eye));
                CardboardFactorySceneBuilder.Capture("GrilleButtonUpright");
                var target=game.TargetForRay(new Ray(eye,button.position+Vector3.up*.035f-eye));
                bool reachable=target && target.GetComponent<FactoryStationControl>() && target.GetComponent<FactoryStationControl>().part==StationPart.Guard;
                float gap=BoundsIn(button.Find("Button bezel"),game.station.transform).min.y-BoundsIn(game.station.transform.Find("Table"),game.station.transform).max.y;
                File.WriteAllText("Library/FactoryGrilleButtonOrientation.txt","Updated "+count+" grille button assemblies; button seated on tabletop with green tint; bezel materials preserved; gap="+gap+"; player interaction reachable="+reachable);
            }
            finally{camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply opening in Edit Mode.");
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();Population(game);Corridor(game);Interactions(game);Apples(game);English(game);
                var hand=Camera.main.GetComponent<CardboardMarkerHand>();
                const string handPath="Assets/Cardboard/Generated/FirstPersonHand.mat";var handMaterial=AssetDatabase.LoadAssetAtPath<Material>(handPath);
                if(!handMaterial){handMaterial=new Material(Shader.Find("Cardboard/FirstPersonHand"));AssetDatabase.CreateAsset(handMaterial,handPath);}hand.paperTemplate=handMaterial;Camera.main.cullingMask|=1<<CardboardMarkerHand.HandLayer;EditorUtility.SetDirty(hand);EditorUtility.SetDirty(Camera.main);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                File.WriteAllText("Library/FactoryOpeningUpdate.txt","SUCCESS: corridor, escorted introduction, tutorial repair, printer camera reveal, twelve workers/four styles, Apple, enlarged interaction volumes, English scene text.");
            }
            catch(Exception error){File.WriteAllText("Library/FactoryOpeningUpdate.txt",error.ToString());Debug.LogException(error);}
        }
    }
}
