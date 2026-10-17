using UnityEngine;

namespace Cardboard
{
    public static class ImportedFactoryGears
    {
        public static bool Create(Transform parent,int type)
        {
            var prefab=Resources.Load<GameObject>("FactoryGears/Gear"+Mathf.Clamp(type,1,3));
            if(!prefab) return false;
            Object.Instantiate(prefab,parent,false);return true;
        }
    }
}
