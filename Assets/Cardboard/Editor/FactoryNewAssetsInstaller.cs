using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Cardboard.CardboardArt;
using static Cardboard.Editor.FactoryPresentationInstaller;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryNewAssetsInstaller
    {
        const string Root="Assets/Game_Assets/Cardboard/";
        public static void Presses(Transform world)
        {
            var moving=Paper("TexturedPress",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Press/Texture/IMG_2271.PNG"));
            var bed=Paper("TexturedUnderPress",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Under_Press/TExture/IMG_2270.PNG"));
            // The updated exports have origins at zero; normalize them independently of the old assembly origin.
            var pressMesh=Bake(Root+"Press/Model/Press.fbx","TexturedPress",90,Vector3.zero,true);
            var bedMesh=Bake(Root+"Under_Press/Model/Under_Press.fbx","TexturedUnderPress",90,Vector3.zero,true);
            foreach(var station in world.GetComponentsInChildren<FactoryWorkstation>(true))
            {
                var oldBed=station.transform.Find("Under press");if(oldBed)Object.DestroyImmediate(oldBed.gameObject);
                if(station.press)Object.DestroyImmediate(station.press.gameObject);
                float blankBottom=station.cuttingPosition.y-station.sheet.localScale.y*.5f;
                var basePosition=new Vector3(station.cuttingPosition.x,blankBottom-bedMesh.bounds.extents.y,station.cuttingPosition.z);
                MeshObject("Under press",station.transform,bedMesh,bed,basePosition);
                station.press=MeshObject("Moving press",station.transform,pressMesh,moving,
                    new Vector3(station.cuttingPosition.x,1.65f+pressMesh.bounds.extents.y*.65f,station.cuttingPosition.z));
                station.press.localScale=new Vector3(1,.65f,1);
                station.RebindModels();
            }
        }
        public static void Instruction(Transform world)
        {
            var game=world.GetComponent<CardboardFactoryGame>();
            var old=world.Find("Floor instruction");if(old)Object.DestroyImmediate(old.gameObject);
            var model=Bake(Root+"Instrucion/Instruction.fbx","InstructionSheet",-90,Vector3.zero,true,.85f);
            var material=Paper("InstructionPaper",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Instrucion/Instruction_Texture.png"));
            var holder=Group("Floor instruction",world,Vector3.zero);
            var instruction=holder.gameObject.AddComponent<FactoryInstruction>();instruction.game=game;game.instruction=instruction;
            instruction.sheet=MeshObject("Instruction • two authored sides",holder,model,material,Vector3.zero);
            instruction.pickup=instruction.sheet.gameObject.AddComponent<BoxCollider>();
            instruction.pickup.center=model.bounds.center;instruction.pickup.size=new Vector3(model.bounds.size.x,model.bounds.size.y,Mathf.Max(.045f,model.bounds.size.z));
            FactoryGeometry.Interaction(instruction.sheet,FactoryAction.Instruction,"E — поднять инструкцию");
            instruction.floorPosition=game.station.transform.TransformPoint(new Vector3(-.95f,.04f,-2.8f));
            instruction.floorRotation=Quaternion.Euler(90,15,0);instruction.ResetFloor();
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update assets in Edit Mode.");
            var world=Object.FindFirstObjectByType<CardboardFactoryWorld>();
            if(!world)throw new InvalidOperationException("Factory scene is missing.");
            EditorSceneManager.SaveScene(world.gameObject.scene);
            Directory.CreateDirectory("Backups/SettingsInstructionUpdate");
            string backup="Backups/SettingsInstructionUpdate/FactoryShift-before-update.unity";
            if(!File.Exists(backup))File.Copy(world.gameObject.scene.path,backup);
            Presses(world.transform);Instruction(world.transform);FactoryMenuBuilder.Create(world.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            File.WriteAllText("Library/FactoryNewAssetsUpdate.txt","SUCCESS: authored UVs, imported settings models, two-sided instruction and all four textured presses updated.");
        }
    }
}
