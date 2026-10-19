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

namespace Cardboard.Editor
{
    public static class FactoryAtmosphereInstaller
    {
        public const string DustFolder="Assets/Game_Assets/Cardboard/Floor/Floor_Dust";
        public const string RootName="Factory atmosphere";
        public const string ProfilePath="Assets/Cardboard/Generated/FactoryAtmosphereProfile.asset";
        static readonly List<string> readable=new List<string>();
        static readonly StringBuilder report=new StringBuilder();
        static string Generated=>FactoryPresentationInstaller.Generated;
        static Transform Group(string name,Transform parent,Vector3 position)
        {var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=position;return root;}
        static void AO()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:UniversalRendererData",new[]{"Assets/Settings"}))
            {
                var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                var feature=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
                if(!feature)
                {
                    feature=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();feature.name="Factory contact ambient occlusion";
                    AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
                }
                var settings=new SerializedObject(feature);
                void Number(string field,float value){var p=settings.FindProperty("m_Settings."+field);if(p!=null)p.floatValue=value;}
                void Option(string field,int value){var p=settings.FindProperty("m_Settings."+field);if(p!=null)p.intValue=value;}
                void Flag(string field,bool value){var p=settings.FindProperty("m_Settings."+field);if(p!=null)p.boolValue=value;}
                Number("Intensity",1.05f);Number("Radius",.34f);Number("DirectLightingStrength",.45f);Number("Falloff",60);
                Flag("Downsample",false);Flag("AfterOpaque",false);Option("Source",1);Option("NormalSamples",2);
                Option("Samples",0);Option("BlurQuality",0);Option("AOMethod",0);Option("Mode",1);
                settings.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);feature.Create();
                renderer.SetDirty();EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
                report.AppendLine("AO: "+renderer.name+", intensity 1.05, radius 0.34m, full-resolution depth normals, 12 samples");
            }
        }
        static T Component<T>(VolumeProfile profile) where T:VolumeComponent
        {
            if(profile.TryGet<T>(out var component))return component;
            component=profile.Add<T>(false);AssetDatabase.AddObjectToAsset(component,profile);return component;
        }
        static void Grading(Transform root)
        {
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
            Component<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var color=Component<ColorAdjustments>(profile);color.postExposure.Override(.45f);color.contrast.Override(16);
            color.saturation.Override(-6);color.colorFilter.Override(Color.white);
            var white=Component<WhiteBalance>(profile);white.temperature.Override(4);white.tint.Override(2);
            var bloom=Component<Bloom>(profile);bloom.intensity.Override(.12f);bloom.threshold.Override(1.35f);
            bloom.scatter.Override(.5f);bloom.highQualityFiltering.Override(true);
            var vignette=Component<Vignette>(profile);vignette.intensity.Override(.16f);vignette.smoothness.Override(.45f);
            vignette.color.Override(new Color(.11f,.085f,.055f));vignette.rounded.Override(true);
            // URP versions with volume-driven SSAO use this component; older versions use the renderer feature.
            var type=typeof(UniversalRenderPipeline).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusionVolumeOverride");
            if(type!=null)
            {
                var ao=profile.components.FirstOrDefault(c=>c.GetType()==type);
                if(!ao){ao=profile.Add(type,false);AssetDatabase.AddObjectToAsset(ao,profile);}
                var data=new SerializedObject(ao);
                void Parameter(string field,float value,bool integer=false,bool boolean=false)
                {
                    var p=data.FindProperty(field);if(p==null)return;
                    p.FindPropertyRelative("m_OverrideState").boolValue=true;var v=p.FindPropertyRelative("m_Value");
                    if(boolean)v.boolValue=value!=0;else if(integer)v.intValue=(int)value;else v.floatValue=value;
                }
                Parameter("m_Mode",1,true);Parameter("m_Intensity",1.05f);Parameter("m_Radius",.34f);
                Parameter("m_DirectLightingStrength",.45f);Parameter("m_FalloffDistance",60);
                Parameter("m_SampleCount",2,true);Parameter("m_Downsample",0,false,true);Parameter("m_BlurQuality",0,true);
                data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(ao);
            }
            foreach(var component in profile.components)EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            var volume=Group("Global Volume • warm cardboard",root,Vector3.zero).gameObject.AddComponent<Volume>();
            volume.isGlobal=true;volume.priority=20;volume.weight=1;volume.sharedProfile=profile;
            var camera=Camera.main;var cameraData=camera.GetUniversalAdditionalCameraData();
            camera.allowHDR=true;cameraData.renderPostProcessing=true;cameraData.volumeLayerMask=1;
            cameraData.requiresDepthTexture=true;cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality=AntialiasingQuality.High;EditorUtility.SetDirty(cameraData);EditorUtility.SetDirty(camera);
            foreach(string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets/Settings"}))
            {
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var data=new SerializedObject(pipeline);data.FindProperty("m_SupportsHDR").boolValue=true;
                data.FindProperty("m_ColorGradingMode").intValue=1;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            }
            report.AppendLine("Global Volume: ACES, exposure +0.45, contrast +16, saturation -6, warm white balance, subtle bloom and vignette");
        }
        static Texture2D Texture(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing dust texture: "+path);
            if(!importer.isReadable)readable.Add(path);
            importer.isReadable=true;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=true;importer.anisoLevel=4;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Mesh SpriteMesh(Texture2D texture,string name)
        {
            var pixels=texture.GetPixels32();int width=texture.width,height=texture.height;
            int minX=width,minY=height,maxX=0,maxY=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>24)
            {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            if(minX>=maxX || minY>=maxY)throw new InvalidOperationException("Dust texture contains no visible alpha: "+texture.name);
            minX=Mathf.Max(0,minX-2);minY=Mathf.Max(0,minY-2);maxX=Mathf.Min(width-1,maxX+2);maxY=Mathf.Min(height-1,maxY+2);
            float w=maxX-minX+1,h=maxY-minY+1,longest=Mathf.Max(w,h);w/=longest;h/=longest;
            var mesh=new Mesh{name=name};
            mesh.vertices=new[]{new Vector3(-w/2,-h/2,0),new Vector3(w/2,-h/2,0),new Vector3(w/2,h/2,0),new Vector3(-w/2,h/2,0)};
            mesh.normals=Enumerable.Repeat(Vector3.back,4).ToArray();
            mesh.uv=new[]{new Vector2((float)minX/width,(float)minY/height),new Vector2((float)(maxX+1)/width,(float)minY/height),
                new Vector2((float)(maxX+1)/width,(float)(maxY+1)/height),new Vector2((float)minX/width,(float)(maxY+1)/height)};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Generated+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!saved){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            else
            {
                saved.Clear();saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;
                saved.triangles=mesh.triangles;saved.tangents=mesh.tangents;saved.bounds=mesh.bounds;saved.UploadMeshData(false);
                Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);
            }
            report.AppendLine(name+": alpha bounds "+minX+","+minY+" — "+maxX+","+maxY+", original artwork aspect preserved");
            return saved;
        }
        static Material Cutout(Texture2D texture,string name)
        {
            var material=FactoryPresentationInstaller.Paper(name,Color.white,texture);
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.18f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=(int)RenderQueue.AlphaTest;
            material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }
        static Texture2D MoteTexture()
        {
            string path=Generated+"AirDustDot.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture)return texture;
            texture=new Texture2D(32,32,TextureFormat.RGBA32,true){name="Soft airborne paper dust",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.2f));
            }
            texture.SetPixels(pixels);texture.Apply(true);AssetDatabase.CreateAsset(texture,path);return texture;
        }
        static Material MoteMaterial()
        {
            string path=Generated+"AirborneBeigeDust.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",MoteTexture());material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
            material.SetFloat("_CameraFadingEnabled",1);material.SetFloat("_CameraNearFadeDistance",.25f);material.SetFloat("_CameraFarFadeDistance",.8f);
            material.EnableKeyword("_FADING_ON");material.SetVector("_CameraFadeParams",new Vector4(.25f,1/(.8f-.25f),0,0));
            CardboardSurface.Configure(material);EditorUtility.SetDirty(material);return material;
        }
        static void Air(Transform root,string name,Vector3 center,Vector3 size,int particles,float rate,Material material)
        {
            var system=Group(name,root,center).gameObject.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.loop=true;main.duration=30;main.prewarm=true;main.startLifetime=new ParticleSystem.MinMaxCurve(24,38);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.008f,.025f);main.startSize=new ParticleSystem.MinMaxCurve(.01f,.026f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.65f,.52f,.34f,.30f),new Color(.92f,.80f,.57f,.58f));
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=particles;main.playOnAwake=true;
            var emission=system.emission;emission.rateOverTime=rate;
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
            var noise=system.noise;noise.enabled=true;noise.strength=.035f;noise.frequency=.18f;noise.scrollSpeed=.06f;noise.quality=ParticleSystemNoiseQuality.Low;
            var color=system.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(1,.8f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.maxParticleSize=.025f;
            CardboardSurface.Configure(renderer);system.useAutoRandomSeed=false;system.randomSeed=(uint)(particles+17);
        }
        static void Shavings(Transform world,Transform root)
        {
            var meshes=new Mesh[3];var materials=new Material[3];
            for(int i=0;i<3;i++){var texture=Texture(DustFolder+"/"+(11+i*11)+".png");meshes[i]=SpriteMesh(texture,"CardboardShaving_"+(i+1));materials[i]=Cutout(texture,"CardboardShaving_"+(i+1));}
            var floor=Group("Chip floor collision plane",root,new Vector3(0,FactoryEnvironmentInstaller.FloorY(world)+.002f,0));
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var old=station.transform.Find("Cutting debris");if(old)Object.DestroyImmediate(old.gameObject);
                var holder=Group("Cutting debris",station.transform,station.cuttingPosition+new Vector3(0,.035f,-.16f));
                holder.localRotation=Quaternion.LookRotation(new Vector3(0,.65f,-1));
                var effects=station.GetComponent<FactoryMachiningVFX>();if(!effects)effects=station.gameObject.AddComponent<FactoryMachiningVFX>();
                effects.shavings=new ParticleSystem[3];station.machiningEffects=effects;
                for(int i=0;i<3;i++)
                {
                    var system=Group("Shavings "+(11+i*11),holder,Vector3.zero).gameObject.AddComponent<ParticleSystem>();
                    system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);effects.shavings[i]=system;
                    var main=system.main;main.loop=true;main.duration=5;main.playOnAwake=true;main.maxParticles=80;
                    main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=new ParticleSystem.MinMaxCurve(2,3.5f);
                    main.startSpeed=new ParticleSystem.MinMaxCurve(.75f,1.65f);main.startSize=new ParticleSystem.MinMaxCurve(.10f,.23f);
                    main.gravityModifier=.35f;main.startRotation3D=true;
                    main.startRotationX=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationY=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationZ=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                    var emission=system.emission;emission.enabled=false;
                    var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=32;shape.radius=.10f;
                    var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
                    rotation.x=new ParticleSystem.MinMaxCurve(.8f,3);rotation.y=new ParticleSystem.MinMaxCurve(.4f,1.4f);rotation.z=new ParticleSystem.MinMaxCurve(.8f,3);
                    var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.72f,1),new Keyframe(1,0)));
                    var collision=system.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;collision.SetPlane(0,floor);
                    collision.bounce=.18f;collision.dampen=.55f;collision.radiusScale=.08f;
                    var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
                    renderer.mesh=meshes[i];renderer.sharedMaterial=materials[i];CardboardSurface.Configure(renderer);
                    system.useAutoRandomSeed=false;system.randomSeed=(uint)(i+41);
                }
                EditorUtility.SetDirty(effects);EditorUtility.SetDirty(station);
            }
            report.AppendLine("Shavings: authored 11 / 22 / 33 textures, 180 chips per full cut, spinning and bouncing on the actual floor; emitted only by accepted cutting progress");
        }
        static void FloorDust(Transform world,Transform root)
        {
            var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{DustFolder}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path=>!new[]{"11","22","33"}.Contains(Path.GetFileNameWithoutExtension(path))).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            var meshes=new Mesh[paths.Length];var materials=new Material[paths.Length];
            for(int i=0;i<paths.Length;i++){var texture=Texture(paths[i]);meshes[i]=SpriteMesh(texture,"FloorDust_"+i);materials[i]=Cutout(texture,"FloorDust_"+i);}
            var previous=root.Find("Floor • settled dust and offcuts");if(previous)Object.DestroyImmediate(previous.gameObject);
            var holder=Group("Floor • settled dust and offcuts",root,Vector3.zero);
            var random=new System.Random(172012);int index=0;float floor=FactoryEnvironmentInstaller.FloorY(world);
            float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            void Patch(Vector3 point,float size)
            {
                if(point.x<CardboardFactoryWorld.WorkshopWest+.8f || point.x>CardboardFactoryWorld.WorkshopEast-.8f)return;
                if(Mathf.Abs(point.x+13)<1.8f && point.z<-12.3f && point.z>-18.5f)return;
                foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>())
                {var p=point-station.pumpPlate.position;if(Mathf.Abs(p.x)<1 && Mathf.Abs(p.z)<.85f)return;}
                int type=index%paths.Length;var item=Group("Floor dust "+(++index)+" • "+Path.GetFileNameWithoutExtension(paths[type]),holder,new Vector3(point.x,floor+.003f+(index%3)*.0003f,point.z));
                if(type<2)size*=.32f;else if(type==2)size*=.75f;
                item.localRotation=Quaternion.Euler(90,Range(0,360),0);item.localScale=Vector3.one*size;
                item.gameObject.AddComponent<MeshFilter>().sharedMesh=meshes[type];var renderer=item.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=materials[type];CardboardSurface.Configure(renderer);
            }
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
                for(int i=0;i<26;i++)Patch(station.transform.TransformPoint(new Vector3(Range(-3.8f,5.5f),0,Range(-2.1f,.5f))),Range(.30f,1.05f));
            for(int i=0;i<105;i++)
            {
                float x=Range(CardboardFactoryWorld.WorkshopWest+1.2f,CardboardFactoryWorld.WorkshopEast-1.2f),z=Range(-12.5f,12.5f);
                if(i<44)z=i%2==0?Range(-13,-11):Range(11,13);
                Patch(new Vector3(x,0,z),Range(.3f,1.3f));
            }
            for(int i=0;i<45;i++)Patch(new Vector3(Range(-24.7f,-1.3f),0,Range(-32.7f,-17.5f)),Range(.25f,.8f));
            report.AppendLine("Floor debris: "+index+" grounded patches using all "+paths.Length+" dust, sawdust and tape variants; no colliders, doors and pump plates kept clear");
        }
        static void Haze(Transform world,Transform root)
        {
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;
            RenderSettings.fogDensity=.0065f;RenderSettings.fogColor=CardboardArt.Hex("706452");
            var previous=root.Find("Light • subtle warm scattering");if(previous)Object.DestroyImmediate(previous.gameObject);
            var holder=Group("Light • subtle warm scattering",root,Vector3.zero);
            string path=Generated+"FactoryLightScattering.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Cardboard/SoftLightScattering"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_ScatterColor",new Color(1.10f,.86f,.56f,1));material.SetFloat("_Density",.026f);
            CardboardSurface.Configure(material);EditorUtility.SetDirty(material);
            float floor=FactoryEnvironmentInstaller.FloorY(world);int count=0;
            foreach(var light in world.GetComponentsInChildren<Light>(true))
            {
                if(light.name!="Practical workshop lamp" && light.name!="Canteen warm light")continue;
                float height=Mathf.Max(.5f,light.transform.position.y-floor-.06f);
                var volume=GameObject.CreatePrimitive(PrimitiveType.Cube);volume.name="Depth-clipped warm lamp haze "+(++count);
                volume.transform.SetParent(holder,false);volume.transform.position=light.transform.position-Vector3.up*(height*.5f);
                volume.transform.localScale=new Vector3(height*.90f,height,height*.90f);
                Object.DestroyImmediate(volume.GetComponent<Collider>());
                var renderer=volume.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            Camera.main.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            report.AppendLine("Subtle exponential haze (0.0065) and "+count+" warm scattering volumes, clipped against scene depth; no collisions or solid shadow casters.");
        }
        public static void Enhance(Transform world)
        {
            report.Clear();readable.Clear();var root=world.Find(RootName);
            if(!root)throw new InvalidOperationException("Existing factory atmosphere is missing.");
            foreach(var system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if(system.transform.parent!=root)continue;
                bool canteen=system.name.StartsWith("Canteen");var main=system.main;
                main.maxParticles=canteen?1100:3600;main.startSize=new ParticleSystem.MinMaxCurve(.01f,.026f);
                main.startColor=new ParticleSystem.MinMaxGradient(new Color(.65f,.52f,.34f,.30f),new Color(.92f,.80f,.57f,.58f));
                var emission=system.emission;emission.rateOverTime=canteen?28:95;EditorUtility.SetDirty(system);
            }
            try {FloorDust(world,root);Haze(world,root);}
            finally
            {
                foreach(string path in readable.Distinct())
                {var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.isReadable=false;importer.SaveAndReimport();}
            }
            report.AppendLine("Airborne paper dust: workshop emission 95/s, canteen 28/s, approximately three times previous density.");
            File.WriteAllText("Library/FactoryAtmosphereEnhancement.txt",report.ToString());
        }
        public static void Apply(Transform world)
        {
            report.Clear();readable.Clear();Directory.CreateDirectory(Generated);
            var old=world.Find(RootName);if(old)Object.DestroyImmediate(old.gameObject);
            var root=Group(RootName,world,Vector3.zero);
            try
            {
                AO();Grading(root);var material=MoteMaterial();
                Air(root,"Workshop • slow beige airborne dust",new Vector3(CardboardFactoryWorld.WorkshopCenterX,2.5f,0),new Vector3(42,4,25),3600,95,material);
                Air(root,"Canteen • slow beige airborne dust",new Vector3(-13,2.4f,-25),new Vector3(23,3.8f,15),1100,28,material);
                Shavings(world,root);FloorDust(world,root);Haze(world,root);
            }
            finally
            {
                foreach(string path in readable.Distinct())
                {var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.isReadable=false;importer.SaveAndReimport();}
            }
            File.WriteAllText("Library/FactoryAtmosphereUpdate.txt",report.ToString());
        }
        [MenuItem("Cardboard/Apply factory atmosphere and debris")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Apply atmosphere in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();EditorSceneManager.SaveScene(game.gameObject.scene);
            Directory.CreateDirectory("Backups/FactoryAtmosphere");
            foreach(string path in new[]{game.gameObject.scene.path,"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset","Assets/Settings/PC_RPAsset.asset","Assets/Settings/Mobile_RPAsset.asset"})
            {string backup="Backups/FactoryAtmosphere/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);}
            Apply(game.transform);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            Preview();
        }
        public static void Preview()
        {
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();var root=game.transform.Find(RootName);
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var systems=root.GetComponentsInChildren<ParticleSystem>();
            try
            {
                foreach(var system in systems){system.Simulate(30,true,true);system.Pause();}
                camera.fieldOfView=CardboardCamera.GameplayFov;
                var eye=new Vector3(CardboardFactoryWorld.WorkshopEast-3,2,-8);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(-13,1.8f,8)-eye));
                CardboardFactorySceneBuilder.Capture("FactoryAtmosphere");
                eye=game.station.transform.TransformPoint(new Vector3(-1.8f,1.35f,-2.2f));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(0,.65f,0))-eye));
                CardboardFactorySceneBuilder.Capture("AtmosphereWorkbench");
                eye=game.station.transform.TransformPoint(new Vector3(3,.65f,-4));
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(game.station.transform.TransformPoint(new Vector3(2,0,-.5f))-eye));
                CardboardFactorySceneBuilder.Capture("FactoryFloorDebris");
            }
            finally
            {
                foreach(var system in systems)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(position,rotation);
            }
        }
    }
}
