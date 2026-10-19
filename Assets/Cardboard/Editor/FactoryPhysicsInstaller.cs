using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryPhysicsInstaller
    {
        static PhysicsMaterial ContactMaterial()
        {
            const string path="Assets/Cardboard/Generated/PickupPhysics.physicMaterial";
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if(!material){material=new PhysicsMaterial("Cardboard props");AssetDatabase.CreateAsset(material,path);}
            material.staticFriction=.55f;material.dynamicFriction=.43f;material.bounciness=.07f;
            material.frictionCombine=PhysicsMaterialCombine.Average;material.bounceCombine=PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);return material;
        }
        static FactoryPhysicsProp Configure(Transform root,float mass,string title)
        {
            var body=root.GetComponent<Rigidbody>();if(!body)body=root.gameObject.AddComponent<Rigidbody>();
            body.mass=mass;
            var prop=root.GetComponent<FactoryPhysicsProp>();if(!prop)prop=root.gameObject.AddComponent<FactoryPhysicsProp>();
            prop.itemName=title;prop.Initialize();prop.RememberHome();
            root.gameObject.isStatic=false;root.gameObject.layer=8;
            foreach(var collider in root.GetComponentsInChildren<Collider>())collider.sharedMaterial=ContactMaterial();
            var target=root.GetComponent<FactoryInteractable>();if(!target)target=root.gameObject.AddComponent<FactoryInteractable>();
            target.action=FactoryAction.PhysicsProp;target.prompt="E - PICK UP "+title;
            EditorUtility.SetDirty(prop);EditorUtility.SetDirty(body);return prop;
        }
        public static void Prop(Transform root,bool barrel)
        {
            var capsule=barrel?root.GetComponent<CapsuleCollider>():null;
            if(capsule)
            {
                // Flat end caps let barrels stand and stack; a capsule of these proportions becomes a ball.
                const string path="Assets/Cardboard/Generated/BarrelPhysicsHull.asset";
                var hull=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!hull)
                {
                    const int sides=20;var vertices=new Vector3[sides*2+2];var triangles=new int[sides*12];
                    float radius=capsule.radius,height=capsule.height;
                    for(int i=0;i<sides;i++)
                    {
                        float angle=i*Mathf.PI*2/sides;
                        var point=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                        vertices[i]=point;vertices[i+sides]=point+Vector3.up*height;
                        int next=(i+1)%sides,k=i*12;
                        triangles[k]=i;triangles[k+1]=i+sides;triangles[k+2]=next;
                        triangles[k+3]=next;triangles[k+4]=i+sides;triangles[k+5]=next+sides;
                        triangles[k+6]=sides*2;triangles[k+7]=i;triangles[k+8]=next;
                        triangles[k+9]=sides*2+1;triangles[k+10]=next+sides;triangles[k+11]=i+sides;
                    }
                    vertices[sides*2+1]=Vector3.up*height;
                    hull=new Mesh{name="Convex barrel with flat end caps",vertices=vertices,triangles=triangles};
                    hull.RecalculateBounds();hull.RecalculateNormals();AssetDatabase.CreateAsset(hull,path);
                }
                Object.DestroyImmediate(capsule);
                var collider=root.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=hull;collider.convex=true;
            }
            Configure(root,barrel?4:1.6f,barrel?"barrel":"box");
        }
        static void FitInk(TextMesh text,Vector3 point,float width,float height)
        {
            var settings=new TextGenerationSettings
            {
                font=text.font,fontSize=text.fontSize,fontStyle=FontStyle.Normal,color=Color.white,
                scaleFactor=1,lineSpacing=1,textAnchor=TextAnchor.LowerLeft,
                horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow,
                generationExtents=Vector2.zero,generateOutOfBounds=true
            };
            using(var generator=new TextGenerator())
            {
                generator.Populate(text.text,settings);var vertices=generator.verts;
                Bounds bounds=default;bool first=true;
                for(int quad=0;quad+3<vertices.Count;quad+=4)
                {
                    if((vertices[quad].position-vertices[quad+2].position).sqrMagnitude<.001f)continue;
                    for(int corner=0;corner<4;corner++)
                    {var vertex=vertices[quad+corner].position;if(first){bounds=new Bounds(vertex,Vector3.zero);first=false;}else bounds.Encapsulate(vertex);}
                }
                float units=Mathf.Min(width/bounds.size.x,height/bounds.size.y);
                text.characterSize=units*10;
                text.transform.localPosition=point-new Vector3(bounds.min.x,bounds.min.y,0)*units;
            }
        }
        public static void Instruction(FactoryInstruction instruction)
        {
            instruction.physical=Configure(instruction.sheet,.22f,"instructions");
            instruction.physical.instructionSheet=true;
            instruction.sheet.GetComponent<FactoryInteractable>().action=FactoryAction.Instruction;
            InstructionHints(instruction);instruction.ResetFloor();EditorUtility.SetDirty(instruction);
        }
        public static void InstructionHints(FactoryInstruction instruction)
        {
            var bounds=instruction.sheet.GetComponent<MeshFilter>().sharedMesh.bounds;
            foreach(string name in new[]{"Instruction control hint","Flip instruction hint"})
            {var prior=instruction.sheet.Find(name);if(prior)Object.DestroyImmediate(prior.gameObject);}
            var hint=Group("Instruction control hint",instruction.sheet,new Vector3(bounds.min.x+.10f,bounds.min.y+.035f,bounds.min.z-.006f));
            var text=hint.gameObject.AddComponent<TextMesh>();text.font=CardboardTypography.DefaultFont;
            text.fontSize=48;text.characterSize=.030f;text.text="LBM - TROW, RBM - PUT";text.anchor=TextAnchor.LowerLeft;
            text.color=new Color(.14f,.10f,.06f);text.font.RequestCharactersInTexture(text.text,48);
            FitInk(text,hint.localPosition,bounds.size.x-.16f,.044f);instruction.controlHint=hint;instruction.flipHint=null;
            var ink=Paper("InstructionControlInk",Color.white,null,true);
            var label=hint.gameObject.AddComponent<CardboardWorldLabel>();label.ink=ink;label.Refresh();
            EditorUtility.SetDirty(instruction);
        }
        public static void InstructionHintsCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update instruction hints in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();InstructionHints(game.instruction);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Library/FactoryInstructionControls.txt",game.instruction.controlHint.GetComponent<TextMesh>().text);
        }
        public static void Cursor(FactoryMainMenu menu)
        {
            var lens=menu.lens;var old=lens.transform.Find("Volumetric cardboard cursor");
            if(old)Object.DestroyImmediate(old.gameObject);
            var mesh=Bake("Assets/Game_Assets/Buttons/Cursor/Cursor.fbx","MenuVolumeCursor",-90,Vector3.zero,true,1,true);
            var vertices=mesh.vertices;var normals=mesh.normals;var turn=Quaternion.Euler(0,0,45);
            Vector3 tip=Vector3.zero;float furthest=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=turn*vertices[i];if(normals.Length==vertices.Length)normals[i]=turn*normals[i];
                float projection=Vector3.Dot(vertices[i],new Vector3(-1,1,0).normalized);
                if(projection>furthest){furthest=projection;tip=vertices[i];}
            }
            tip.z=0;for(int i=0;i<vertices.Length;i++)vertices[i]-=tip;
            mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
            var root=Group("Volumetric cardboard cursor",lens.transform,Vector3.zero);
            var cursor=root.gameObject.AddComponent<FactoryVolumeCursor>();cursor.lens=lens;cursor.overlay=menu.uiCamera;cursor.menu=menu;
            cursor.model=MeshObject("Authored cursor • tip is pointer hotspot",root,mesh,
                Paper("MenuVolumeCursor",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game_Assets/Buttons/Cursor/Cursor.png")),Vector3.zero);
            cursor.model.gameObject.layer=10;cursor.modelHeight=mesh.bounds.size.y;menu.pointer=cursor;
            CardboardSurface.Configure(cursor.model.GetComponent<Renderer>());
            cursor.model.gameObject.SetActive(false);EditorUtility.SetDirty(menu);
        }
        public static void Apply(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();
            game.objectCarry=world.GetComponent<FactoryObjectCarry>();
            if(!game.objectCarry)game.objectCarry=world.gameObject.AddComponent<FactoryObjectCarry>();
            game.objectCarry.game=game;
            var storage=world.Find(FactoryStorageInstaller.RootName);
            if(storage)foreach(var collider in storage.GetComponentsInChildren<Collider>())
                Prop(collider.transform,collider.name.StartsWith("Barrel "));
            if(game.instruction)Instruction(game.instruction);
            foreach(var runner in world.GetComponentsInChildren<CardboardRunner>())
                if(!runner.isPlayer && !runner.GetComponent<FactoryWorkerImpact>())runner.gameObject.AddComponent<FactoryWorkerImpact>();
            if(game.mainMenu)Cursor(game.mainMenu);
            EditorUtility.SetDirty(game);
        }
        [MenuItem("Cardboard/Install physical props and volumetric cursor")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before installing physics.");
            var world=Object.FindFirstObjectByType<CardboardFactoryWorld>();Apply(world.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            File.WriteAllText("Library/FactoryPhysicsInstall.txt","SUCCESS: physical instruction, 34 barrels, 18 dry boxes, worker reactions and volumetric cursor saved.");
        }
    }
}
