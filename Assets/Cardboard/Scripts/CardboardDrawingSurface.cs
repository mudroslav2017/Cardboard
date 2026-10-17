using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(MeshRenderer),typeof(MeshCollider))]
    public class CardboardDrawingSurface : MonoBehaviour
    {
        public CardboardRunner Runner => GetComponentInParent<CardboardRunner>();
        public Texture2D DrawingTexture => textures[0];
        public Texture2D BackDrawingTexture => textures[1];
        readonly Material[] instances=new Material[2];
        readonly Texture2D[] textures=new Texture2D[2];
        readonly Color32[][] pixels=new Color32[2][];
        Material[] originals;
        MeshRenderer display;

        public static int SideOf(RaycastHit hit)
        { return hit.collider.transform.InverseTransformDirection(hit.normal).z<0?1:0; }

        void EnsureTexture(int side)
        {
            if(textures[side]) return;
            if(!display) { display=GetComponent<MeshRenderer>();originals=display.sharedMaterials; }
            textures[side]=Instantiate((Texture2D)originals[side].mainTexture);
            textures[side].name=name+(side==0?" front":" back")+" • individual marker drawing";
            instances[side]=new Material(originals[side]) {name=name+" • marker ink "+side};
            instances[side].mainTexture=textures[side];instances[side].SetTexture("_BaseMap",textures[side]);
            var materials=display.sharedMaterials;materials[side]=instances[side];display.sharedMaterials=materials;
            pixels[side]=textures[side].GetPixels32();
        }

        // The two faces have independent ink; radius is measured on the physical sheet.
        public void PaintStroke(Vector2 from,Vector2 to,Color color,float radius,int side=0)
        {
            side=Mathf.Clamp(side,0,1);EnsureTexture(side);
            var texture=textures[side];var inkPixels=pixels[side];
            int width=texture.width,height=texture.height;
            Vector2 a=new Vector2(from.x*(width-1),from.y*(height-1));
            Vector2 b=new Vector2(to.x*(width-1),to.y*(height-1));
            float rx=Mathf.Max(1,radius/1.36f*width),ry=Mathf.Max(1,radius/2.34f*height);
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
                    if(coverage>0) inkPixels[y*width+x]=Color.Lerp(inkPixels[y*width+x],color,coverage*.72f);
                }
            }
            if(maxX<minX || maxY<minY) return;
            int patchWidth=maxX-minX+1,patchHeight=maxY-minY+1;
            var patch=new Color32[patchWidth*patchHeight];
            for(int y=0;y<patchHeight;y++) System.Array.Copy(inkPixels,(minY+y)*width+minX,patch,y*patchWidth,patchWidth);
            texture.SetPixels32(minX,minY,patchWidth,patchHeight,patch);texture.Apply(true,false);
        }
        public void ClearDrawing()
        {
            for(int side=0;side<2;side++)
            {
                if(!textures[side] || originals==null) continue;
                pixels[side]=((Texture2D)originals[side].mainTexture).GetPixels32();
                textures[side].SetPixels32(pixels[side]);textures[side].Apply(true,false);
            }
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
