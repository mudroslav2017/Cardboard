using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public class FactoryDeathGrade : MonoBehaviour
    {
        public const float PinImpactAt=.82f, HoldAfterPin=2f;
        public const float RestartAt=PinImpactAt+HoldAfterPin;
        public RectTransform word,pin;
        public Text lettering;
        public CanvasGroup wordOpacity,pinOpacity;
        public FactoryDeathPresentation presentation;
        public static FactoryDeathGrade Create(Transform canvas)
        {
            var root=new GameObject("Death • black screen and pinned grade",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect=root.GetComponent<RectTransform>();rect.SetParent(canvas,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            root.GetComponent<Image>().color=Color.black;root.GetComponent<Image>().raycastTarget=false;
            var grade=root.AddComponent<FactoryDeathGrade>();
            var word=new GameObject("ЛОХ • red paper grade",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text),typeof(CanvasGroup),typeof(Shadow));
            grade.word=word.GetComponent<RectTransform>();grade.word.SetParent(rect,false);grade.word.sizeDelta=new Vector2(720,320);
            grade.lettering=word.GetComponent<Text>();grade.lettering.text="ЛОХ";grade.lettering.font=CardboardTypography.DefaultFont;
            grade.lettering.fontSize=150;grade.lettering.color=new Color(.86f,.025f,.015f);grade.lettering.alignment=TextAnchor.MiddleCenter;
            grade.lettering.raycastTarget=false;grade.lettering.horizontalOverflow=HorizontalWrapMode.Overflow;grade.lettering.verticalOverflow=VerticalWrapMode.Overflow;
            word.GetComponent<Shadow>().effectColor=new Color(.24f,0,0);word.GetComponent<Shadow>().effectDistance=new Vector2(4,-5);
            grade.wordOpacity=word.GetComponent<CanvasGroup>();
            var pin=new GameObject("Cardboard grading pin",typeof(RectTransform),typeof(CanvasRenderer),typeof(FactoryCardboardPin),typeof(CanvasGroup),typeof(Shadow));
            grade.pin=pin.GetComponent<RectTransform>();grade.pin.SetParent(rect,false);grade.pin.sizeDelta=new Vector2(130,190);
            pin.GetComponent<FactoryCardboardPin>().raycastTarget=false;
            pin.GetComponent<Shadow>().effectColor=new Color(.12f,.07f,.025f);pin.GetComponent<Shadow>().effectDistance=new Vector2(3,-4);
            grade.pinOpacity=pin.GetComponent<CanvasGroup>();grade.Hide();return grade;
        }
        public void Hide() { gameObject.SetActive(false); }
        public void Show(float time)
        {
            gameObject.SetActive(true);
            if(presentation)presentation.Show(time);
            float flight=Mathf.Clamp01(time/.40f);
            float settle=Mathf.Max(0,time-.4f);
            word.localScale=Vector3.one*Mathf.Lerp(4.5f,1,Mathf.SmoothStep(0,1,flight));
            wordOpacity.alpha=Mathf.Clamp01(time/.22f);
            float shake=Mathf.Sin(settle*59)*Mathf.Exp(-settle*13)*6;
            if(time>=PinImpactAt)shake+=Mathf.Sin((time-PinImpactAt)*72)*Mathf.Exp(-(time-PinImpactAt)*16)*8;
            word.anchoredPosition=new Vector2(shake,30+shake*.45f);
            word.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-17,-6,flight)+shake*.3f);
            float nail=Mathf.Clamp01((time-.60f)/(PinImpactAt-.60f));
            pin.gameObject.SetActive(time>=.60f);pinOpacity.alpha=nail;
            pin.localScale=Vector3.one*Mathf.Lerp(5,1,Mathf.SmoothStep(0,1,nail));
            pin.anchoredPosition=Vector2.Lerp(new Vector2(110,-160),new Vector2(2,120),Mathf.SmoothStep(0,1,nail));
            pin.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-40,-17,nail));
        }
    }
}
