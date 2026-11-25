using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class FactoryCraneGauge : Graphic
    {
        static readonly Color Green = new Color32(105, 190, 122, 255);
        static readonly Color Yellow = new Color32(241, 212, 101, 255);
        static readonly Color Orange = new Color32(236, 139, 57, 255);
        static readonly Color Red = new Color32(225, 55, 66, 255);
        static readonly Color Rim = new Color32(50, 38, 28, 255);
        static readonly Color Paper = new Color32(243, 222, 181, 255);
        static readonly Vector2[] Circle = UnitCircle();

        Vector2 shownDeflection;
        float shownStress;
        GameObject lunchNotice;

        internal static FactoryCraneGauge Create(Transform hud)
        {
            var root = new GameObject("Crane load gauge", typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(hud, false);
            var gauge = root.AddComponent<FactoryCraneGauge>();
            gauge.raycastTarget = false;
            var anchor = new Vector2(.5f, 0f);
            var rect = gauge.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = Vector2.one * 104f;
            rect.anchoredPosition = new Vector2(0f, 24f);
            if (hud.TryGetComponent(out FactoryLunchHUD lunchHud)) gauge.lunchNotice = lunchHud.panel;
            return gauge;
        }

        internal void Present(Vector2 deflection, float stress, bool released, float dt)
        {
            float bottom = lunchNotice && lunchNotice.activeSelf ? 108f : 24f;
            rectTransform.anchoredPosition = new Vector2(0f, bottom);

            float blend = 1f - Mathf.Exp(-19f * dt);
            Vector2 nextDeflection = Vector2.Lerp(shownDeflection, deflection, blend);
            float nextStress = released ? 1f : Mathf.Lerp(shownStress, stress, blend);
            if ((nextDeflection - shownDeflection).sqrMagnitude < .000001f &&
                Mathf.Abs(nextStress - shownStress) < .0005f) return;
            shownDeflection = nextDeflection;
            shownStress = nextStress;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            shownDeflection = Vector2.zero;
            shownStress = 0f;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f - 2f;
            Disc(mesh, center + Vector2.down * 2f, radius + 1f, new Color(0f, 0f, 0f, .4f));
            Disc(mesh, center, radius, Rim);
            Disc(mesh, center, radius - 3f, Paper);
            Disc(mesh, center, radius - 5f, StressColor(shownStress));

            Vector2 dot = center + shownDeflection * (radius - 15f);
            Disc(mesh, dot, 7.5f, Rim);
            Disc(mesh, dot, 4.8f, Paper);
        }

        static Color StressColor(float stress)
        {
            if (stress < .48f) return Color.Lerp(Green, Yellow, stress / .48f);
            if (stress < .8f) return Color.Lerp(Yellow, Orange, (stress - .48f) / .32f);
            return Color.Lerp(Orange, Red, (stress - .8f) / .2f);
        }

        static Vector2[] UnitCircle()
        {
            var points = new Vector2[48];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = 2f * Mathf.PI * i / points.Length;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return points;
        }

        static void Disc(VertexHelper mesh, Vector2 center, float radius, Color32 fill)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, fill, Vector2.zero);
            for (int i = 0; i < Circle.Length; i++)
            {
                mesh.AddVert(center + Circle[i] * radius, fill, Vector2.zero);
                mesh.AddTriangle(start, start + i + 1, start + (i + 1) % Circle.Length + 1);
            }
        }
    }
}
