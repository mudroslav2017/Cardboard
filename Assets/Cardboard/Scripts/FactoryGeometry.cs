using UnityEngine;
using static Cardboard.CardboardArt;

namespace Cardboard
{
    public static class FactoryGeometry
    {
        public static void WorldLabel(string content,Transform parent,Vector3 position,float size,float maxWidth=4.5f,bool lightInk=false)
        {
            var label=CardboardArt.Label(content,parent,position,size);
            var mesh=label.GetComponent<TextMesh>();float width=0,max=0;
            mesh.color=lightInk?CardboardArt.Cream.color:CardboardArt.Ink.color;
            foreach(char c in content)
            {
                if(c=='\n') { max=Mathf.Max(max,width);width=0;continue; }
                width+=mesh.font.GetCharacterInfo(c,out var info,mesh.fontSize)?info.advance:mesh.fontSize*.5f;
            }
            max=Mathf.Max(max,width)*mesh.characterSize*.1f;
            if(max>maxWidth) mesh.characterSize*=maxWidth/max;
        }
        public static Mesh GearMesh(int teeth,bool damaged=false)
        {
            var points=new Vector2[teeth*4];
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/points.Length;
                float r=i%4==1 || i%4==2?1.08f:.86f;
                if(damaged && i<5) r=.65f;
                points[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
            }
            var temp=Panel("Cut gear "+teeth+(damaged?" broken":""),null,Vector3.zero,points,.065f,Gold);
            var mesh=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp.gameObject);return mesh;
        }
        public static Mesh HubMesh(FactoryHub hub)
        {
            Vector2[] points;
            if(hub==FactoryHub.Square) points=Rect(.42f,.42f,.015f);
            else
            {
                int count=hub==FactoryHub.Hexagon?6:24;points=new Vector2[count];
                for(int i=0;i<count;i++) { float a=i*Mathf.PI*2/count;points[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.23f; }
            }
            var temp=Panel("Hub socket "+hub,null,Vector3.zero,points,.075f,Ink);
            var mesh=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp.gameObject);return mesh;
        }
        public static Transform MeshPart(string name,Transform parent,Vector3 pos,Mesh mesh,Material paper,Material edge)
        {
            var part=Group(name,parent,pos);part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[] {paper,edge};return part;
        }
        public static void GearVisual(Transform parent,FactoryRecipe recipe,Mesh[] gears,Mesh[] hubs,Material paper,Material edge,Material ink)
        {
            int teeth=Mathf.Clamp((recipe.teeth-8)/2,0,gears.Length-1);
            for(int i=0;i<recipe.layers;i++) MeshPart("Laminated gear layer "+i,parent,Vector3.back*i*.07f,gears[teeth],paper,edge);
            MeshPart("Printed hub socket",parent,new Vector3(0,0,.062f),hubs[(int)recipe.hub],ink,edge);
        }
        public static Transform Solid(string name,Transform parent,Vector3 pos,Vector3 size,Material mat)
        {
            var part=Box(name,parent,pos,size,mat);part.gameObject.layer=8;
            part.gameObject.AddComponent<BoxCollider>();return part;
        }
        public static FactoryInteractable Interaction(Transform part,FactoryAction action,string prompt,FactoryGear gear=null)
        {
            var item=part.gameObject.AddComponent<FactoryInteractable>();item.action=action;item.prompt=prompt;item.gear=gear;
            if(!part.GetComponent<Collider>()) part.gameObject.AddComponent<BoxCollider>();return item;
        }
        public static Transform Rod(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material mat)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=(a+b)*.5f;obj.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            obj.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2);
            obj.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(obj.GetComponent<Collider>());return obj.transform;
        }
    }
}
