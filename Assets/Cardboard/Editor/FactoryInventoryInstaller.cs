using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;

namespace Cardboard.Editor
{
    public static class FactoryInventoryInstaller
    {
        public const string Root="Assets/Game_Assets/Buttons/Inventory_Slot/";
        public const float BoardWidth=4.02f*1.5f,BoardHeight=2*1.5f;
        static Texture2D Texture(string name,bool alpha=false)
        {
            string path=Root+name;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(!importer)throw new InvalidOperationException("Missing inventory texture: "+path);
            importer.wrapMode=TextureWrapMode.Clamp;importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=alpha;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Mesh Quad()
        {
            string path=FactoryPresentationInstaller.Generated+"InventoryMarkerQuad.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)return mesh;
            mesh=new Mesh{name="Inventory marker • original full image UV"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void OverlayPart(Transform part)
        {
            part.gameObject.layer=FactoryInventorySlot.Layer;
            var renderer=part.GetComponent<Renderer>();if(!renderer)return;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            CardboardSurface.Configure(renderer);
        }
        static void Outline(FactoryInventorySlot inventory)
        {
            var slot=inventory.visuals.Find("Inventory slot • supplied model and UV");
            var old=slot.Find("Inventory slot • four pixel black outline");if(old)Object.DestroyImmediate(old.gameObject);
            var source=slot.GetComponent<MeshFilter>().sharedMesh;var points=source.vertices;var indices=source.triangles;
            const int directions=16;
            var vertices=new Vector3[points.Length*directions];var offsets=new Vector2[vertices.Length];
            var triangles=new int[indices.Length*directions];
            for(int copy=0;copy<directions;copy++)
            {
                float angle=copy*Mathf.PI*2/directions;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                for(int i=0;i<points.Length;i++){vertices[copy*points.Length+i]=points[i];offsets[copy*points.Length+i]=direction;}
                for(int i=0;i<indices.Length;i++)triangles[copy*indices.Length+i]=indices[i]+copy*points.Length;
            }
            string meshPath=FactoryPresentationInstaller.Generated+"InventorySlotOutline.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);bool create=!mesh;
            if(create)mesh=new Mesh();else mesh.Clear();
            mesh.name="Inventory slot • screen-space silhouette";mesh.indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.vertices=vertices;mesh.uv2=offsets;mesh.triangles=triangles;mesh.RecalculateBounds();
            if(create)AssetDatabase.CreateAsset(mesh,meshPath);else EditorUtility.SetDirty(mesh);
            string materialPath=FactoryPresentationInstaller.Generated+"InventorySlotOutline.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Cardboard/InventorySlotOutline"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetFloat("_OutlinePixels",4);CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
            var part=MeshObject("Inventory slot • four pixel black outline",slot,mesh,material,Vector3.zero);OverlayPart(part);
            inventory.outline=part.GetComponent<MeshRenderer>();inventory.Layout();EditorUtility.SetDirty(inventory);
        }
        public static void StyleCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating inventory style.");
            var inventory=Camera.main.GetComponent<FactoryInventorySlot>();Outline(inventory);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);EditorSceneManager.SaveScene(inventory.gameObject.scene);
            File.WriteAllText("Library/FactoryInventoryStyle.txt","SUCCESS: gentle opposite view-relative tilt (3, 9, 3 degrees), black four-pixel screen-space silhouette; existing scene layout retained.");
        }
        public static void Create(Transform world)
        {
            Directory.CreateDirectory(FactoryPresentationInstaller.Generated);
            var game=world.GetComponent<CardboardFactoryGame>();var lens=game.mainMenu.lens;
            var old=lens.GetComponent<FactoryInventorySlot>();
            if(old)
            {
                if(old.overlay){lens.GetUniversalAdditionalCameraData().cameraStack.Remove(old.overlay);Object.DestroyImmediate(old.overlay.gameObject);}
                if(old.visuals)Object.DestroyImmediate(old.visuals.gameObject);
                Object.DestroyImmediate(old);
            }
            var oldLight=lens.transform.Find("Inventory • paper fill light");if(oldLight)Object.DestroyImmediate(oldLight.gameObject);
            var inventory=lens.gameObject.AddComponent<FactoryInventorySlot>();inventory.game=game;inventory.lens=lens;
            inventory.overlay=Group("Inventory • overlay camera",lens.transform,Vector3.zero).gameObject.AddComponent<Camera>();
            inventory.overlay.cullingMask=1<<FactoryInventorySlot.Layer;
            inventory.overlay.fieldOfView=50;inventory.overlay.nearClipPlane=.01f;inventory.overlay.farClipPlane=3;
            inventory.overlay.clearFlags=CameraClearFlags.Depth;
            var data=inventory.overlay.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;
            lens.GetUniversalAdditionalCameraData().cameraStack.Add(inventory.overlay);lens.cullingMask &= ~(1<<FactoryInventorySlot.Layer);
            inventory.visuals=Group("Inventory • angled cardboard slot",lens.transform,Vector3.zero);
            var mesh=Bake(Root+"Inventory slot.fbx","InventorySlot",-90,Vector3.zero,true,1);
            var slot=MeshObject("Inventory slot • supplied model and UV",inventory.visuals,mesh,Paper("InventorySlot",Color.white,Texture("Inventory slot.png")),Vector3.zero);
            OverlayPart(slot);Outline(inventory);
            var texture=Texture("Marker.png",true);
            var material=Paper("InventoryMarker",Color.white,texture);
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.2f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=(int)RenderQueue.AlphaTest;EditorUtility.SetDirty(material);
            inventory.markerHome=new Vector3(0,0,-mesh.bounds.extents.z-.008f);
            inventory.marker=MeshObject("Marker • supplied transparent artwork",inventory.visuals,Quad(),material,inventory.markerHome);
            inventory.marker.localScale=Vector3.one*.94f;OverlayPart(inventory.marker);
            string glowPath=FactoryPresentationInstaller.Generated+"InventoryMarkerGlow.mat";
            var glow=AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if(!glow){glow=new Material(Shader.Find("Cardboard/InventoryEdgeGlow"));AssetDatabase.CreateAsset(glow,glowPath);}
            glow.SetTexture("_BaseMap",texture);glow.SetColor("_GlowColor",Hex("FFE0A3"));glow.SetFloat("_Strength",0);CardboardSurface.Configure(glow);EditorUtility.SetDirty(glow);
            var halo=MeshObject("Marker • soft beige silhouette glow",inventory.marker,Quad(),glow,new Vector3(0,0,.001f));OverlayPart(halo);
            inventory.glow=halo.GetComponent<MeshRenderer>();
            var light=Group("Inventory • paper fill light",lens.transform,new Vector3(.25f,.35f,.05f)).gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.color=Hex("FFF1D7");light.range=2;light.intensity=.55f;
            light.shadows=LightShadows.None;light.cullingMask=1<<FactoryInventorySlot.Layer;
            inventory.Pose(0,false,false);
        }
        static Bounds BoundsOf(Transform root)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
        }
        public static void Backboards(Transform world,StringBuilder report=null)
        {
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var board=station.transform.Find("Workstation backboard");
                var renderer=board.GetComponent<MeshRenderer>();var before=renderer.bounds;float bottom=before.min.y;
                var table=BoundsOf(station.transform.Find("Table"));
                board.localScale=Vector3.Scale(board.localScale,new Vector3(BoardWidth/before.size.x,BoardHeight/before.size.y,1));
                var after=renderer.bounds;
                float depthShift=table.max.z+.003f-after.min.z;
                board.position+=new Vector3(table.center.x-after.center.x,bottom-after.min.y,depthShift);
                // These controls are mounted on the board, so retain their distance from its face.
                foreach(Transform child in station.transform)
                    if(child.name=="Gear type selector pivot" || child.name.StartsWith("Selector gear ") ||
                       child.name=="Pressure gauge mounted on backboard" || child.name.StartsWith("Pressure step ") ||
                       child.name=="Automatic work order printer")
                    {child.position+=Vector3.forward*depthShift;EditorUtility.SetDirty(child);}
                if(Mathf.Abs(renderer.bounds.min.y-bottom)>.001f)throw new InvalidOperationException("Backboard bottom moved.");
                report?.AppendLine(station.name+": board "+renderer.bounds.size.ToString("F4")+", bottom "+bottom.ToString("F4")+" preserved, centered on table X="+table.center.x.ToString("F4"));
                EditorUtility.SetDirty(board);
            }
        }
        public static void Materials(Transform world,StringBuilder report=null)
        {
            var materials=new HashSet<Material>();
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
                CardboardSurface.Configure(renderer);EditorUtility.SetDirty(renderer);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(AssetImporter.GetAtPath(path) is ModelImporter)continue;
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is Material material)materials.Add(material);
            }
            foreach(var material in materials){CardboardSurface.Configure(material);EditorUtility.SetDirty(material);}
            report?.AppendLine("Configured "+materials.Count+" materials: two-sided, maximum roughness; original textures/colors retained.");
        }
        public static void Apply(Transform world)
        {
            Create(world);Backboards(world);Materials(world);
        }
        [MenuItem("Cardboard/Update inventory and workstation backboards")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating inventory.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();EditorSceneManager.SaveScene(game.gameObject.scene);
            Directory.CreateDirectory("Backups/InventoryAndBoards");
            string backup="Backups/InventoryAndBoards/FactoryShift-before-inventory.unity";if(!File.Exists(backup))File.Copy(game.gameObject.scene.path,backup);
            var report=new StringBuilder();Create(game.transform);Backboards(game.transform,report);Materials(game.transform,report);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryInventoryUpdate.txt","SUCCESS\n"+report);
        }
        public static void Preview()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var camera=game.mainMenu.lens;
            var inventory=camera.GetComponent<FactoryInventorySlot>();var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            bool active=inventory.Visible,selected=inventory.Selection>.5f;
            try
            {
                camera.fieldOfView=CardboardCamera.GameplayFov;
                var eye=game.station.transform.TransformPoint(new Vector3(-1,1.85f,-4.5f));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,1.1f,0))-eye));
                inventory.Pose(2,true,false);CardboardFactorySceneBuilder.Capture("InventorySlotIdle");
                inventory.Pose(2,true,true);CardboardFactorySceneBuilder.Capture("InventorySlotSelected");
            }
            finally {inventory.Pose(2,active,selected);camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);}
        }
    }
}
