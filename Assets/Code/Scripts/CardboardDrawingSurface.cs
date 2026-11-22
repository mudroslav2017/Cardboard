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
        // Character atlases are a single texture; wall faces store ink in small, lazy canvases.
        public Texture2D DrawingTexture => textures[0];
        public Texture2D BackDrawingTexture => textures[TextureSlot(1)];
        public const int WallPixelsPerMetre=1024;
        const float WallTileMetres=1;
        const int WallTextureBorder=4;
        readonly Dictionary<Vector2Int,CardboardDrawingSurface> wallTiles=new Dictionary<Vector2Int,CardboardDrawingSurface>();
        bool wallTile;
        bool TiledWall => transparentInk && !wallTile;
        public int AllocatedCanvasCount => TiledWall?wallTiles.Count:(textures[0]?1:0)+(textures[1]?1:0);
        public bool HasVisibleDrawing
        {
            get
            {
                if(TiledWall){foreach(var tile in wallTiles.Values)if(tile.HasVisibleDrawing)return true;return false;}
                return display && display.enabled && (textures[0] || textures[1]);
            }
        }
        readonly Material[] instances=new Material[2];
        readonly Texture2D[] textures=new Texture2D[2];
        readonly Color32[][] pixels=new Color32[2][];
        readonly Color32[][] basePixels=new Color32[2][];
        Material[] originals;
        readonly Dictionary<int,Color>[] inkLayers={new Dictionary<int,Color>(),new Dictionary<int,Color>()};
        MeshRenderer display;

        Vector2Int WallTileAt(Vector2 metres)
        {
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(metres.x/WallTileMetres),0,Mathf.CeilToInt(physicalSize.x/WallTileMetres)-1),
                Mathf.Clamp(Mathf.FloorToInt(metres.y/WallTileMetres),0,Mathf.CeilToInt(physicalSize.y/WallTileMetres)-1));
        }
        public Color SampleDrawing(Vector2 uv,int side=0)
        {
            if(TiledWall)
            {
                Vector2 metres=Vector2.Scale(uv,physicalSize);var key=WallTileAt(metres);
                if(!wallTiles.TryGetValue(key,out var tile))return Color.clear;
                return tile.SampleDrawing(new Vector2((metres.x-key.x*WallTileMetres)/tile.physicalSize.x,(metres.y-key.y*WallTileMetres)/tile.physicalSize.y));
            }
            var texture=textures[TextureSlot(side)];if(!texture)return Color.clear;
            if(wallTile)uv=new Vector2((uv.x*(texture.width-2*WallTextureBorder)+WallTextureBorder)/texture.width,
                (uv.y*(texture.height-2*WallTextureBorder)+WallTextureBorder)/texture.height);
            return texture.GetPixelBilinear(uv.x,uv.y);
        }
        void ApplyWallStroke(Vector2 from,Vector2 to,Color color,float radius,bool erase)
        {
            Vector2 a=Vector2.Scale(from,physicalSize),b=Vector2.Scale(to,physicalSize);
            // Paint the same segment into adjoining canvases, including their filtering gutters.
            float padding=radius+(float)WallTextureBorder/WallPixelsPerMetre;
            var first=WallTileAt(Vector2.Min(a,b)-Vector2.one*padding);var last=WallTileAt(Vector2.Max(a,b)+Vector2.one*padding);
            for(int y=first.y;y<=last.y;y++)
            for(int x=first.x;x<=last.x;x++)
            {
                var key=new Vector2Int(x,y);var origin=new Vector2(x*WallTileMetres,y*WallTileMetres);
                if(!wallTiles.TryGetValue(key,out var tile))
                {
                    if(erase)continue;
                    if(!display)display=GetComponent<MeshRenderer>();
                    Vector2 size=Vector2.Min(Vector2.one*WallTileMetres,physicalSize-origin);
                    var node=new GameObject("Wall ink "+x+","+y,typeof(MeshFilter),typeof(MeshRenderer));
                    node.layer=gameObject.layer;node.transform.SetParent(transform,false);
                    node.transform.localPosition=new Vector3((origin.x+size.x*.5f)/physicalSize.x-.5f,(origin.y+size.y*.5f)/physicalSize.y-.5f,0);
                    node.transform.localScale=new Vector3(size.x/physicalSize.x,size.y/physicalSize.y,1);
                    node.GetComponent<MeshFilter>().sharedMesh=GetComponent<MeshFilter>().sharedMesh;
                    var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=display.sharedMaterial;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.enabled=false;
                    tile=node.AddComponent<CardboardDrawingSurface>();tile.transparentInk=true;tile.wallTile=true;tile.physicalSize=size;
                    // The original face remains the single, continuous drawing target.
                    node.GetComponent<MeshCollider>().enabled=false;wallTiles.Add(key,tile);
                }
                var start=new Vector2((a.x-origin.x)/tile.physicalSize.x,(a.y-origin.y)/tile.physicalSize.y);
                var end=new Vector2((b.x-origin.x)/tile.physicalSize.x,(b.y-origin.y)/tile.physicalSize.y);
                tile.ApplyStroke(start,end,color,radius,0,erase);
            }
        }

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
                int width=Mathf.CeilToInt(physicalSize.x*WallPixelsPerMetre)+2*WallTextureBorder;
                int height=Mathf.CeilToInt(physicalSize.y*WallPixelsPerMetre)+2*WallTextureBorder;
                textures[side]=new Texture2D(width,height,TextureFormat.RGBA32,true)
                    {wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
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
            if(wallTile)
            {
                var texture=textures[side];
                instances[side].SetTextureScale("_BaseMap",new Vector2((float)(texture.width-2*WallTextureBorder)/texture.width,(float)(texture.height-2*WallTextureBorder)/texture.height));
                instances[side].SetTextureOffset("_BaseMap",new Vector2((float)WallTextureBorder/texture.width,(float)WallTextureBorder/texture.height));
            }
            var materials=display.sharedMaterials;materials[side]=instances[side];display.sharedMaterials=materials;
        }

        // The two faces have independent ink; radius is measured on the physical sheet.
        public void PaintStroke(Vector2 from,Vector2 to,Color color,float radius,int side=0)
        {ApplyStroke(from,to,color,radius,side,false);}
        public void EraseStroke(Vector2 from,Vector2 to,float radius,int side=0)
        {ApplyStroke(from,to,Color.clear,radius,side,true);}
        void ApplyStroke(Vector2 from,Vector2 to,Color color,float radius,int side,bool erase)
        {
            if(TiledWall){ApplyWallStroke(from,to,color,radius,erase);return;}
            side=TextureSlot(side);
            if(erase && !textures[side])return;
            EnsureTexture(side);
            if(transparentInk && !erase)display.enabled=true;
            var texture=textures[side];var inkPixels=pixels[side];
            int width=texture.width,height=texture.height;
            int spanX=wallTile?width-2*WallTextureBorder:width-1,spanY=wallTile?height-2*WallTextureBorder:height-1;
            float inset=wallTile?WallTextureBorder-.5f:0;
            Vector2 a=new Vector2(from.x*spanX+inset,from.y*spanY+inset);
            Vector2 b=new Vector2(to.x*spanX+inset,to.y*spanY+inset);
            float rx=Mathf.Max(1,radius/Mathf.Max(.01f,physicalSize.x)*(wallTile?spanX:width)),ry=Mathf.Max(1,radius/Mathf.Max(.01f,physicalSize.y)*(wallTile?spanY:height));
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
            if(TiledWall)
            {
                foreach(var tile in wallTiles.Values){tile.gameObject.SetActive(false);Destroy(tile.gameObject);}
                wallTiles.Clear();return;
            }
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
