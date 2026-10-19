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
            bool visible=game && !game.MenuActive && !game.CaptureActive && !game.Ended &&
                (lunch.State==LunchState.Ready || lunch.Carrying || Time.time<lunch.ResultUntil);
            panel.SetActive(visible);if(!visible) return;
            aim.SetActive(!CardboardMarker.DrawingMode && (lunch.Carrying || lunch.State==LunchState.Ready));
            balance.SetActive(lunch.Carrying);
            title.text=lunch.Carrying?"KEEP YOUR LUNCH ON THE TRAY":lunch.State==LunchState.Served?"LUNCH DELIVERED":
                lunch.State==LunchState.Spilled?"LUNCH SPILLED: +1 MARK":"LUNCH TIME";
            var focus=game.Focus?game.Focus.GetComponent<FactoryLunchControl>():null;
            hint.text=lunch.State==LunchState.Served?"Tray delivered, lunch intact.\nYou can return to work.":
                lunch.State==LunchState.Spilled?"The workers noticed the spilled food.\nContinue your shift carefully.":focus?focus.Prompt:lunch.Carrying?
                "WASD - walk to the table. LMB / RMB - balance.\nTurn smoothly to keep your lunch on the tray.":
                "Collect your tray from the canteen counter.\nThe entrance is in the workshop south wall.";
            needle.anchoredPosition=new Vector2(Mathf.Clamp(lunch.Tilt,-1,1)*190,0);
        }
    }
}
