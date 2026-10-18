using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public class FactoryMistakeHUD : MonoBehaviour
    {
        public Image[] stars;
        public Text reasons;
        int displayed=-1;
        public static FactoryMistakeHUD Create(Transform parent,Camera camera)
        {
            var root=new GameObject("Shift mistakes • three cardboard stars",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
            canvas.planeDistance=.055f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=1;
            var group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var hud=root.AddComponent<FactoryMistakeHUD>();hud.stars=new Image[CardboardFactoryGame.MistakeLimit];
            var sprite=CardboardStar();
            for(int i=0;i<hud.stars.Length;i++)
            {
                var star=new GameObject("Mistake star "+(i+1),typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Shadow));
                var rect=star.GetComponent<RectTransform>();rect.SetParent(root.transform,false);
                rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,.5f);
                rect.sizeDelta=new Vector2(72,72);rect.anchoredPosition=new Vector2(-66-(2-i)*86,-66);
                var image=star.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
                star.GetComponent<Shadow>().effectColor=new Color(0,0,0,.65f);star.GetComponent<Shadow>().effectDistance=new Vector2(2,-3);
                hud.stars[i]=image;
            }
            var label=new GameObject("Debug • reasons for mistakes",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text),typeof(Shadow));
            var labelRect=label.GetComponent<RectTransform>();labelRect.SetParent(root.transform,false);
            labelRect.anchorMin=labelRect.anchorMax=labelRect.pivot=new Vector2(1,1);
            labelRect.sizeDelta=new Vector2(345,330);labelRect.anchoredPosition=new Vector2(-30,-120);
            hud.reasons=label.GetComponent<Text>();hud.reasons.font=CardboardTypography.DefaultFont;hud.reasons.fontSize=20;
            hud.reasons.color=new Color(1,.94f,.80f);hud.reasons.alignment=TextAnchor.UpperRight;hud.reasons.lineSpacing=1.7f;
            hud.reasons.supportRichText=false;hud.reasons.raycastTarget=false;
            hud.reasons.horizontalOverflow=HorizontalWrapMode.Wrap;hud.reasons.verticalOverflow=VerticalWrapMode.Overflow;
            label.GetComponent<Shadow>().effectColor=new Color(.08f,.06f,.035f,.95f);label.GetComponent<Shadow>().effectDistance=new Vector2(1.5f,-2);
            hud.Refresh();return hud;
        }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            var game=CardboardFactoryGame.Instance;int count=game?game.Mistakes:0;
            if(count==displayed || stars==null || !reasons) return;
            displayed=count;
            for(int i=0;i<stars.Length;i++) stars[i].color=i<count?Color.white:new Color(.43f,.40f,.34f,.65f);
            var text=new StringBuilder();
            if(game) for(int i=0;i<game.MistakeReasons.Count;i++)
            {if(i>0) text.Append("\n\n");text.Append(i+1).Append(". ").Append(game.MistakeReasons[i]);}
            reasons.text=text.ToString();
        }
        static Sprite CardboardStar()
        {
            const int size=192;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) {name="Cardboard mistake star • fibers",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var points=new Vector2[10];
            for(int i=0;i<10;i++)
            {
                float angle=(90-i*36)*Mathf.Deg2Rad;
                points[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i%2==0?1:.46f);
            }
            var pixels=new Color[size*size];var random=new System.Random(173);
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                Vector2 p=new Vector2((x-(size-1)*.5f)/87,(y-(size-1)*.5f)/87);
                bool inside=false;float edge=float.MaxValue;
                for(int i=0,j=9;i<10;j=i++)
                {
                    var a=points[i];var b=points[j];
                    if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
                    Vector2 ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
                    edge=Mathf.Min(edge,Vector2.Distance(p,a+ab*t));
                }
                if(!inside) {pixels[y*size+x]=Color.clear;continue;}
                float grain=.94f+(float)random.NextDouble()*.09f;
                if((x+y/4)%21<2) grain-=.08f;
                Color color=edge<.026f?new Color(.28f,.18f,.095f):
                    edge<.065f?new Color(.93f,.73f,.40f):new Color(.76f,.54f,.27f);
                pixels[y*size+x]=new Color(color.r*grain,color.g*grain,color.b*grain,1);
            }
            texture.SetPixels(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);sprite.name="Cardboard mistake star";
            CardboardArt.Generated.Add(texture);CardboardArt.Generated.Add(sprite);return sprite;
        }
    }
}
