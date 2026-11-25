using UnityEngine;

namespace Cardboard
{
    public sealed class FactoryCraneImpact : MonoBehaviour
    {
        [SerializeField] Renderer[] affectedPanels;
        [SerializeField] Color paint = Color.white;
        [SerializeField] string faultMessage;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color BruisedCardboard = new Color(.35f, .13f, .08f);
        MaterialPropertyBlock block;

        public string FaultMessage => faultMessage;
        public int Condition { get; private set; } = 100;

        public void Damage(float speed)
        {
            Condition = Mathf.Max(0, Condition - Mathf.Clamp(Mathf.RoundToInt(speed * 6f), 8, 28));
            ApplyPaint(Color.Lerp(paint, BruisedCardboard, 1f - Condition / 100f));
        }

        public void Restore(Color freshPaint)
        {
            Condition = 100;
            paint = freshPaint;
            ApplyPaint(paint);
        }

        public void Restore() => Restore(paint);

        void ApplyPaint(Color color)
        {
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            foreach (var panel in affectedPanels) panel.SetPropertyBlock(block);
        }
    }
}
