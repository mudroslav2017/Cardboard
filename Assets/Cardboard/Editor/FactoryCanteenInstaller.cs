using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryCanteenInstaller
    {
        public static void Install(CardboardFactoryGame game)
        {
            var lunch=game.lunch;
            var furniture=game.transform.Find("Canteen furniture");
            if(!furniture){furniture=new GameObject("Canteen furniture").transform;furniture.SetParent(game.transform,false);}
            var source=game.station.transform.Find("Table");
            foreach(var name in new[]{"Lunch table • placeholder","Shared lunch table west • placeholder","Shared lunch table east • placeholder"})
            {
                var old=lunch.transform.Find(name);if(!old)continue;
                var bounds=FactoryFinaleInstaller.BoundsOf(old);
                var root=new GameObject(name.Replace(" • placeholder"," • workstation model")).transform;root.SetParent(furniture,false);
                var model=Object.Instantiate(source,root);model.name="Table";model.localPosition=Vector3.zero;model.rotation=source.rotation;model.localScale=source.lossyScale;
                var measured=FactoryFinaleInstaller.BoundsOf(model);
                root.localScale=new Vector3(bounds.size.x/measured.size.x,bounds.size.y/measured.size.y,bounds.size.z/measured.size.z);
                measured=FactoryFinaleInstaller.BoundsOf(root);root.position+=bounds.center-measured.center;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)
                {material.SetFloat("_Cull",0);material.doubleSidedGI=true;EditorUtility.SetDirty(material);}
                if(model.GetComponentsInChildren<Collider>().Length==0)
                {
                    var shape=root.gameObject.AddComponent<BoxCollider>();shape.center=root.InverseTransformPoint(bounds.center);shape.size=new Vector3(bounds.size.x/root.lossyScale.x,bounds.size.y/root.lossyScale.y,bounds.size.z/root.lossyScale.z);
                }
                foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=8;
                Object.DestroyImmediate(old.gameObject);
            }
            var oldChairs=furniture.Find("Dining chairs");if(oldChairs)Object.DestroyImmediate(oldChairs.gameObject);
            var chairs=new GameObject("Dining chairs").transform;chairs.SetParent(furniture,false);
            var mesh=FactoryPresentationInstaller.Bake("Assets/Game_Assets/Cardboard/Chair/chair.fbx","CanteenChair",0,Vector3.zero,true,.65f);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Cardboard/Chair/Без названия53_20261019161000 (2).png");
            if(!texture)throw new FileNotFoundException("Chair texture missing.");
            var chairMaterial=FactoryPresentationInstaller.Paper("CanteenChair",Color.white,texture);
            var floor=game.GetComponentsInChildren<Renderer>().First(r=>r.name=="Canteen floor surface").bounds.max.y;
            void Chair(Vector3 position,float yaw,string name)
            {
                var chair=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));chair.transform.SetParent(chairs,false);
                chair.GetComponent<MeshFilter>().sharedMesh=mesh;chair.GetComponent<MeshRenderer>().sharedMaterial=chairMaterial;
                chair.GetComponent<MeshCollider>().sharedMesh=mesh;chair.layer=8;
                chair.transform.SetPositionAndRotation(new Vector3(position.x,floor-mesh.bounds.min.y,position.z),Quaternion.Euler(0,yaw+90,0));
            }
            for(int i=0;i<lunch.diners.places.Length;i++)
            {
                var place=lunch.diners.places[i];Chair(place.position-place.forward*.82f,place.eulerAngles.y,"Worker dining chair "+(i+1));
            }
            Chair(new Vector3(-7,0,-32.25f),0,"Player dining chair");
            var ceiling=furniture.Find("Canteen ceiling");if(ceiling)Object.DestroyImmediate(ceiling.gameObject);
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Canteen ceiling";roof.transform.SetParent(furniture,false);
            roof.transform.localPosition=new Vector3(-13,5.65f,-25);roof.transform.localScale=new Vector3(26,.18f,18);roof.layer=8;
            var wall=game.GetComponentsInChildren<Renderer>().First(r=>r.name=="Canteen west wall");
            var roofMaterial=FactoryPresentationInstaller.Paper("CanteenCeiling",new Color(.74f,.66f,.52f),wall.sharedMaterial.mainTexture);
            roofMaterial.mainTextureScale=new Vector2(4.3f,3);roof.GetComponent<Renderer>().sharedMaterial=roofMaterial;EditorUtility.SetDirty(roofMaterial);Lighting(game);RemoveFloatingSigns(game);
        }
        public static int RemoveFloatingSigns(CardboardFactoryGame game)
        {
            int count=0;var lunch=game.lunch;
            foreach(var text in lunch.GetComponentsInChildren<TextMesh>(true))
            {
                if(text.gameObject.name!="Lunch sign" && text!=lunch.counterSign && text!=lunch.tableSign)continue;
                Object.DestroyImmediate(text.gameObject);count++;
            }
            lunch.counterSign=lunch.tableSign=null;EditorUtility.SetDirty(lunch);return count;
        }
        public static void RemoveFloatingSignsCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Remove canteen signs in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();int count=RemoveFloatingSigns(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryLunchSigns.txt","SUCCESS: removed "+count+" floating canteen signs; pickup and delivery interactions retained.");
        }
        public static void Lighting(CardboardFactoryGame game)
        {
            foreach(var lamp in game.GetComponentsInChildren<Light>(true))
            {
                if(lamp.name!="Canteen warm light")continue;
                lamp.intensity=Mathf.Max(lamp.intensity,6);lamp.range=Mathf.Max(lamp.range,14);
                EditorUtility.SetDirty(lamp);
            }
            var root=game.transform.Find("Canteen lunch lighting");
            if(!root){root=new GameObject("Canteen lunch lighting").transform;root.SetParent(game.transform,false);}
            var positions=new[]{new Vector3(-21,3.8f,-20),new Vector3(-5,3.8f,-20),new Vector3(-21,3.8f,-29),new Vector3(-5,3.8f,-29),new Vector3(-13,3.8f,-21),new Vector3(-13,3.8f,-30)};
            for(int i=0;i<positions.Length;i++)
            {
                string name="Canteen fill light "+(i+1);var node=root.Find(name);
                if(!node){node=new GameObject(name).transform;node.SetParent(root,false);}
                node.position=positions[i];var light=node.GetComponent<Light>();if(!light)light=node.gameObject.AddComponent<Light>();
                light.type=LightType.Point;light.color=new Color(1,.94f,.82f);light.intensity=4.2f;light.range=12;
                light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.lightmapBakeType=LightmapBakeType.Realtime;
                light.cullingMask=~((1<<10)|(1<<FactoryInventorySlot.Layer)|(1<<FactoryDeathPresentation.Layer));EditorUtility.SetDirty(light);
            }
            DeliveryLighting(game);
            game.lunch.EnsureTraySpotlight();game.lunch.traySpotlight.renderMode=LightRenderMode.ForcePixel;
            game.lunch.RefreshTrayLight();EditorUtility.SetDirty(game.lunch.traySpotlight);EditorUtility.SetDirty(game.lunch);
        }
        public static void DeliveryLighting(CardboardFactoryGame game)
        {
            var lunch=game.lunch;
            foreach(var node in lunch.GetComponentsInChildren<Transform>(true))
                if(node.name=="Put tray here • placeholder" || node.name=="Stand here to eat • placeholder")
                    Object.DestroyImmediate(node.gameObject);
            var root=game.transform.Find("Canteen lunch lighting");
            if(!root){root=new GameObject("Canteen lunch lighting").transform;root.SetParent(game.transform,false);}
            var spot=root.Find("Canteen delivery spotlight");
            if(!spot){spot=new GameObject("Canteen delivery spotlight").transform;spot.SetParent(root,false);}
            spot.SetPositionAndRotation(lunch.destination.position+Vector3.up*3.8f,Quaternion.Euler(90,0,0));
            var light=spot.GetComponent<Light>();if(!light)light=spot.gameObject.AddComponent<Light>();
            light.type=LightType.Spot;light.color=new Color(1,.97f,.86f);light.intensity=24;light.range=7;
            light.spotAngle=75;light.innerSpotAngle=48;light.shadows=LightShadows.Soft;
            light.shadowBias=.02f;light.shadowNormalBias=.05f;light.renderMode=LightRenderMode.ForcePixel;
            light.lightmapBakeType=LightmapBakeType.Realtime;
            light.cullingMask=~((1<<10)|(1<<FactoryInventorySlot.Layer)|(1<<FactoryDeathPresentation.Layer));EditorUtility.SetDirty(light);
        }
        public static void CarryUpdate(CardboardFactoryGame game)
        {
            var lunch=game.lunch;
            lunch.destination.position=new Vector3(-7,lunch.destination.position.y,-30.45f);
            lunch.destinationHit.transform.position=new Vector3(-7,lunch.destinationHit.transform.position.y,-30.45f);
            var chair=game.transform.Find("Canteen furniture/Dining chairs/Player dining chair");
            if(chair)chair.SetPositionAndRotation(new Vector3(-7,chair.position.y,-32.25f),Quaternion.Euler(0,90,0));
            lunch.physicsTray.ResetContents();
            var hud=game.GetComponentInChildren<FactoryLunchHUD>(true);
            if(hud && hud.hint && !hud.hint.GetComponent<UnityEngine.UI.Shadow>())
            {
                var shadow=hud.hint.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1.5f,-2);
            }
            Lighting(game);EditorUtility.SetDirty(lunch);EditorUtility.SetDirty(lunch.physicsTray);
        }
        [MenuItem("Cardboard/Update canteen tray carrying and lighting")]
        public static void CarryUpdateCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update tray carrying in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();CarryUpdate(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        public static void ShortLunchCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update lunch timing in Edit Mode.");
            var game=Object.FindAnyObjectByType<CardboardFactoryGame>();var crowd=game.lunch.diners;
            crowd.minimumLunch=27.5f;crowd.afterPlayer=7.5f;crowd.maximumLunch=35;
            EditorUtility.SetDirty(crowd);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryLunchTiming.txt","SUCCESS: minimum 27.5 s, after player 7.5 s, maximum 35 s; one ordinary lunch per shift.");
        }
        public static void LightingCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update lunch lighting in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();Lighting(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryLunchLighting.txt","SUCCESS: brighter canteen ceiling lamps, six warm fill lights and a dedicated bright tray spotlight.");
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install furniture in Edit Mode.");
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();string apple=FactoryAudioInstaller.ApplePose(game.lunch);
                Install(game);
                if(FactoryAudioInstaller.ApplePose(game.lunch)!=apple)throw new InvalidOperationException("Apple pose changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                File.WriteAllText("Library/FactoryCanteenUpdate.txt","SUCCESS: canteen ceiling, thirteen supplied textured double-sided chairs, three workstation-model dining tables. Preserved trays, apple and workstations.");
            }
            catch(Exception error){File.WriteAllText("Library/FactoryCanteenUpdate.txt",error.ToString());Debug.LogException(error);}
        }
    }
}
