using UnityEngine;
using System.Collections.Generic;

namespace Cardboard
{
    [RequireComponent(typeof(MeshRenderer),typeof(MeshCollider))]
    public class CardboardDrawingSurface : MonoBehaviour
    {
        public bool transparentInk;
        public Vector2 physicalSize=new Vector2(1.36f,2.34f);
        public CardboardRunner Runner => GetComponentInParent<CardboardRunner>();
        public Texture2D DrawingTexture => textures[0];
        public Texture2D BackDrawingTexture => textures[TextureSlot(1)];
        readonly Material[] instances=new Material[2];
        readonly Texture2D[] textures=new Texture2D[2];
        readonly Color32[][] pixels=new Color32[2][];
        readonly Color32[][] basePixels=new Color32[2][];
        Material[] originals;
        readonly Dictionary<int,Color>[] inkLayers={new Dictionary<int,Color>(),new Dictionary<int,Color>()};
        MeshRenderer display;

        int TextureSlot(int side)
        {
            if(!display) display=GetComponent<MeshRenderer>();
            // Imported people use one UV atlas for both faces and the corrugated edge.
            return display.sharedMaterials.Length==1?0:Mathf.Clamp(side,0,1);
        }

        public static int SideOf(RaycastHit hit)
        {
            var collider=hit.collider as MeshCollider;
            if(collider && collider.sharedMesh)
                return collider.transform.InverseTransformPoint(hit.point).z<collider.sharedMesh.bounds.center.z?1:0;
            return hit.collider.transform.InverseTransformDirection(hit.normal).z<0?1:0;
        }

        public static bool RaycastForDrawing(Ray ray,float distance,out RaycastHit hit)
        {
            // Cull Off makes inward-facing polygons visible. Include them in this
            // query too, so the nearest face supplies its own UVs instead of the far face.
            bool previous=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;
                return Physics.Raycast(ray,out hit,distance,~(1<<9),QueryTriggerInteraction.Ignore);
            }
            finally { Physics.queriesHitBackfaces=previous; }
        }

        void EnsureTexture(int side)
        {
            if(textures[side]) return;
            if(!display) display=GetComponent<MeshRenderer>();
            if(originals==null || originals.Length==0) originals=display.sharedMaterials;
            if(transparentInk)
            {
                int width=Mathf.Clamp(Mathf.CeilToInt(physicalSize.x*128),32,4096);
                int height=Mathf.Clamp(Mathf.CeilToInt(physicalSize.y*128),32,4096);
                textures[side]=new Texture2D(width,height,TextureFormat.RGBA32,true)
                    {wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                pixels[side]=new Color32[width*height];textures[side].SetPixels32(pixels[side]);
                display.enabled=true;
            }
            else
            {
                textures[side]=Instantiate((Texture2D)originals[side].mainTexture);
                basePixels[side]=textures[side].GetPixels32();pixels[side]=(Color32[])basePixels[side].Clone();
            }
            textures[side].name=name+(side==0?" front":" back")+" • individual marker drawing";
            instances[side]=new Material(originals[side]) {name=name+" • marker ink "+side};
            instances[side].mainTexture=textures[side];instances[side].SetTexture("_BaseMap",textures[side]);
            var materials=display.sharedMaterials;materials[side]=instances[side];display.sharedMaterials=materials;
        }

        // The two faces have independent ink; radius is measured on the physical sheet.
        public void PaintStroke(Vector2 from,Vector2 to,Color color,float radius,int side=0)
        {ApplyStroke(from,to,color,radius,side,false);}
        public void EraseStroke(Vector2 from,Vector2 to,float radius,int side=0)
        {ApplyStroke(from,to,Color.clear,radius,side,true);}
        void ApplyStroke(Vector2 from,Vector2 to,Color color,float radius,int side,bool erase)
        {
            side=TextureSlot(side);
            if(erase && !textures[side])return;
            EnsureTexture(side);
            if(transparentInk && !erase)display.enabled=true;
            var texture=textures[side];var inkPixels=pixels[side];
            int width=texture.width,height=texture.height;
            Vector2 a=new Vector2(from.x*(width-1),from.y*(height-1));
            Vector2 b=new Vector2(to.x*(width-1),to.y*(height-1));
            float rx=Mathf.Max(1,radius/Mathf.Max(.01f,physicalSize.x)*width),ry=Mathf.Max(1,radius/Mathf.Max(.01f,physicalSize.y)*height);
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a,b)/(Mathf.Min(rx,ry)*.45f)),1,2048);
            int minX=width,minY=height,maxX=-1,maxY=-1;
            for(int s=0;s<=steps;s++)
            {
                Vector2 center=Vector2.Lerp(a,b,(float)s/steps);
                int x0=Mathf.Max(0,Mathf.FloorToInt(center.x-rx)),x1=Mathf.Min(width-1,Mathf.CeilToInt(center.x+rx));
                int y0=Mathf.Max(0,Mathf.FloorToInt(center.y-ry)),y1=Mathf.Min(height-1,Mathf.CeilToInt(center.y+ry));
                minX=Mathf.Min(minX,x0);maxX=Mathf.Max(maxX,x1);minY=Mathf.Min(minY,y0);maxY=Mathf.Max(maxY,y1);
                for(int y=y0;y<=y1;y++)
                for(int x=x0;x<=x1;x++)
                {
                    float dx=(x-center.x)/rx,dy=(y-center.y)/ry;
                    float coverage=Mathf.Clamp01((1-Mathf.Sqrt(dx*dx+dy*dy))*Mathf.Min(rx,ry));
                    if(coverage>0)
                    {
                        if(erase)
                        {
                            int eraseIndex=y*width+x;
                            if(!inkLayers[side].TryGetValue(eraseIndex,out var erasedInk))continue;
                            erasedInk.a*=1-coverage;
                            if(erasedInk.a<.003f){inkLayers[side].Remove(eraseIndex);erasedInk=Color.clear;}else inkLayers[side][eraseIndex]=erasedInk;
                            inkPixels[eraseIndex]=transparentInk?erasedInk:Color.Lerp(basePixels[side][eraseIndex],new Color(erasedInk.r,erasedInk.g,erasedInk.b,1),erasedInk.a);
                            continue;
                        }
                        int index=y*width+x;float alpha=coverage*.72f;
                        inkPixels[index]=Color.Lerp(inkPixels[index],color,alpha);
                        inkLayers[side].TryGetValue(index,out var old);float combined=alpha+old.a*(1-alpha);
                        var ink=(color*alpha+old*(old.a*(1-alpha)))/combined;ink.a=combined;inkLayers[side][index]=ink;
                        if(transparentInk)inkPixels[index]=ink;
                    }
                }
            }
            if(maxX<minX || maxY<minY) return;
            int patchWidth=maxX-minX+1,patchHeight=maxY-minY+1;
            var patch=new Color32[patchWidth*patchHeight];
            for(int y=0;y<patchHeight;y++) System.Array.Copy(inkPixels,(minY+y)*width+minX,patch,y*patchWidth,patchWidth);
            texture.SetPixels32(minX,minY,patchWidth,patchHeight,patch);texture.Apply(true,false);
            if(transparentInk && erase && inkLayers[side].Count==0)display.enabled=false;
        }
        public void SetBaseMaterial(Material material)
        {
            if(!display)display=GetComponent<MeshRenderer>();
            var materials=display.sharedMaterials;
            if(originals!=null && originals.Length>0 && originals[0]==material)return;
            originals=new Material[materials.Length];
            for(int side=0;side<materials.Length;side++)
            {
                originals[side]=material;
                if(side<textures.Length && textures[side])
                {
                    var source=(Texture2D)material.mainTexture;basePixels[side]=source.GetPixels32();pixels[side]=(Color32[])basePixels[side].Clone();
                    foreach(var ink in inkLayers[side])if(ink.Key<pixels[side].Length)pixels[side][ink.Key]=Color.Lerp(pixels[side][ink.Key],ink.Value,ink.Value.a);
                    textures[side].SetPixels32(pixels[side]);textures[side].Apply(true,false);
                    instances[side].CopyPropertiesFromMaterial(material);instances[side].mainTexture=textures[side];instances[side].SetTexture("_BaseMap",textures[side]);
                    materials[side]=instances[side];
                }
                else materials[side]=material;
            }
            display.sharedMaterials=materials;
        }
        public void ClearDrawing()
        {
            for(int side=0;side<2;side++)
            {
                inkLayers[side].Clear();
                if(!textures[side] || originals==null) continue;
                pixels[side]=transparentInk?new Color32[textures[side].width*textures[side].height]:(Color32[])basePixels[side].Clone();
                textures[side].SetPixels32(pixels[side]);textures[side].Apply(true,false);
            }
            if(transparentInk && display)display.enabled=false;
        }
        void OnDestroy()
        {
            if(display && originals!=null) display.sharedMaterials=originals;
            for(int side=0;side<2;side++)
            {
                if(instances[side]) Destroy(instances[side]);
                if(textures[side]) Destroy(textures[side]);
            }
        }
    }
}
