using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryFinaleInstaller
    {
        public static Bounds BoundsOf(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        public static void Audit()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var text=new StringBuilder();
            var source=game.station.transform.Find("Table");
            text.AppendLine("Station table "+source.position+" rotation "+source.eulerAngles+" scale "+source.lossyScale+" bounds "+BoundsOf(source));
            foreach(var r in source.GetComponentsInChildren<Renderer>())text.AppendLine("  "+r.name+" "+r.bounds+" "+string.Join(",",r.sharedMaterials.Select(m=>m.name)));
            foreach(var t in game.GetComponentsInChildren<Transform>(true))
                if((t.name.ToLowerInvariant().Contains("table") || t.name.ToLowerInvariant().Contains("counter") || t.name.ToLowerInvariant().Contains("ceiling")) && t.position.z< -16)
                    text.AppendLine("Canteen "+AnimationUtility.CalculateTransformPath(t,game.transform)+" pos="+t.position+" scale="+t.lossyScale+(t.GetComponentsInChildren<Renderer>(true).Length>0?" bounds="+BoundsOf(t):""));
            for(int i=0;i<game.lunch.diners.places.Length;i++)text.AppendLine("Place "+i+"="+game.lunch.diners.places[i].position+" yaw="+game.lunch.diners.places[i].eulerAngles.y+" meal="+game.lunch.diners.meals[i].position);
            var chair=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game_Assets/Cardboard/Chair/chair.fbx"));
            try
            {
                text.AppendLine("Chair "+BoundsOf(chair.transform));
                var points=chair.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).Where(v=>v.y>1.4f).ToArray();
                text.AppendLine("Chair backrest average="+points.Aggregate(Vector3.zero,(a,b)=>a+b)/Mathf.Max(1,points.Length));
                foreach(var f in chair.GetComponentsInChildren<MeshFilter>())text.AppendLine("Mesh "+f.name+" "+f.sharedMesh.bounds+" pose="+f.transform.position+" rotation="+f.transform.eulerAngles+" scale="+f.transform.lossyScale);
            }
            finally{Object.DestroyImmediate(chair);}
            File.WriteAllText("Library/FactoryCanteenAudit.txt",text.ToString());
        }
        public static void Install(CardboardFactoryGame game)
        {
            var ending=game.GetComponent<FactoryEnding>();if(!ending)ending=game.gameObject.AddComponent<FactoryEnding>();
            ending.game=game;game.ending=ending;
            var old=game.transform.Find("Escape white glow");if(old)Object.DestroyImmediate(old.gameObject);
            var glow=new GameObject("Escape white glow").transform;glow.SetParent(game.transform,false);
            glow.localPosition=new Vector3(CardboardFactoryWorld.WorkshopEast+14.25f,1.55f,-2);ending.glow=glow;
            var material=FactoryPresentationInstaller.Paper("EscapeWhite",Color.white);
            material.shader=Shader.Find("Universal Render Pipeline/Unlit");material.SetColor("_BaseColor",Color.white);
            var core=GameObject.CreatePrimitive(PrimitiveType.Cube);core.name="White exit opening";core.transform.SetParent(glow,false);
            core.transform.localScale=new Vector3(.06f,3.2f,3.2f);core.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(core.GetComponent<Collider>());
            var halo=GameObject.CreatePrimitive(PrimitiveType.Quad);halo.name="Soft white exit glow";halo.transform.SetParent(glow,false);
            halo.transform.localPosition=Vector3.left*.07f;halo.transform.localRotation=Quaternion.Euler(0,-90,0);halo.transform.localScale=new Vector3(5.2f,4.9f,1);
            var haloMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Cardboard/Generated/EscapeHalo.mat");
            if(!haloMaterial){haloMaterial=new Material(Shader.Find("Cardboard/EscapeGlow"));AssetDatabase.CreateAsset(haloMaterial,"Assets/Cardboard/Generated/EscapeHalo.mat");}
            halo.GetComponent<Renderer>().sharedMaterial=haloMaterial;Object.DestroyImmediate(halo.GetComponent<Collider>());
            var light=glow.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=Color.white;light.intensity=5;light.range=7;light.shadows=LightShadows.None;
            glow.gameObject.SetActive(false);
            old=game.transform.Find("Ending white transition");if(old)Object.DestroyImmediate(old.gameObject);
            var ui=new GameObject("Ending white transition",typeof(RectTransform),typeof(Canvas));ui.transform.SetParent(game.transform,false);
            var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
            var panel=new GameObject("White screen",typeof(RectTransform),typeof(Image));panel.transform.SetParent(ui.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            ending.white=panel.GetComponent<Image>();ending.white.color=new Color(1,1,1,0);ending.white.raycastTarget=false;

            var hud=game.GetComponentInChildren<FactoryMistakeHUD>(true);
            var counter=hud.GetComponent<FactoryRepairCounter>();if(!counter)counter=hud.gameObject.AddComponent<FactoryRepairCounter>();
            counter.game=game;counter.inventory=Camera.main.GetComponent<FactoryInventorySlot>();
            old=hud.transform.Find("Repair count");if(old)Object.DestroyImmediate(old.gameObject);
            var label=new GameObject("Repair count",typeof(RectTransform),typeof(Text),typeof(Shadow));label.transform.SetParent(hud.transform,false);
            rect=label.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(32,-28);rect.sizeDelta=new Vector2(153,76);
            counter.label=label.GetComponent<Text>();counter.label.font=CardboardTypography.DefaultFont;counter.label.fontSize=44;counter.label.text="0 / 6";
            counter.label.color=new Color(1,.94f,.8f);counter.label.alignment=TextAnchor.MiddleLeft;counter.label.raycastTarget=false;
            label.GetComponent<Shadow>().effectColor=new Color(.07f,.04f,.02f,.95f);label.GetComponent<Shadow>().effectDistance=new Vector2(2,-3);
            old=counter.inventory.overlay.transform.Find("Repair counter 3D gear");if(old)Object.DestroyImmediate(old.gameObject);
            var icon=new GameObject("Repair counter 3D gear").transform;icon.SetParent(counter.inventory.overlay.transform,false);counter.icon=icon;
            FactoryGeometry.GearVisual(icon,game.gears[0].recipe,game.gearMeshes,game.hubMeshes,game.gearPaper,game.gearEdge,game.gearInk);
            foreach(var t in icon.GetComponentsInChildren<Transform>(true))t.gameObject.layer=FactoryInventorySlot.Layer;
            foreach(var r in icon.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            counter.Refresh();
            foreach(var ambience in game.GetComponentsInChildren<FactoryMechanismAudio>(true))
            {
                var retained=ambience.speakers.Select((s,i)=>new{source=s,volume=ambience.volumes[i]}).Where(s=>s.source && s.source.clip && !Removed(s.source.clip)).ToArray();
                ambience.speakers=retained.Select(s=>s.source).ToArray();ambience.volumes=retained.Select(s=>s.volume).ToArray();EditorUtility.SetDirty(ambience);
            }
            foreach(var speaker in game.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip && Removed(s.clip)).ToArray())Object.DestroyImmediate(speaker.gameObject);
            EditorUtility.SetDirty(ending);EditorUtility.SetDirty(game);EditorUtility.SetDirty(counter);EditorUtility.SetDirty(material);
        }
        static bool Removed(AudioClip clip)=>clip.name.Replace(" ","").Equals("большоймеханизм2",StringComparison.OrdinalIgnoreCase);
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install finale in Edit Mode.");
            try
            {
                var game=Object.FindFirstObjectByType<CardboardFactoryGame>();string apple=FactoryAudioInstaller.ApplePose(game.lunch);
                Install(game);
                if(FactoryAudioInstaller.ApplePose(game.lunch)!=apple)throw new InvalidOperationException("Apple pose changed.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
                File.WriteAllText("Library/FactoryFinaleUpdate.txt","SUCCESS: six-repair ending, 3D counter, white exit. Removed big mechanism 2. Preserved apple and machine poses.");
            }
            catch(Exception error){File.WriteAllText("Library/FactoryFinaleUpdate.txt",error.ToString());Debug.LogException(error);}
        }
    }
}
