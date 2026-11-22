using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public class FactoryCardboardPin : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color edge=new Color(.24f,.14f,.065f),paper=new Color(.83f,.65f,.39f),face=new Color(.67f,.45f,.22f);
            Polygon(mesh,new[]{new Vector2(-6,40),new Vector2(6,40),new Vector2(5,-54),new Vector2(0,-78),new Vector2(-5,-54)},edge);
            Polygon(mesh,new[]{new Vector2(-3,36),new Vector2(3,36),new Vector2(2,-52),new Vector2(0,-66),new Vector2(-2,-52)},paper);
            Disc(mesh,new Vector2(0,42),31,edge);Disc(mesh,new Vector2(0,44),26,paper);Disc(mesh,new Vector2(1,47),21,face);
            for(int i=-2;i<=2;i++)
                Polygon(mesh,new[]{new Vector2(-14+i*2,44+i*6),new Vector2(12+i*2,46+i*6),new Vector2(12+i*2,48+i*6),new Vector2(-14+i*2,46+i*6)},paper);
        }
        static void Disc(VertexHelper mesh,Vector2 center,float radius,Color color)
        {
            var p=new Vector2[18];
            for(int i=0;i<p.Length;i++){float a=i*Mathf.PI*2/p.Length;p[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}
            Polygon(mesh,p,color);
        }
        static void Polygon(VertexHelper mesh,Vector2[] points,Color color)
        {
            int start=mesh.currentVertCount;
            for(int i=0;i<points.Length;i++)mesh.AddVert(points[i],color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)mesh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
