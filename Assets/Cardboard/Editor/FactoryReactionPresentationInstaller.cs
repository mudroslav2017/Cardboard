using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryReactionPresentationInstaller
    {
        public const string NormalPath="Assets/Game_Assets/Cardboard/Suspicois/Normal.png";
        public const string AngryPath="Assets/Game_Assets/Cardboard/Suspicois/Angry.png";
        static Rect AlphaRect(string path)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Cannot read "+path);
                var pixels=texture.GetPixels32();int left=texture.width,right=0,bottom=texture.height,top=0;
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a>16){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                return Rect.MinMaxRect(Mathf.Max(0,left-3),Mathf.Max(0,bottom-3),Mathf.Min(texture.width,right+4),Mathf.Min(texture.height,top+4));
            }
            finally{Object.DestroyImmediate(texture);}
        }
        static Sprite Sprite(string path,string name,Rect rect)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprite=UnityEngine.Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=name;
            string output="Assets/Cardboard/Generated/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Sprite>(output);
            if(saved){EditorUtility.CopySerialized(sprite,saved);Object.DestroyImmediate(sprite);EditorUtility.SetDirty(saved);return saved;}
            AssetDatabase.CreateAsset(sprite,output);return sprite;
        }
        static void Faces(Transform root)
        {
            var normal=AlphaRect(NormalPath);var angry=AlphaRect(AngryPath);
            var common=Rect.MinMaxRect(Mathf.Min(normal.xMin,angry.xMin),Mathf.Min(normal.yMin,angry.yMin),Mathf.Max(normal.xMax,angry.xMax),Mathf.Max(normal.yMax,angry.yMax));
            var normalSprite=Sprite(NormalPath,"SuspicionNormal",common);var angrySprite=Sprite(AngryPath,"SuspicionAngry",common);
            var hud=root.GetComponentInChildren<FactoryMistakeHUD>(true);
            for(int i=0;i<hud.stars.Length;i++)
            {
                var image=hud.stars[i];image.gameObject.name="Suspicion face "+(i+1);image.sprite=normalSprite;image.preserveAspect=true;
                foreach(var fill in image.GetComponentsInChildren<Image>(true).Where(f=>f!=image).ToArray())Object.DestroyImmediate(fill.gameObject);
            }
            hud.gameObject.name="Shift mistakes • three suspicious faces";hud.fills=null;hud.SetFaces(normalSprite,angrySprite);
            EditorUtility.SetDirty(hud);
        }
        public static void Voices(Transform root)
        {
            foreach(var path in Directory.GetFiles("Assets/Game_Assets/Sounds","*.mp3").Where(p=>!System.IO.Path.GetFileNameWithoutExtension(p).Equals("Ring",StringComparison.OrdinalIgnoreCase)))
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as AudioImporter;
                if(importer && !importer.forceToMono){importer.forceToMono=true;importer.SaveAndReimport();}
            }
            foreach(var reaction in root.GetComponentsInChildren<FactoryWorkerImpact>(true))
            {
                var worker=reaction.GetComponent<CardboardRunner>();if(!worker || worker.isPlayer)continue;
                var node=worker.transform.Find("Worker voice • spatial");
                if(!node){node=new GameObject("Worker voice • spatial").transform;node.SetParent(worker.transform,false);}
                node.localPosition=new Vector3(0,1.6f,0);
                var speaker=node.GetComponent<AudioSource>();if(!speaker)speaker=node.gameObject.AddComponent<AudioSource>();
                if(reaction.voice && reaction.voice!=speaker)
                {
                    speaker.outputAudioMixerGroup=reaction.voice.outputAudioMixerGroup;
                    if(reaction.voice.transform==worker.transform)Object.DestroyImmediate(reaction.voice);
                }
                speaker.playOnAwake=false;speaker.loop=true;speaker.mute=false;speaker.volume=.55f;
                speaker.spatialBlend=1;speaker.spread=0;speaker.panStereo=0;speaker.dopplerLevel=0;
                speaker.minDistance=1.25f;speaker.maxDistance=16;speaker.rolloffMode=AudioRolloffMode.Logarithmic;
                reaction.voice=speaker;reaction.voiceVolume=.55f;reaction.voiceFadeIn=.2f;reaction.voiceFadeOut=.4f;
                EditorUtility.SetDirty(reaction);EditorUtility.SetDirty(speaker);
            }
        }
        static void Emotions(Transform root)
        {
            const string folder="Assets/Game_Assets/Cardboard/Massage/Massage_Reactions/";
            var names=new[]{"Mistake_On_Stanok","Dont_Jump","Dont_Run","Throw","Throw_2"};
            var meshes=new Mesh[names.Length];var materials=new Material[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                string path=folder+names[i]+".png";var rect=AlphaRect(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                float scale=Mathf.Min(.32f/rect.width,.24f/rect.height),x=rect.width*scale*.5f,y=rect.height*scale*.5f;
                string meshPath="Assets/Cardboard/Generated/MessageEmotion"+names[i]+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(!mesh){mesh=new Mesh{name="Message emotion "+names[i]};AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear();
                mesh.vertices=new[]{new Vector3(-x,-y,0),new Vector3(x,-y,0),new Vector3(x,y,0),new Vector3(-x,y,0)};
                mesh.uv=new[]{new Vector2(rect.xMin/texture.width,rect.yMin/texture.height),new Vector2(rect.xMax/texture.width,rect.yMin/texture.height),new Vector2(rect.xMax/texture.width,rect.yMax/texture.height),new Vector2(rect.xMin/texture.width,rect.yMax/texture.height)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);meshes[i]=mesh;
                string materialPath="Assets/Cardboard/Generated/MessageEmotion"+names[i]+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
                material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
                material.SetFloat("_Cull",0);material.doubleSidedGI=true;material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(material);materials[i]=material;
            }
            foreach(var reaction in root.GetComponentsInChildren<FactoryWorkerImpact>(true))
            {
                if(!reaction.message)continue;
                foreach(Transform child in reaction.message.Cast<Transform>().Where(t=>t.name.StartsWith("Emotion • ")).ToArray())Object.DestroyImmediate(child.gameObject);
                reaction.reactionIcons=new Transform[names.Length];
                float front=reaction.message.GetComponent<MeshFilter>().sharedMesh.bounds.max.z;
                for(int i=0;i<names.Length;i++)
                {
                    var icon=FactoryPresentationInstaller.MeshObject("Emotion • "+names[i],reaction.message,meshes[i],materials[i],new Vector3(0,.025f,front+.002f));
                    icon.gameObject.layer=9;var renderer=icon.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                    icon.gameObject.SetActive(false);reaction.reactionIcons[i]=icon;
                }
                EditorUtility.SetDirty(reaction);
            }
        }
        public static void Apply(Transform root){Voices(root);Faces(root);Emotions(root);}
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply reaction presentation in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();Apply(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            var hud=game.GetComponentInChildren<FactoryMistakeHUD>(true);var report=new StringBuilder();
            report.AppendLine("SUCCESS: 3 cropped face icons with the previous dim base and horizontal half-fill.");
            report.AppendLine("Normal="+hud.normalFace.rect+" Angry="+hud.angryFace.rect);
            foreach(var reaction in game.GetComponentsInChildren<FactoryWorkerImpact>())report.AppendLine(reaction.name+" voice="+reaction.voice.transform.localPosition+" volume="+reaction.voiceVolume+" spatial="+reaction.voice.spatialBlend+" fade="+reaction.voiceFadeIn+" / "+reaction.voiceFadeOut);
            File.WriteAllText("Library/FactoryReactionPresentation.txt",report.ToString());
        }
    }
}
