using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard.Editor
{
    public static class FactoryTableMaterials
    {
        public const string PrefabPath="Assets/Cardboard/Generated/WorkbenchTable.prefab";
        static FactoryWorkstation[] Stations() =>
            UnityEngine.Object.FindObjectsByType<FactoryWorkstation>(FindObjectsInactive.Include,FindObjectsSortMode.None);

        public static void Audit()
        {
            var report=new StringBuilder();
            report.AppendLine("Playing="+EditorApplication.isPlaying);
            foreach(var station in Stations())
            {
                report.AppendLine(station.name+" player="+station.isPlayerStation+" dirty="+station.gameObject.scene.isDirty);
                var table=station.transform.Find("Table");
                if(!table) continue;
                foreach(var renderer in table.GetComponentsInChildren<Renderer>(true))
                {
                    report.Append("  "+AnimationUtility.CalculateTransformPath(renderer.transform,table));
                    foreach(var material in renderer.sharedMaterials)
                        report.Append(" | "+(material?AssetDatabase.GetAssetPath(material)+" texture="+material.mainTexture+" color="+material.color+" cull="+(material.HasProperty("_Cull")?material.GetFloat("_Cull"):-1)+" smoothness="+(material.HasProperty("_Smoothness")?material.GetFloat("_Smoothness"):-1)+" reflections="+(material.HasProperty("_EnvironmentReflections")?material.GetFloat("_EnvironmentReflections"):-1)+" shader="+material.shader.name:"MISSING"));
                    report.AppendLine();
                }
            }
            File.WriteAllText("Library/TableMaterialsAudit.txt",report.ToString());
        }

        [MenuItem("Cardboard/Copy player table materials to all tables")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply table materials in Edit Mode.");
            var stations=Stations();
            var player=Array.Find(stations,station=>station.isPlayerStation);
            if(!player) throw new InvalidOperationException("Player workstation is missing.");
            var source=player.transform.Find("Table");
            if(!source) throw new InvalidOperationException("Player table is missing.");
            var originals=source.GetComponentsInChildren<Renderer>(true);
            // Validate every corresponding mesh before changing the open scene.
            foreach(var renderer in originals)
            {
                foreach(var material in renderer.sharedMaterials)
                    if(!material || !AssetDatabase.Contains(material))
                        throw new InvalidOperationException("Save the table material asset first: "+renderer.name);
                string path=AnimationUtility.CalculateTransformPath(renderer.transform,source);
                foreach(var station in stations)
                    if(!station.transform.Find("Table/"+path)?.GetComponent<Renderer>())
                        throw new InvalidOperationException("Matching table part is missing: "+station.name+"/"+path);
            }
            foreach(var renderer in originals)
            {
                foreach(var material in renderer.sharedMaterials)
                {
                    Undo.RecordObject(material,"Show both sides of table material");
                    if(material.HasProperty("_Cull")) material.SetFloat("_Cull",(float)CullMode.Off);
                    material.doubleSidedGI=true;
                    EditorUtility.SetDirty(material);
                }
                string path=AnimationUtility.CalculateTransformPath(renderer.transform,source);
                foreach(var station in stations)
                {
                    var target=station.transform.Find("Table/"+path).GetComponent<Renderer>();
                    Undo.RecordObject(target,"Copy player table materials");
                    target.sharedMaterials=renderer.sharedMaterials;
                    target.shadowCastingMode=ShadowCastingMode.TwoSided;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    EditorUtility.SetDirty(target);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(source.gameObject,PrefabPath,out bool saved);
            if(!saved) throw new InvalidOperationException("Could not save the table appearance prefab.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Audit();
            File.AppendAllText("Library/TableMaterialsAudit.txt","SUCCESS: copied "+originals.Length+" table parts to "+stations.Length+" workstations; all table materials and shadows are double-sided. Saved shared table prefab.\n");
            Preview();
        }

        [MenuItem("Cardboard/Fix blue sheen on table legs")]
        public static void FixLegColor()
        {
            var materials=new System.Collections.Generic.HashSet<Material>();
            foreach(var station in Stations())
            foreach(var renderer in station.transform.Find("Table").GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.name=="Cube") continue;
                foreach(var material in renderer.sharedMaterials) if(material) materials.Add(material);
            }
            foreach(var material in materials)
            {
                Undo.RecordObject(material,"Make cardboard table legs matte");
                material.SetColor("_BaseColor",Color.white);
                material.SetColor("_Color",Color.white);
                material.SetFloat("_Metallic",0);
                material.SetFloat("_Smoothness",0);
                material.SetFloat("_EnvironmentReflections",0);
                material.SetFloat("_SpecularHighlights",0);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Audit();
            File.AppendAllText("Library/TableMaterialsAudit.txt","SUCCESS: removed glossy blue reflections from "+materials.Count+" shared cardboard leg/brace materials.\n");
            Preview();
        }

        [MenuItem("Cardboard/Rotate each vertical table leg 180 degrees")]
        public static void RotateVerticalLegs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rotate table legs in Edit Mode.");
            var stations=Stations();
            var player=Array.Find(stations,station=>station.isPlayerStation);
            if(!player) throw new InvalidOperationException("Player workstation is missing.");
            var legs=new System.Collections.Generic.List<Renderer>();
            foreach(var station in stations)
            {
                var table=station.transform.Find("Table");
                if(!table) throw new InvalidOperationException("Table is missing: "+station.name);
                var parts=table.GetComponentsInChildren<Renderer>(true);
                for(int number=1;number<=4;number++)
                {
                    string name="Cube."+number.ToString("000");
                    var leg=Array.Find(parts,part=>part.name==name);
                    if(!leg || leg.bounds.size.y<Mathf.Max(leg.bounds.size.x,leg.bounds.size.z)*2)
                        throw new InvalidOperationException("Vertical table leg is missing: "+station.name+"/"+name);
                    legs.Add(leg);
                }
            }
            var report=new StringBuilder();
            foreach(var leg in legs)
            {
                var transform=leg.transform;
                Vector3 center=leg.bounds.center;
                Quaternion rotation=transform.rotation;
                Undo.RecordObject(transform,"Rotate table leg around its own center");
                // One FBX leg has an offset origin; use its geometric center as the pivot.
                transform.RotateAround(center,Vector3.up,180);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                EditorUtility.SetDirty(transform);
                float movement=Vector3.Distance(center,leg.bounds.center);
                float angle=Quaternion.Angle(rotation,transform.rotation);
                report.AppendLine(leg.name+" center displacement="+movement.ToString("F6")+" m; rotation="+angle.ToString("F3")+" degrees");
                if(movement>.0001f || Mathf.Abs(angle-180)>.01f)
                    throw new InvalidOperationException("Table leg moved away from its center: "+leg.name);
            }
            PrefabUtility.SaveAsPrefabAsset(player.transform.Find("Table").gameObject,PrefabPath,out bool saved);
            if(!saved) throw new InvalidOperationException("Could not save rotated table legs.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            report.AppendLine("SUCCESS: rotated "+legs.Count+" vertical legs on "+stations.Length+" tables by 180 degrees around their own vertical centers; saved scene and table prefab.");
            File.WriteAllText("Library/TableLegRotation.txt",report.ToString());
            Preview();
        }

        public static void Preview()
        {
            var stations=Stations();
            var player=Array.Find(stations,station=>station.isPlayerStation);
            var camera=Camera.main;
            Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            var people=new System.Collections.Generic.Dictionary<Renderer,bool>();
            foreach(var worker in UnityEngine.Object.FindObjectsByType<CardboardRunner>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var renderer in worker.GetComponentsInChildren<Renderer>(true))
                {people[renderer]=renderer.enabled;renderer.enabled=false;}
            try
            {
                foreach(var station in new[]{player,Array.Find(stations,item=>!item.isPlayerStation)})
                {
                    if(!station) continue;
                    camera.transform.position=station.transform.TransformPoint(new Vector3(.1f,1.35f,-3.8f));
                    camera.transform.rotation=Quaternion.LookRotation(station.transform.TransformPoint(new Vector3(0,.65f,0))-camera.transform.position);
                    CardboardFactorySceneBuilder.Capture(station.isPlayerStation?"TableMaterialsPlayer":"TableMaterialsWorker");
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                foreach(var person in people) if(person.Key) person.Key.enabled=person.Value;
            }
        }
    }
}
