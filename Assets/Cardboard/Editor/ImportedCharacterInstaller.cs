using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardboard.Editor
{
    public static class ImportedCharacterInstaller
    {
        const string Root = "Assets/Cardboard/Resources/CardboardCharacters";
        static readonly string[] Names = {"HumanLera", "HumanKolya", "HumanNastya", "HumanMasha"};
        static readonly string[] Sources = {"Human Lera/Model/Human_Lera_1.fbx", "Human_Kolya/Human_Kolya.fbx", "Human Nastya/Model/Human_Nastya_1.fbx", "chel_masha/Model/chel.fbx"};
        static readonly string[] Textures = {"Human Lera/Texture/Human_Lera_Normal.png", "Human_Kolya/Kolya_Normal.png", "Human Nastya/Texture/Human_Nastya_Normal.png", "chel_masha/Texture/Masha_Normal.png"};
        const string SourceRoot = "Assets/Game_Assets/Cardboard/";

        static Quaternion Facing(int variant)=>Quaternion.Euler(0,variant%Names.Length==3?0:180,0);

        public static void Prepare()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            for(int index=0;index<Names.Length;index++)
            {
                string modelPath=SourceRoot+Sources[index];var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if(!importer)throw new InvalidOperationException("Missing character model: "+modelPath);
                if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                var materials=new Material[3];var states=new[]{"Normal","Suspicious","Angry"};
                for(int state=0;state<states.Length;state++)
                {
                    string texturePath=(SourceRoot+Textures[index]).Replace("_Normal","_"+states[state]);
                    var textureImporter=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if(!textureImporter)throw new InvalidOperationException("Missing character texture: "+texturePath);
                    textureImporter.textureType=TextureImporterType.Default;textureImporter.isReadable=true;textureImporter.sRGBTexture=true;
                    textureImporter.textureCompression=TextureImporterCompression.Uncompressed;textureImporter.npotScale=TextureImporterNPOTScale.None;
                    textureImporter.mipmapEnabled=true;textureImporter.filterMode=FilterMode.Bilinear;textureImporter.wrapMode=TextureWrapMode.Clamp;textureImporter.SaveAndReimport();
                    string path=Root+"/"+Names[index]+(state==0?"":"_"+states[state])+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
                    material.name=Names[index]+" • "+states[state]+" • both sides";material.color=Color.white;
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.mainTexture=material.GetTexture("_BaseMap");
                    CardboardSurface.Configure(material);EditorUtility.SetDirty(material);materials[state]=material;
                }
                var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                try
                {
                    var filters=instance.GetComponentsInChildren<MeshFilter>();
                    if(filters.Length!=1)throw new InvalidOperationException("Expected a single cardboard mesh in "+modelPath+", found "+filters.Length);
                    Mesh mesh=CanonicalMesh(filters[0]);mesh.name=Names[index]+" • original UV";
                    string meshPath=Root+"/"+Names[index]+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
                    var root=new GameObject(Names[index]);root.transform.localRotation=Facing(index);
                    try
                    {
                        root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[0];CardboardSurface.Configure(renderer);
                        var face=root.AddComponent<FactoryWorkerAppearance>();face.normal=materials[0];face.suspicious=materials[1];face.angry=materials[2];
                        PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+Names[index]+".prefab");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        public static void TurnMashaCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorApplication.isPlaying=false;EditorApplication.delayCall+=TurnMashaCurrent;return;}
            string path=Root+"/HumanMasha.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{prefab.transform.localRotation=Facing(3);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var scene=SceneManager.GetActiveScene();var meshAsset=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/HumanMasha.asset");int count=0;
            foreach(var root in scene.GetRootGameObjects())
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh!=meshAsset)continue;
                filter.transform.localRotation=Facing(3);EditorUtility.SetDirty(filter.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(filter.transform);count++;
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Library/FactoryMashaTurn.txt","SUCCESS: HumanMasha visual turned 180 degrees on "+count+" scene characters; prefab saved for future instances. Other character types unchanged.");
        }
        static Mesh CanonicalMesh(MeshFilter source)
        {
            var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
            Bounds raw=new Bounds(source.transform.TransformPoint(vertices[0]),Vector3.zero);
            foreach(var vertex in vertices)raw.Encapsulate(source.transform.TransformPoint(vertex));
            Quaternion turn=raw.size.y<Mathf.Min(raw.size.x,raw.size.z)?Quaternion.Euler(90,0,0):Quaternion.identity;
            Bounds stood=new Bounds(turn*source.transform.TransformPoint(vertices[0]),Vector3.zero);
            foreach(var vertex in vertices)stood.Encapsulate(turn*source.transform.TransformPoint(vertex));
            if(stood.size.x<stood.size.z)turn=Quaternion.Euler(0,90,0)*turn;
            Matrix4x4 pose=Matrix4x4.Rotate(turn)*source.transform.localToWorldMatrix;var normalPose=pose.inverse.transpose;
            Bounds bounds=default;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=pose.MultiplyPoint3x4(vertices[i]);if(i==0)bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);
                if(normals.Length==vertices.Length)normals[i]=normalPose.MultiplyVector(normals[i]).normalized;
            }
            // The face drawing is on the left UV island. Orient that island toward the walking direction,
            // using geometry rather than normal signs (the supplied sheets can have inward normals).
            float positiveU=0,negativeU=0;int positive=0,negative=0;
            for(int i=0;i<vertices.Length;i++)
            {
                if(normals.Length==vertices.Length && Mathf.Abs(normals[i].z)<.9f)continue;
                if(vertices[i].z>bounds.center.z){positiveU+=uv[i].x;positive++;}else{negativeU+=uv[i].x;negative++;}
            }
            bool reverse=positive>0 && negative>0 && positiveU/positive<negativeU/negative;
            float scale=2.31f/bounds.size.y;Vector3 origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=(vertices[i]-origin)*scale;
                if(reverse){vertices[i].x=-vertices[i].x;vertices[i].z=-vertices[i].z;if(normals.Length==vertices.Length)normals[i]=Quaternion.Euler(0,180,0)*normals[i];}
            }
            mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
            if(pose.determinant<0)for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
            {var triangles=mesh.GetTriangles(submesh);for(int i=0;i<triangles.Length;i+=3){int first=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=first;}mesh.SetTriangles(triangles,submesh);}
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        static void Replace(MeshFilter target, int variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + Names[variant % Names.Length] + ".prefab");
            target.sharedMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            target.GetComponent<MeshRenderer>().sharedMaterials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            target.name = Names[variant % Names.Length]; target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = prefab.transform.localRotation; target.transform.localScale = Vector3.one;
            var config=prefab.GetComponent<FactoryWorkerAppearance>();var appearance=target.GetComponent<FactoryWorkerAppearance>();
            if(!appearance)appearance=target.gameObject.AddComponent<FactoryWorkerAppearance>();
            appearance.normal=config.normal;appearance.suspicious=config.suspicious;appearance.angry=config.angry;EditorUtility.SetDirty(appearance);
            var collider = target.GetComponent<MeshCollider>(); if (collider) collider.sharedMesh = target.sharedMesh;
            EditorUtility.SetDirty(target); EditorUtility.SetDirty(target.GetComponent<MeshRenderer>());
        }
        public static int ReplaceInScene(Scene scene)
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var runner in root.GetComponentsInChildren<CardboardRunner>(true))
                {
                    var mesh = runner.puppet ? runner.puppet.GetComponentInChildren<MeshFilter>(true) : null;
                    if (mesh) { Replace(mesh, runner.personality); count++; }
                }
                foreach (var game in root.GetComponentsInChildren<CardboardFactoryGame>(true))
                {
                    var mesh = game.victim ? game.victim.GetComponentInChildren<MeshFilter>(true) : null;
                    if (mesh) { Replace(mesh, 0); count++; }
                }
            }
            if (count > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            return count;
        }
        public static void VerifyRuntime(Action<bool,string> check)
        {
            var game=CardboardFactoryGame.Instance;
            foreach(var runner in game.crowd.runners)
            {
                var mesh=runner.puppet.GetComponentInChildren<MeshFilter>(true);
                var material=mesh.GetComponent<MeshRenderer>().sharedMaterial;
                int variant=runner.personality%Names.Length;
                check(mesh.sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+Names[variant]+".asset") && Quaternion.Angle(mesh.transform.localRotation,Facing(variant))<.1f,"Imported character and forward orientation assigned to "+runner.name);
                check(material.GetTexture("_BaseMap")==AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+Textures[variant]) && material.color==Color.white && material.GetFloat("_Smoothness")==0 && material.GetFloat("_Metallic")==0,"Original base color and maximum roughness: "+mesh.name);
                check(material.GetFloat("_Cull")==0 && material.doubleSidedGI,"Both polygon sides are visible: "+mesh.name);
            }
            for(int variant=0;variant<Names.Length;variant++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoot+Sources[variant]).GetComponentInChildren<MeshFilter>().sharedMesh;
                var actual=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+Names[variant]+".asset");
                var a=source.uv;var b=actual.uv;bool same=a.Length==b.Length;
                for(int i=0;same && i<a.Length;i++) same=a[i]==b[i];
                check(same,"Authored UV coordinates preserved: "+Names[variant]);
                var runner=game.crowd.runners[variant+1];
                var surface=runner.puppet.GetComponentInChildren<CardboardDrawingSurface>();
                var oldPosition=runner.transform.position;var oldRotation=runner.transform.rotation;
                var marker=CardboardMarker.Instance;var camera=Camera.main;
                var oldCameraPosition=camera.transform.position;var oldCameraRotation=camera.transform.rotation;
                int oldColor=marker.colorIndex;
                try
                {
                    // Keep the drawing probe clear of factory furniture after floor-plan changes.
                    runner.transform.SetPositionAndRotation(new Vector3(200,0,200),Quaternion.identity);Physics.SyncTransforms();
                    var renderer=surface.GetComponent<MeshRenderer>();var atlas=(Texture2D)renderer.sharedMaterial.mainTexture;
                    Vector2 frontUV=Vector2.zero;Color frontColor=default;bool frontHit=false;
                    for(int side=0;side<2;side++)
                    {
                        Vector3 origin=surface.transform.TransformPoint(new Vector3(0,1.15f,side==0?2:-2));
                        Vector3 direction=surface.transform.TransformDirection(side==0?Vector3.back:Vector3.forward);
                        bool previousBackfaces=Physics.queriesHitBackfaces;
                        bool hit=CardboardDrawingSurface.RaycastForDrawing(new Ray(origin,direction),3,out var contact) && contact.collider==surface.GetComponent<MeshCollider>();
                        check(Physics.queriesHitBackfaces==previousBackfaces,"Drawing query restores global physics settings");
                        check(hit,"Imported mesh raycast reaches "+Names[runner.personality%Names.Length]+" side "+side);
                        if(!hit) continue;
                        check(CardboardDrawingSurface.SideOf(contact)==side,"Marker identifies imported face "+side);
                        float localZ=surface.transform.InverseTransformPoint(contact.point).z;
                        check(side==0?localZ>0:localZ<0,"Marker hits the nearest visible face instead of the opposite side");
                        Vector2 uv=contact.textureCoord;Color before=atlas.GetPixelBilinear(uv.x,uv.y);
                        camera.transform.SetPositionAndRotation(origin+direction,Quaternion.LookRotation(direction,surface.transform.up));
                        marker.SetDrawingMode(true);marker.colorIndex=side==0?4:2;
                        marker.TracePointer((Vector2)camera.WorldToScreenPoint(contact.point),true);
                        check(marker.Target==surface && marker.IsTouching,"Real marker pointer paints the visible character face");
                        check(Vector3.Dot(marker.ContactNormal,direction)<-.9f,"Marker nib stays outside the visible face despite inward normals");
                        var painted=side==0?surface.DrawingTexture:surface.BackDrawingTexture;
                        check(painted && painted!=atlas && Vector4.Distance(before,painted.GetPixelBilinear(uv.x,uv.y))>.1f,"Marker paints original UV atlas on side "+side);
                        if(side==0) {frontUV=uv;frontColor=painted.GetPixelBilinear(uv.x,uv.y);frontHit=true;}
                        else if(frontHit) check(Vector4.Distance(frontColor,painted.GetPixelBilinear(frontUV.x,frontUV.y))<.01f,"Painting the back preserves front drawing");
                    }
                    check(renderer.sharedMaterial.mainTexture!=atlas && AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+Names[runner.personality%Names.Length]+".mat").mainTexture==atlas,"Drawing leaves shared character assets unchanged");
                    surface.ClearDrawing();
                    if(frontHit) check(Vector4.Distance(atlas.GetPixelBilinear(frontUV.x,frontUV.y),surface.DrawingTexture.GetPixelBilinear(frontUV.x,frontUV.y))<.01f,"Clear restores supplied character texture");
                }
                finally
                {
                    marker.SetDrawingMode(false,false);marker.colorIndex=oldColor;
                    camera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);
                    runner.transform.SetPositionAndRotation(oldPosition,oldRotation);Physics.SyncTransforms();
                }
            }
            check(game.victim.GetComponentInChildren<MeshFilter>(true).sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/HumanLera.asset"),"Capture victim uses imported character");
        }
        [MenuItem("Cardboard/Use imported people")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before replacing characters.");
            try
            {
                Prepare(); var report = new StringBuilder();
                foreach (string path in new[] {CardboardFactorySceneBuilder.ScenePath, "Assets/Scenes/CardboardPlayground.unity"})
                {
                    if (!File.Exists(path)) continue;
                    Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    report.AppendLine(path + ": replaced " + ReplaceInScene(scene) + " character meshes");
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
                const string runnerPath = "Assets/Cardboard/Generated/CardboardRunner.prefab";
                if (File.Exists(runnerPath))
                {
                    var contents = PrefabUtility.LoadPrefabContents(runnerPath);
                    try { var runner = contents.GetComponent<CardboardRunner>(); Replace(runner.puppet.GetComponentInChildren<MeshFilter>(true), runner.personality); PrefabUtility.SaveAsPrefabAsset(contents, runnerPath); }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
                AssetDatabase.SaveAssets();
                report.AppendLine("SUCCESS: original UV atlases, URP Base Map, white Base Color, Metallic 0, Smoothness 0 (roughness 1), Cull Off (both sides).");
                File.WriteAllText("Library/ImportedCharactersReport.txt", report.ToString());
                CardboardFactorySceneBuilder.Capture("ImportedCharacters");
            }
            catch (Exception error) { File.WriteAllText("Library/ImportedCharactersReport.txt", error.ToString()); Debug.LogException(error); }
        }
    }
}
