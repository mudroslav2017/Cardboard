using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    public static class CardboardSurface
    {
        public const string ShaderName="Cardboard/DoubleSidedLit";
        public static Material Configure(Material material)
        {
            if(!material)return null;
            if(material.shader && material.shader.name=="Universal Render Pipeline/Lit")
            {
                var shader=Shader.Find(ShaderName);
                if(shader)material.shader=shader;
            }
            if(material.HasProperty("_Cull"))material.SetFloat("_Cull",(float)CullMode.Off);
            material.doubleSidedGI=true;
            if(material.HasProperty("_ReceiveShadows"))material.SetFloat("_ReceiveShadows",1);
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            foreach(string property in new[]{"_Smoothness","_Glossiness","_Metallic","_EnvironmentReflections","_SpecularHighlights"})
                if(material.HasProperty(property))material.SetFloat(property,0);
            if(material.HasProperty("_EnvironmentReflections"))material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if(material.HasProperty("_SpecularHighlights"))material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return material;
        }
        public static void Configure(Renderer renderer)
        {
            bool world=renderer.gameObject.layer!=10 && renderer.gameObject.layer!=FactoryInventorySlot.Layer && renderer.gameObject.layer!=FactoryDeathPresentation.Layer && !renderer.GetComponent<TextMesh>();
            renderer.shadowCastingMode=world?ShadowCastingMode.TwoSided:ShadowCastingMode.Off;
            renderer.receiveShadows=world;
            if(!world && renderer is MeshRenderer)
            {
                // Keep camera-attached UI transforms in sync instead of caching them with world instances.
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                if(renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_BaseColor"))
                    properties.SetColor("_BaseColor",renderer.sharedMaterial.GetColor("_BaseColor"));
                else properties.SetFloat("_Cull",0);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
