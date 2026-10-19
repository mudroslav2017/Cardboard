using UnityEngine;

namespace Cardboard
{
    public static class ImportedCardboardCharacters
    {
        public static Transform Create(Transform parent, int variant)
        {
            string model = new[]{"HumanLera","HumanKolya","HumanNastya","HumanMasha"}[variant%4];
            var prefab = Resources.Load<GameObject>("CardboardCharacters/" + model);
            if (!prefab) throw new System.InvalidOperationException("Missing imported character prefab: " + model + ". Run Cardboard/Use imported people in the editor.");
            return Object.Instantiate(prefab, parent, false).transform;
        }
    }
}
