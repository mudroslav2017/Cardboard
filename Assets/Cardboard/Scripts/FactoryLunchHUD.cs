using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public class FactoryLunchHUD : MonoBehaviour
    {
        public FactoryLunch lunch;
        public GameObject panel,balance,aim;
        public Text title,hint;
        public RectTransform needle;
        void LateUpdate()
        {
            var game=lunch?lunch.game:null;
            bool visible=game && !game.MenuActive && !game.CaptureActive && !game.Ended && !game.EndingBusy && !lunch.FinalLunch &&
                (!lunch.Missed || lunch.Carrying || Time.time<lunch.ResultUntil) &&
                (lunch.State==LunchState.Ready || lunch.Carrying || Time.time<lunch.ResultUntil);
            panel.SetActive(visible);if(!visible)return;
            var background=panel.GetComponent<Image>();if(background)background.enabled=false;
            title.gameObject.SetActive(true);hint.gameObject.SetActive(false);
            title.rectTransform.anchoredPosition=new Vector2(0,10);title.rectTransform.sizeDelta=new Vector2(1100,54);
            title.fontSize=28;title.alignment=TextAnchor.MiddleCenter;
            title.text=lunch.State==LunchState.Served?"ENJOY YOUR MEAL! GET BACK TO WORK.":
                lunch.State==LunchState.Spilled?"LUNCH SPILLED. BACK TO WORK.":
                lunch.State==LunchState.Ready && !lunch.Attended?"LUNCH TIME! REACH THE CANTEEN IN "+lunch.ArrivalCountdown:"CARRY THE FOOD TO THE TABLE.";
            aim.SetActive(!CardboardMarker.DrawingMode && (lunch.Carrying || lunch.State==LunchState.Ready));
            balance.SetActive(lunch.Carrying);balance.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,84);
            needle.anchoredPosition=new Vector2(Mathf.Clamp(lunch.Tilt,-1,1)*190,0);
        }
    }
}
