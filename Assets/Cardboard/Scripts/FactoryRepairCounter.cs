using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    [DefaultExecutionOrder(190)]
    public class FactoryRepairCounter : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public FactoryInventorySlot inventory;
        public Text label;
        public Transform icon;
        public bool Visible=>label && label.gameObject.activeSelf;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            bool visible=game && !game.MenuActive && !game.Ended && !game.CaptureActive && !game.IntroBusy;
            label.gameObject.SetActive(visible);icon.gameObject.SetActive(visible);
            label.text=game.Repairs+" / "+game.RepairGoal;
            if(!inventory || !inventory.overlay)return;
            var camera=inventory.overlay;
            float halfHeight=.65f*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float unit=halfHeight*2/1000;
            icon.localPosition=new Vector3(-halfHeight*inventory.lens.aspect+unit*223,halfHeight-unit*66,.65f);
            icon.localScale=Vector3.one*(unit*31);
            icon.localRotation=Quaternion.Euler(-8,16,Time.unscaledTime*22);
        }
    }
}
