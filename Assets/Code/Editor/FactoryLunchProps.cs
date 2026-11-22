using UnityEditor;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryLunchProps
    {
        public const string ModelPath="Assets/Game_Assets/Cardboard/Glass_Of_Water/Model/glass_Of_Water.fbx";
        public const string TexturePath="Assets/Game_Assets/Cardboard/Glass_Of_Water/Texture/IMG_2289.PNG";
        public const string CupName="Glass of water";
        public static float Surface(Transform tray,Vector3 position)
        {
            float height=0;bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                var model=tray.Find(FactoryTrayInstaller.VisualName);if(!model)model=tray.Find("podnos • normalized model");
                if(!model)return .02f;
                var ray=new Ray(tray.TransformPoint(new Vector3(position.x,2,position.z)),-tray.up);
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.gameObject.activeSelf)continue;
                    var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    try
                    {
                        Physics.SyncTransforms();
                        if(collider.Raycast(ray,out var hit,4))height=Mathf.Max(height,tray.InverseTransformPoint(hit.point).y);
                    }
                    finally{Object.DestroyImmediate(collider);}
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
            return height;
        }
        public static Transform Cup(Transform tray)
        {
            var remove=new System.Collections.Generic.List<GameObject>();
            foreach(Transform child in tray)
                if(child.name.StartsWith("Food cube") || child.name==CupName)remove.Add(child.gameObject);
            foreach(var old in remove)Object.DestroyImmediate(old);
            var mesh=Bake(ModelPath,"LunchGlassOfWater",0,Vector3.zero,true,.14f);
            var material=Paper("LunchGlassOfWater",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            Vector3 anchor=Vector3.zero;
            var source=tray.Find("podnos • normalized model");
            foreach(var renderer in source.GetComponentsInChildren<Renderer>(true))
                if(renderer.name==CupName)
                {
                    anchor=tray.InverseTransformPoint(renderer.bounds.center);
                    renderer.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
                }
            bool active=tray.gameObject.activeSelf;tray.gameObject.SetActive(true);
            try
            {
                anchor.y=Surface(tray,anchor)-mesh.bounds.min.y;
                var cup=MeshObject(CupName,tray,mesh,material,anchor);
                CardboardSurface.Configure(cup.GetComponent<Renderer>());return cup;
            }
            finally{tray.gameObject.SetActive(active);}
        }
        public static void Apply(FactoryLunch lunch)
        {
            lunch.food=new[]{Cup(lunch.tray)};
            if(lunch.diners)
                for(int i=0;i<lunch.diners.meals.Length;i++)lunch.diners.bites[i]=Cup(lunch.diners.meals[i]);
        }
    }
}
