using UnityEngine;

namespace Cardboard
{
    public class FactoryPunchcardView : MonoBehaviour
    {
        public Transform[] icons;
        public int GearType {get;private set;}
        public void ShowType(int type)
        {
            GearType=Mathf.Clamp(type,1,3);
            if(icons==null)return;
            for(int i=0;i<icons.Length;i++)if(icons[i])icons[i].gameObject.SetActive(i==GearType-1);
        }
    }
}
