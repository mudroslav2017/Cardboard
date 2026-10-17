using UnityEngine;
using System.Collections.Generic;

namespace Cardboard
{
    // A single extruded silhouette with all details printed onto its paper surface.
    public static class CardboardCutout
    {
        static readonly Dictionary<int,Mesh> meshes=new Dictionary<int,Mesh>();
        static readonly Dictionary<int,Material[]> materials=new Dictionary<int,Material[]>();
        public static void Reset() { meshes.Clear();materials.Clear(); }
        static readonly Vector2[] Outline = {
            new Vector2(-.40f,0),new Vector2(.40f,0),new Vector2(.36f,.12f),new Vector2(.25f,.16f),
            new Vector2(.25f,.70f),new Vector2(.40f,.75f),new Vector2(.31f,1.02f),new Vector2(.30f,1.24f),
            new Vector2(.41f,1.03f),new Vector2(.48f,.83f),new Vector2(.60f,.83f),new Vector2(.64f,.94f),
            new Vector2(.55f,1.22f),new Vector2(.40f,1.50f),new Vector2(.21f,1.59f),new Vector2(.29f,1.70f),
            new Vector2(.36f,1.90f),new Vector2(.32f,2.12f),new Vector2(.20f,2.25f),new Vector2(0,2.31f),
            new Vector2(-.23f,2.25f),new Vector2(-.34f,2.10f),new Vector2(-.36f,1.86f),new Vector2(-.29f,1.69f),
            new Vector2(-.20f,1.59f),new Vector2(-.40f,1.50f),new Vector2(-.55f,1.22f),new Vector2(-.64f,.94f),
            new Vector2(-.60f,.83f),new Vector2(-.48f,.83f),new Vector2(-.41f,1.03f),new Vector2(-.30f,1.24f),
            new Vector2(-.31f,1.02f),new Vector2(-.40f,.75f),new Vector2(-.25f,.70f),new Vector2(-.25f,.16f),new Vector2(-.36f,.12f)
        };

        public static Transform Create(Transform parent,int variant)
        {
            if(meshes.TryGetValue(variant,out Mesh existing) && existing)
            {
                var copy=CardboardArt.Group("One solid cardboard silhouette",parent,Vector3.zero);
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh=existing;
                copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials[variant];
                return copy;
            }
            const int width=384,height=768;
            var pixels=new Color[width*height];
            var random=new System.Random(417+variant);
            Color paper=CardboardArt.Hex(variant%3==0?"BB925F":variant%3==1?"C6A272":"AF8759");
            Color ink=CardboardArt.Hex("4C443A");
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float grain=.95f+(float)random.NextDouble()*.10f;
                if(x%67==0) grain-=.025f;
                pixels[y*width+x]=new Color(paper.r*grain,paper.g*grain,paper.b*grain,1);
            }
            var plain=(Color[])pixels.Clone();
            void Dot(Vector2 p,float radius)
            {
                float px=(p.x+.68f)/1.36f*(width-1),py=p.y/2.34f*(height-1);
                int rx=Mathf.CeilToInt(radius/1.36f*width),ry=Mathf.CeilToInt(radius/2.34f*height);
                for(int y=Mathf.Max(0,(int)py-ry);y<=Mathf.Min(height-1,(int)py+ry);y++)
                for(int x=Mathf.Max(0,(int)px-rx);x<=Mathf.Min(width-1,(int)px+rx);x++)
                {
                    float dx=(x-px)/width*1.36f,dy=(y-py)/height*2.34f;
                    float alpha=Mathf.Clamp01((radius-Mathf.Sqrt(dx*dx+dy*dy))*width/1.36f+ .5f);
                    pixels[y*width+x]=Color.Lerp(pixels[y*width+x],ink,alpha*.93f);
                }
            }
            void Line(Vector2 a,Vector2 b,float radius=.005f)
            {
                int steps=Mathf.CeilToInt(Vector2.Distance(a,b)*700);
                for(int i=0;i<=steps;i++)
                {
                    Vector2 p=Vector2.Lerp(a,b,steps==0?0:(float)i/steps);
                    p.x+=Mathf.Sin(i*.18f+variant)*.001f; Dot(p,radius);
                }
            }
            void Path(params Vector2[] points) { for(int i=1;i<points.Length;i++) Line(points[i-1],points[i]); }
            Vector2 P(float x,float y) => new Vector2(x,y);
            var inset=new Vector2[Outline.Length+1];
            for(int i=0;i<Outline.Length;i++) inset[i]=Vector2.Lerp(Outline[i],P(0,1.15f),.045f);
            inset[Outline.Length]=inset[0]; Path(inset);
            // Hair, neck and clothing are ink lines, not separate body parts.
            if(variant%3==0)
                Path(P(-.27f,1.75f),P(-.25f,2.04f),P(-.16f,2.14f),P(-.17f,2.03f),P(-.03f,2.15f),P(-.04f,2.04f),P(.10f,2.16f),P(.10f,2.04f),P(.23f,2.12f),P(.25f,1.78f));
            else
                Path(P(-.29f,1.68f),P(-.26f,2.04f),P(-.17f,2.10f),P(-.12f,2.04f),P(0,2.15f),P(.04f,2.04f),P(.20f,2.13f),P(.24f,1.68f));
            Path(P(-.22f,1.97f),P(-.20f,1.82f),P(-.12f,1.73f),P(-.06f,1.70f),P(-.06f,1.62f));
            Path(P(.22f,1.97f),P(.20f,1.82f),P(.12f,1.73f),P(.06f,1.70f),P(.06f,1.62f));
            Path(P(-.20f,1.57f),P(-.06f,1.62f),P(0,1.57f),P(.06f,1.62f),P(.20f,1.57f));
            Path(P(-.38f,1.47f),P(-.29f,1.29f),P(-.20f,1.34f));
            Path(P(.38f,1.47f),P(.29f,1.29f),P(.20f,1.34f));
            Path(P(-.25f,1.32f),P(-.24f,1.02f),P(.24f,1.02f),P(.25f,1.32f));
            Path(P(-.24f,1.02f),P(-.34f,.78f),P(0,.73f),P(.34f,.78f),P(.24f,1.02f));
            Path(P(0,.73f),P(0,.17f),P(-.10f,.11f),P(-.32f,.11f),P(-.30f,.04f),P(-.02f,.04f),P(0,.10f),P(.02f,.04f),P(.30f,.04f),P(.32f,.11f),P(.10f,.11f),P(0,.17f));
            if(variant%2==0) { Dot(P(-.09f,1.93f),.014f); Dot(P(.09f,1.93f),.014f); Path(P(-.07f,1.84f),P(0,1.81f),P(.07f,1.84f)); }
            if(variant%3==1) Path(P(-.10f,1.18f),P(-.10f,1.08f),P(.08f,1.08f),P(.08f,1.18f),P(-.10f,1.18f));
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,true) {name="Hand-drawn cardboard person "+variant,wrapMode=TextureWrapMode.Clamp,anisoLevel=4};
            texture.SetPixels(pixels);texture.Apply();
            var mat=new Material(CardboardArt.Kraft) {name="Printed single sheet "+variant};
            mat.color=Color.white;mat.SetColor("_BaseColor",Color.white);mat.mainTexture=texture;mat.SetTexture("_BaseMap",texture);
            CardboardArt.Generated.Add(texture);CardboardArt.Generated.Add(mat);
            // The rear is a separate printed surface, with hair and clothing seen from behind.
            pixels=plain;
            Path(inset);
            Path(P(-.29f,1.70f),P(-.28f,2.06f),P(-.18f,2.17f),P(0,2.22f),P(.18f,2.17f),P(.28f,2.06f),P(.29f,1.70f),P(-.29f,1.70f));
            Path(P(-.20f,1.57f),P(-.10f,1.53f),P(.10f,1.53f),P(.20f,1.57f));
            Path(P(-.38f,1.47f),P(-.29f,1.29f),P(-.20f,1.34f));
            Path(P(.38f,1.47f),P(.29f,1.29f),P(.20f,1.34f));
            Path(P(-.25f,1.32f),P(-.24f,1.02f),P(.24f,1.02f),P(.25f,1.32f));
            Path(P(-.24f,1.02f),P(-.34f,.78f),P(0,.73f),P(.34f,.78f),P(.24f,1.02f));
            Path(P(0,.73f),P(0,.12f));
            Path(P(-.32f,.10f),P(.32f,.10f));
            var rearTexture=new Texture2D(width,height,TextureFormat.RGBA32,true) {name="Cardboard person rear "+variant,wrapMode=TextureWrapMode.Clamp,anisoLevel=4};
            rearTexture.SetPixels(pixels);rearTexture.Apply();
            var rearMaterial=new Material(mat) {name="Printed rear sheet "+variant};
            rearMaterial.mainTexture=rearTexture;rearMaterial.SetTexture("_BaseMap",rearTexture);
            CardboardArt.Generated.Add(rearTexture);CardboardArt.Generated.Add(rearMaterial);
            var sheet=CardboardArt.Panel("One solid cardboard silhouette",parent,Vector3.zero,Outline,.045f,mat);
            var mesh=sheet.GetComponent<MeshFilter>().sharedMesh;
            int[] faces=mesh.GetTriangles(0),edges=mesh.GetTriangles(1);
            int count=faces.Length/2;
            var frontTriangles=new int[count];var rearTriangles=new int[count];
            System.Array.Copy(faces,0,frontTriangles,0,count);System.Array.Copy(faces,count,rearTriangles,0,count);
            mesh.subMeshCount=3;mesh.SetTriangles(frontTriangles,0);mesh.SetTriangles(rearTriangles,1);mesh.SetTriangles(edges,2);
            var sheetMaterials=new[] {mat,rearMaterial,CardboardArt.Edge};
            sheet.GetComponent<MeshRenderer>().sharedMaterials=sheetMaterials;
            Vector3[] vertices=mesh.vertices;Vector2[] uv=mesh.uv;
            for(int i=0;i<Outline.Length*2;i++) uv[i]=new Vector2((vertices[i].x+.68f)/1.36f,vertices[i].y/2.34f);
            mesh.uv=uv;
            meshes[variant]=mesh;materials[variant]=sheetMaterials;
            return sheet;
        }
    }
}
