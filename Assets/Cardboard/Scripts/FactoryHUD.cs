using UnityEngine;

namespace Cardboard
{
    public class FactoryHUD : MonoBehaviour
    {
        Font font;
        GUIStyle title,large,text,small,button,center,wrapped;
        readonly Color paper=CardboardArt.Hex("F1E6CE"),muted=CardboardArt.Hex("C1C8AE"),dark=CardboardArt.Hex("203B35"),gold=CardboardArt.Hex("DBAF5B"),red=CardboardArt.Hex("D66F53");
        GUIStyle Style(int size,Color color,FontStyle weight=FontStyle.Normal)
        { var s=new GUIStyle {font=font,fontSize=size,fontStyle=weight};s.normal.textColor=color;return s; }
        void Init()
        {
            font=CardboardTypography.DefaultFont;
            title=Style(22,paper,FontStyle.Bold);large=Style(34,paper,FontStyle.Bold);text=Style(14,paper);small=Style(11,muted);
            wrapped=new GUIStyle(text) {wordWrap=true};center=new GUIStyle(text) {alignment=TextAnchor.MiddleCenter};
            button=Style(14,paper,FontStyle.Bold);button.alignment=TextAnchor.MiddleCenter;
            button.normal.background=Texture2D.whiteTexture;button.hover.background=Texture2D.whiteTexture;button.active.background=Texture2D.whiteTexture;
        }
        void Fill(float x,float y,float w,float h,Color color)
        { GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white; }
        bool Button(Rect rect,string label,bool active=false)
        {
            GUI.backgroundColor=active?new Color(.63f,.39f,.17f):new Color(.16f,.27f,.23f);
            bool hit=GUI.Button(rect,label,button);GUI.backgroundColor=Color.white;return hit;
        }
        void Meter(float x,float y,float width,float amount,Color color)
        { Fill(x,y,width,5,new Color(0,0,0,.35f));Fill(x,y,width*Mathf.Clamp01(amount),5,color); }
        void OnGUI()
        {
            var game=CardboardFactoryGame.Instance;if(!game) return;
            if(!font || text==null) Init();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(scale<=0) return;
            var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float w=Screen.width/scale,h=Screen.height/scale;
            // The machine has its own labels, gauge and workpiece. No recipe overlay or station menu.
            if(game.workbench && game.workbench.Nearby && !game.Ended && game.State!=FactoryState.Chased)
            {
                if(!game.workbench.Manipulating && !CardboardMarker.DrawingMode)
                { Fill(w/2-2,h/2,4,1,paper);Fill(w/2,h/2-2,1,4,paper); }
                GUI.matrix=previous;return;
            }
            Color panel=new Color(.07f,.14f,.12f,.94f);
            Fill(20,20,372,95,panel);Fill(20,20,3,95,gold);
            GUI.Label(new Rect(35,29,350,28),"НЕ СВОЙ  /  СМЕНА 017",title);
            int seconds=Mathf.CeilToInt(game.ShiftRemaining);
            GUI.Label(new Rect(36,64,345,24),"Ремонты  "+game.Repairs+" / "+game.gears.Length+"     Механизм  "+game.WorkingGears+" / "+game.gears.Length,text);
            GUI.Label(new Rect(36,91,340,18),"ДО КОНЦА СМЕНЫ  "+seconds/60+":"+(seconds%60).ToString("00"),small);
            Fill(w-308,20,288,110,panel);
            string suspicion=game.State==FactoryState.Chased?"ЧУЖОЙ В ЦЕХУ!":game.Suspicion>=65?"ОНИ ПОДОЗРЕВАЮТ":game.Suspicion>=30?"НА ВАС СМОТРЯТ":"ВАМ ПОКА ВЕРЯТ";
            GUI.Label(new Rect(w-292,32,260,20),suspicion,small);
            Meter(w-292,59,254,game.Suspicion/100,game.Suspicion>=65?red:gold);
            GUI.Label(new Rect(w-292,76,260,21),"Маскировка рабочего  "+Mathf.RoundToInt(game.Disguise)+"%",text);
            Meter(w-292,105,254,game.Disguise/100,muted);
            if(game.Ended) Ending(game,w,h,panel);
            else
            {
                bool drawing=CardboardMarker.DrawingMode;
                if(!drawing)
                {
                    Fill(w/2-4,h/2,8,1,paper);Fill(w/2,h/2-4,1,8,paper);
                    if(!string.IsNullOrEmpty(game.FocusPrompt))
                    {
                        Fill(w/2-250,h/2+28,500,37,panel);
                        GUI.Label(new Rect(w/2-244,h/2+28,488,37),game.FocusPrompt,center);
                    }
                }
                if(game.Memorized)
                {
                    Fill(20,132,310,91,panel);Fill(20,132,3,91,gold);
                    GUI.Label(new Rect(35,144,280,20),"ЗАПИСАННЫЙ НАРЯД "+(char)('A'+game.Memorized.index),small);
                    GUI.Label(new Rect(35,169,280,48),game.Memorized.recipe.teeth+" зубьев / "+FactoryRecipe.HubName(game.Memorized.recipe.hub)+"\nСлоёв картона: "+game.Memorized.recipe.layers,wrapped);
                }
                if(game.HasPart)
                {
                    Fill(20,h-177,350,65,panel);
                    GUI.Label(new Rect(35,h-165,325,18),"В РУКЕ: ГОТОВАЯ ДЕТАЛЬ",small);
                    GUI.Label(new Rect(35,h-141,325,24),game.HeldRecipe.ToString(),text);
                }
                if(drawing && CardboardMarker.Instance)
                {
                    var marker=CardboardMarker.Instance;
                    Fill(w-278,h-184,258,78,panel);
                    GUI.Label(new Rect(w-262,h-174,230,18),"ФЛОМАСТЕР / 1–5 ЦВЕТ / C ОЧИСТИТЬ",small);
                    for(int i=0;i<marker.Colors.Length;i++)
                    {
                        float x=w-262+i*46;if(i==marker.colorIndex) Fill(x-2,h-145,34,27,paper);
                        Fill(x,h-143,30,23,marker.Colors[i]);
                    }
                }
                if(Time.time<game.MessageUntil)
                {
                    Fill(w/2-360,h-165,720,52,panel);
                    GUI.Label(new Rect(w/2-344,h-154,688,42),game.Message,wrapped);
                }
                Fill(20,h-94,w-40,74,panel);
                GUI.Label(new Rect(35,h-80,w-70,23),"WASD  ходить     SHIFT  бежать     E  взаимодействовать     C  притвориться картонкой     ПКМ  положить предмет     ЛКМ  удержать и бросить",text);
                GUI.Label(new Rect(35,h-52,w-70,23),drawing?"ЛКМ  рисовать вблизи     Мышь  двигать рукой     Колесо  толщина     F  убрать фломастер":"F  фломастер     Мышь  обзор     ESC  освободить курсор     Маски восстанавливаются в шкафчике у входа",small);
                if(Cursor.lockState!=CursorLockMode.Locked && !drawing)
                    GUI.Label(new Rect(w/2-220,h/2+78,440,32),"Нажмите в игровое окно, чтобы осмотреться",center);
            }
            GUI.matrix=previous;
        }
        void Ending(CardboardFactoryGame game,float w,float h,Color panel)
        {
            if(game.State==FactoryState.Captured)
            {
                Fill(w/2-270,145,540,60,panel);
                GUI.Label(new Rect(w/2-250,160,500,35),"ВАС РАСКРЫЛИ. «ЭТО НЕ КАРТОНКА».",title);return;
            }
            Fill(0,0,w,h,new Color(0,0,0,.52f));float x=w/2-330,y=h/2-170;Fill(x,y,660,340,panel);Fill(x,y,660,4,game.State==FactoryState.Escaped?gold:red);
            GUI.Label(new Rect(x+30,y+28,600,45),game.State==FactoryState.Escaped?"СМЕНА ЗАКОНЧЕНА":"ВЫ ОКАЗАЛИСЬ СЛИШКОМ ЖИВЫМ",title);
            string ending=game.State==FactoryState.Escaped?"Механизм работает. Наряд выполнен.\nНикто не догадался, что под картоном прячется человек.":game.Death==FactoryDeath.Press?"СМЕРТЬ: ПРЕСС\nРабочие положили чужака под заводской пресс.":game.Death==FactoryDeath.Pins?"СМЕРТЬ: БУЛАВКИ\nРабочие прикололи вас к доске длинными булавками.":"СМЕРТЬ: КАНЦЕЛЯРСКИЕ КНОПКИ\nКартонные рабочие заткнули чужака кнопками.";
            GUI.Label(new Rect(x+30,y+94,600,95),ending,wrapped);
            GUI.Label(new Rect(x+30,y+205,600,25),"Ремонтов: "+game.Repairs+"   Ошибок: "+game.Mistakes+"   Подозрение: "+Mathf.RoundToInt(game.Suspicion)+"%",text);
            if(Button(new Rect(x+30,y+263,600,48),"НОВАЯ СМЕНА / ENTER",true)) { game.Restart();GUIUtility.ExitGUI(); }
        }

    }
}
