using UnityEngine;

namespace Cardboard
{
    public class CardboardHUD : MonoBehaviour
    {
        GUIStyle text, small, title;
        Font font;
        void Init()
        {
            font=CardboardTypography.DefaultFont;
            text=Style(14,CardboardArt.Hex("F4EBD7"));
            small=Style(11,CardboardArt.Hex("C4D3BF"));
            title=Style(19,CardboardArt.Hex("F4EBD7"));title.fontStyle=FontStyle.Bold;
        }
        GUIStyle Style(int size,Color color)
        {
            var style=new GUIStyle {font=font,fontSize=size};style.normal.textColor=color;return style;
        }
        void Fill(float x,float y,float w,float h,Color color)
        {
            GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
        }
        void OnGUI()
        {
            if(text==null || !font) Init();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            if(scale<=0) return;
            Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float w=Screen.width/scale,h=Screen.height/scale;
            Color panel=new Color(.10f,.20f,.17f,.84f);
            Fill(20,20,305,65,panel);Fill(20,20,3,65,CardboardArt.Hex("D5AD66"));
            GUI.Label(new Rect(36,31,280,28),"Cardboard Commotion",title);
            GUI.Label(new Rect(36,61,280,18),"FIRST PERSON / CARDBOARD WORLD",small);
            Fill(w-178,20,158,40,panel);
            GUI.Label(new Rect(w-161,32,140,22),"12 cardboard coworkers",text);
            Color cross=CardboardArt.Hex("FFF4DA");
            bool drawing=CardboardMarker.DrawingMode;
            if(!drawing)
            {
                Fill(w/2-4,h/2-1,8,2,new Color(0,0,0,.4f));Fill(w/2-1,h/2-4,2,8,new Color(0,0,0,.4f));
                Fill(w/2-3,h/2,6,1,cross);Fill(w/2,h/2-3,1,6,cross);
            }
            Fill(20,h-84,w-40,64,panel);
            GUI.Label(new Rect(36,h-73,w-70,24),"WASD  move     SHIFT  run     SPACE  jump     E  call     R  commotion     MOUSE  look     ESC  cursor",text);
            GUI.Label(new Rect(36,h-47,w-70,24),drawing?"LMB  touch and draw     1-5  color     WHEEL  brush size     C  clear NPC     F  put marker away":"F  take out the marker and draw on coworkers",text);
            if(drawing)
            {
                var marker=CardboardMarker.Instance;
                Fill(w-292,78,272,122,panel);
                GUI.Label(new Rect(w-276,90,245,24),"MARKER",title);
                for(int i=0;i<marker.Colors.Length;i++)
                {
                    float x=w-276+i*47;
                    if(i==marker.colorIndex) Fill(x-3,119,38,31,cross);
                    Fill(x,122,32,25,marker.Colors[i]);
                    GUI.Label(new Rect(x+11,151,24,20),(i+1).ToString(),small);
                }
                GUI.Label(new Rect(w-276,176,245,20),marker.Target?"LMB - TOUCH THE CARDBOARD WITH THE TIP":"Move closer and aim at a coworker",small);
            }
            bool party=Time.time<CardboardRunner.PartyUntil,follow=Time.time<CardboardRunner.FollowUntil;
            if(party || follow)
            {
                Fill(20,h-128,405,32,panel);
                GUI.Label(new Rect(34,h-121,380,22),party?"The cardboard crowd is getting restless.":"The cardboard crowd is following you.",text);
            }
            if(Cursor.lockState!=CursorLockMode.Locked && !drawing)
            {
                Fill(w/2-180,h/2+28,360,36,panel);
                var centered=new GUIStyle(text) {alignment=TextAnchor.MiddleCenter};
                GUI.Label(new Rect(w/2-180,h/2+28,360,36),"Click the game to look around",centered);
            }
            GUI.matrix=previous;
        }

    }
}
