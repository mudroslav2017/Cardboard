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
            material.SetFloat("_OutlinePixels",8);CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
            var part=MeshObject("Inventory slot • four pixel black outline",slot,mesh,material,Vector3.zero);OverlayPart(part);
            inventory.outline=part.GetComponent<MeshRenderer>();inventory.Layout();EditorUtility.SetDirty(inventory);
        }
        public static void UpdateLayoutCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating inventory.");
            var inventory=Camera.main.GetComponent<FactoryInventorySlot>();inventory.ResetInertia();inventory.Layout();
            if(inventory.outline)
            {
                inventory.outline.sharedMaterial.SetFloat("_OutlinePixels",8);
                EditorUtility.SetDirty(inventory.outline.sharedMaterial);AssetDatabase.SaveAssets();
            }
            EditorUtility.SetDirty(inventory.visuals);
            EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);EditorSceneManager.SaveScene(inventory.gameObject.scene);
            File.WriteAllText("Library/FactoryInventoryLayout.txt","SUCCESS: slot and marker scaled to 2/3, subtle smoothed camera/locomotion inertia; eight-pixel outline.");
        }
        public static void StyleCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating inventory style.");
            var inventory=Camera.main.GetComponent<FactoryInventorySlot>();Outline(inventory);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);EditorSceneManager.SaveScene(inventory.gameObject.scene);
            File.WriteAllText("Library/FactoryInventoryStyle.txt","SUCCESS: gentle opposite view-relative tilt (3, 9, 3 degrees), black eight-pixel screen-space silhouette; existing scene layout retained.");
        }
        static Texture2D PaletteTexture()
        {
            string path=FactoryPresentationInstaller.Generated+"InventoryPaletteFibers.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture)return texture;
            const int size=192;
            texture=new Texture2D(size,size,TextureFormat.RGBA32,true)
                {name="Inventory palette • star cardboard fibers",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];var random=new System.Random(173);
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                // The mistake stars use this same seeded paper grain and diagonal fibers.
                float grain=.94f+(float)random.NextDouble()*.09f;
                if((x+y/4)%21<2)grain-=.08f;
                float edge=Mathf.Min(Mathf.Min(x,size-1-x),Mathf.Min(y,size-1-y))/(size*.5f);
                Color paper=edge<.026f?new Color(.28f,.18f,.095f):
                    edge<.065f?new Color(.93f,.73f,.40f):Color.white;
                pixels[y*size+x]=new Color(paper.r*grain,paper.g*grain,paper.b*grain,1);
            }
            texture.SetPixels(pixels);texture.Apply();
            AssetDatabase.CreateAsset(texture,path);return texture;
        }
        static Mesh PaletteCard()
        {
            string path=FactoryPresentationInstaller.Generated+"InventoryPaletteCard.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh)return mesh;
            var edge=new[]{new Vector2(-.18f,-.21f),new Vector2(-.19f,.16f),new Vector2(-.16f,.21f),
                new Vector2(.15f,.205f),new Vector2(.19f,.18f),new Vector2(.185f,-.17f),new Vector2(.16f,-.205f)};
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int face=0;face<2;face++)
            {
                int start=vertices.Count;float z=face==0?-.012f:.012f;
                vertices.Add(new Vector3(0,0,z));uv.Add(Vector2.one*.5f);
                foreach(var point in edge){vertices.Add(new Vector3(point.x,point.y,z));uv.Add(new Vector2(point.x/.38f+.5f,point.y/.42f+.5f));}
                for(int i=0;i<edge.Length;i++)
                {
                    triangles.Add(start);triangles.Add(start+1+(face==0?i:(i+1)%edge.Length));
                    triangles.Add(start+1+(face==0?(i+1)%edge.Length:i));
                }
            }
            for(int i=0;i<edge.Length;i++)
            {
                int start=vertices.Count;var a=edge[i];var b=edge[(i+1)%edge.Length];
                vertices.Add(new Vector3(a.x,a.y,-.012f));vertices.Add(new Vector3(a.x,a.y,.012f));
                vertices.Add(new Vector3(b.x,b.y,.012f));vertices.Add(new Vector3(b.x,b.y,-.012f));
                uv.Add(new Vector2(.005f,0));uv.Add(new Vector2(.015f,0));uv.Add(new Vector2(.015f,1));uv.Add(new Vector2(.005f,1));
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            mesh=new Mesh{name="Inventory palette • thick cut cardboard"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static TextMesh PaletteLabel(string value,Transform parent,Vector3 position,float height,Color color,Material ink)
        {
            var root=Group("Inventory shortcut • "+value,parent,Vector3.zero);
            var text=root.gameObject.AddComponent<TextMesh>();text.font=CardboardTypography.DefaultFont;
            text.fontSize=64;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.color=color;text.text=value;
            text.font.RequestCharactersInTexture(value,64,FontStyle.Normal);
            var label=root.gameObject.AddComponent<CardboardWorldLabel>();label.ink=ink;label.Refresh();
            var renderer=root.GetComponent<MeshRenderer>();var bounds=renderer.localBounds;
            root.localScale=Vector3.one*(height/Mathf.Max(.001f,bounds.size.y));
            root.localPosition=position-Vector3.Scale(bounds.center,root.localScale);
            OverlayPart(root);return text;
        }
        static void Palette(FactoryInventorySlot inventory)
        {
            var old=inventory.visuals.Find("Inventory • five cardboard ink colors");if(old)Object.DestroyImmediate(old.gameObject);
            var oldKey=inventory.visuals.Find("Inventory shortcut • F");if(oldKey)Object.DestroyImmediate(oldKey.gameObject);
            var root=Group("Inventory • five cardboard ink colors",inventory.visuals,Vector3.zero);
            root.localRotation=Quaternion.Euler(0,-22,0);
            var palette=root.gameObject.AddComponent<FactoryMarkerPalette>();root.gameObject.layer=FactoryInventorySlot.Layer;
            palette.cards=new Transform[5];palette.numbers=new TextMesh[5];
            var font=Paper("InventoryPaletteLettering",Color.white,null,true);
            CardboardSurface.Configure(font);EditorUtility.SetDirty(font);
            var colors=inventory.lens.GetComponent<CardboardMarker>().Colors;
            var mesh=PaletteCard();var texture=PaletteTexture();
            for(int i=0;i<5;i++)
            {
                var material=Paper("InventoryInkColor"+(i+1),colors[i],texture);
                CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
                palette.cards[i]=Group("Ink "+(i+1)+" • sliding cardboard",root,FactoryMarkerPalette.ClosedPosition);
                palette.cards[i].gameObject.layer=FactoryInventorySlot.Layer;
                var card=MeshObject("Colored cardboard",palette.cards[i],mesh,material,Vector3.zero);OverlayPart(card);
                palette.numbers[i]=PaletteLabel((i+1).ToString(),palette.cards[i],new Vector3(0,.30f,-.025f),.115f,Color.white,font);
            }
            palette.shortcut=PaletteLabel("F",inventory.visuals,new Vector3(-.25f,-.26f,inventory.markerHome.z-.018f),.16f,Color.black,font);
            inventory.palette=palette;palette.Pose(0,false,false);EditorUtility.SetDirty(inventory);
        }
        public static void PaletteCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before updating palette.");
            var inventory=Camera.main.GetComponent<FactoryInventorySlot>();Palette(inventory);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);EditorSceneManager.SaveScene(inventory.gameObject.scene);
            File.WriteAllText("Library/FactoryInventoryPalette.txt","SUCCESS: five sequential bouncing cardboard colors, white 1–5 and black F in the project font; original inventory and level retained.");
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
            Palette(inventory);inventory.Pose(0,false,false);
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
