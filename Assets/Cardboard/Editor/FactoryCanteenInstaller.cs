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
            Chair(new Vector3(-7,0,-21.75f),180,"Player dining chair");
            var ceiling=furniture.Find("Canteen ceiling");if(ceiling)Object.DestroyImmediate(ceiling.gameObject);
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Canteen ceiling";roof.transform.SetParent(furniture,false);
            roof.transform.localPosition=new Vector3(-13,5.65f,-25);roof.transform.localScale=new Vector3(26,.18f,18);roof.layer=8;
            var wall=game.GetComponentsInChildren<Renderer>().First(r=>r.name=="Canteen west wall");
            var roofMaterial=FactoryPresentationInstaller.Paper("CanteenCeiling",new Color(.74f,.66f,.52f),wall.sharedMaterial.mainTexture);
            roofMaterial.mainTextureScale=new Vector2(4.3f,3);roof.GetComponent<Renderer>().sharedMaterial=roofMaterial;EditorUtility.SetDirty(roofMaterial);
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
