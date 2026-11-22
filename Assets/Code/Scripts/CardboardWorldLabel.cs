using UnityEngine;

namespace Cardboard
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public class CardboardWorldLabel : MonoBehaviour
    {
        public Material ink;
        void OnEnable() { Font.textureRebuilt+=AtlasChanged;Refresh(); }
        void OnDisable() { Font.textureRebuilt-=AtlasChanged; }
        void AtlasChanged(Font font) { Refresh(); }
        public void Refresh()
        {
            var text=GetComponent<TextMesh>();
            if(!ink || !text.font) return;
            ink.mainTexture=text.font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial=ink;
        }
    }
}
