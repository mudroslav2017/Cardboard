using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Cardboard.Editor
{
    public static class FactoryNewAssetsAudit
    {
        public static void Write()
        {
            var report=new StringBuilder();
            foreach(string path in new[]{
                "Assets/Game_Assets/Buttons/1/1.fbx","Assets/Game_Assets/Buttons/2/2.fbx","Assets/Game_Assets/Buttons/3/3.fbx",
                "Assets/Game_Assets/Buttons/Settings/Podloshka.fbx","Assets/Game_Assets/Buttons/Settings/Back_Button.fbx",
                "Assets/Game_Assets/Buttons/Volume_Bar/Volume_Bar.fbx","Assets/Game_Assets/Buttons/Volume_Bar/Volume_Gear.fbx",
                "Assets/Game_Assets/Cardboard/Instrucion/Instruction.fbx"})
            {
                report.AppendLine(path);
                var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                    report.AppendLine("MESH "+mesh.name+" vertices="+mesh.vertexCount+" submeshes="+mesh.subMeshCount);
                    var channels=new List<Vector2>[8];
                    for(int i=0;i<8;i++){channels[i]=new List<Vector2>();mesh.GetUVs(i,channels[i]);report.AppendLine("UV"+i+" count="+channels[i].Count);}
                    var areas=new Dictionary<int,float>();
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=filter.transform.TransformPoint(vertices[triangles[i]]);var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);
                        var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                        areas[i]=Vector3.Cross(b-a,c-a).magnitude;
                    }
                    foreach(var item in areas.OrderByDescending(pair=>pair.Value).Take(6))
                    {
                        int i=item.Key;
                        for(int j=0;j<3;j++)
                        {
                            int v=triangles[i+j];report.Append("POS "+filter.transform.TransformPoint(vertices[v]).ToString("F4"));
                            for(int channel=0;channel<8;channel++)if(channels[channel].Count>v)report.Append(" uv"+channel+"="+channels[channel][v].ToString("F4"));
                            report.AppendLine();
                        }
                    }
                }
                Object.DestroyImmediate(instance);
            }
            File.WriteAllText("Library/NewAssetsUVAudit.txt",report.ToString());
        }
    }
}
