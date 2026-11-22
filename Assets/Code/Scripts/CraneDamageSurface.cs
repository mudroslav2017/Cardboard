using UnityEngine;

namespace Cardboard
{
    public sealed class CraneDamageSurface : MonoBehaviour
    {
        [SerializeField] Renderer[] panels;
        [SerializeField] Color paint = Color.white;
        [SerializeField, Min(1)] int impactScale = 10;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color DamagePaint = new Color(.39f, .15f, .12f);
        MaterialPropertyBlock properties;
        float lastImpact = float.NegativeInfinity;

        public int Integrity { get; private set; } = 100;

        public void Impact(float speed, bool fromAbove)
        {
            float threshold = fromAbove ? 3.2f : 1.2f;
            if (speed < threshold || Integrity == 0 || Time.time - lastImpact < .65f) return;

            Integrity = Mathf.Max(0, Integrity - Mathf.Clamp(Mathf.RoundToInt(speed * impactScale), 8, 28));
            lastImpact = Time.time;
            enabled = true;
            Paint(Color.Lerp(paint, DamagePaint, .8f));
        }

        public void Restore(Color freshPaint)
        {
            paint = freshPaint;
            Integrity = 100;
            lastImpact = float.NegativeInfinity;
            Paint(paint);
        }

        void Update()
        {
            if (Time.time - lastImpact < .35f) return;
            Paint(Color.Lerp(paint, DamagePaint, (100 - Integrity) / 100f));
            enabled = false;
        }

        void Paint(Color color)
        {
            properties ??= new MaterialPropertyBlock();
            properties.SetColor(BaseColor, color);
            foreach (var panel in panels) panel.SetPropertyBlock(properties);
        }
    }
}
