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
            title.text=lunch.Carrying?"НЕ УРОНИТЕ ОБЕД":lunch.State==LunchState.Served?"ОБЕД ДОСТАВЛЕН":
                lunch.State==LunchState.Spilled?"ОБЕД НА ПОЛУ: +1 ЗВЕЗДА":"ВРЕМЯ ОБЕДА";
            var focus=game.Focus?game.Focus.GetComponent<FactoryLunchControl>():null;
            hint.text=lunch.State==LunchState.Served?"Поднос на столе, еда на месте.\nМожно возвращаться к работе.":
                lunch.State==LunchState.Spilled?"Картонки заметили упавшую еду.\nПродолжайте смену и будьте осторожнее.":focus?focus.Prompt:lunch.Carrying?
                "ЛКМ — влево    ПКМ — вправо\nWASD — к столу. Держите метку в центре.":
                "Возьмите поднос на раздаче в столовой.\nВход — в южной стене цеха.";
            needle.anchoredPosition=new Vector2(Mathf.Clamp(lunch.Tilt,-1,1)*190,0);
        }
    }
}
