#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cardboard
{
    internal sealed partial class FactoryCoopShift
    {
        FactoryPhysicsProp[] props;
        CardboardDrawingSurface[] surfaces;
        readonly Dictionary<FactoryPhysicsProp, int> propIds = new Dictionary<FactoryPhysicsProp, int>();
        readonly Dictionary<CardboardDrawingSurface, int> surfaceIds = new Dictionary<CardboardDrawingSurface, int>();
        readonly List<FactoryCoopStroke> strokes = new List<FactoryCoopStroke>();
        Transform handAnchor;
        readonly Quaternion[] propGrip = new Quaternion[MaximumPlayers];
        const int StrokesPerFrame = 80;

        public int AppliedStrokes { get; private set; }
        public bool LocalHandsBusy => HandsBusy(Local);
        bool HandsBusy(FactoryCoopPlayer player) =>
            player.Hand.HasPart || player.Prop > 0 || (trayCarrier != 0 && player == Player(trayCarrier)) ||
            (productionOwner != 0 && player == Player(productionOwner)) || (kitchenOwner != 0 && player == Player(kitchenOwner));

        void SetUpSharedObjects(GameObject[] roots, bool host)
        {
            props = roots.SelectMany(root => root.GetComponentsInChildren<FactoryPhysicsProp>(true))
                .Where(prop => !prop.workbenchPart && (!game.kitchen || !game.kitchen.OwnsHeld(prop)))
                .ToArray();
            surfaces = roots.SelectMany(root => root.GetComponentsInChildren<CardboardDrawingSurface>(true)).ToArray();
            for (int i = 0; i < props.Length; i++) propIds.Add(props[i], i + 1);
            for (int i = 0; i < surfaces.Length; i++) surfaceIds.Add(surfaces[i], i);
            world.Props = new FactoryCoopPose[props.Length];
            if (host)
            {
                handAnchor = new GameObject("Co-op prop grip").transform;
                handAnchor.SetParent(game.transform);
            }
        }

        void ResetSharedObjects()
        {
            foreach (var prop in props)
            {
                if (!prop || !prop.Body) continue;
                if (prop.IsHeld && prop.TryGetComponent(out FactoryStapler stapler)) stapler.EndGrip();
                prop.ResetHome();
                if (localRole != Host)
                {
                    prop.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    prop.Body.isKinematic = true;
                }
            }
            foreach (var surface in surfaces)
                if (surface) surface.ClearDrawing();
            strokes.Clear();
            AppliedStrokes = 0;
        }

        void TakeProp(FactoryCoopPlayer actor, int index)
        {
            var prop = props[index - 1];
            if (!prop || !prop.Body || prop.IsHeld || !prop.gameObject.activeInHierarchy) return;
            actor.Prop = index;
            prop.BeginCarry();
            propGrip[actor.Role - 1] = Quaternion.Inverse(actor.Look) * prop.Body.rotation;
            if (prop.TryGetComponent(out FactoryStapler stapler))
            {
                PositionGrip(actor);
                stapler.BeginGrip(handAnchor);
            }
        }

        void StepProps(float dt)
        {
            foreach (var actor in players)
            {
                if (actor.Prop <= 0) continue;
                var prop = props[actor.Prop - 1];
                if (!prop || !prop.Body || game.Ended)
                {
                    ReleaseProp(actor, Vector3.forward, false);
                    continue;
                }
                PositionGrip(actor);
                if (prop.TryGetComponent(out FactoryStapler stapler))
                {
                    stapler.AdvanceGrip(handAnchor, dt);
                    continue;
                }

                var body = prop.Body;
                var desired = handAnchor.TransformPoint(new Vector3(.12f, -.25f, 1.08f));
                if ((desired - body.position).sqrMagnitude > 16)
                {
                    ReleaseProp(actor, Vector3.forward, false);
                    continue;
                }
                body.linearVelocity = Vector3.ClampMagnitude((desired - body.position) * 12, 11);
                var rotation = actor.Look * propGrip[actor.Role - 1];
                body.MoveRotation(Quaternion.Slerp(body.rotation, rotation, Mathf.Clamp01(dt * 13)));
            }
        }

        void PositionGrip(FactoryCoopPlayer actor) =>
            handAnchor.SetPositionAndRotation(actor.Position + Vector3.up * (1.62f + actor.Jump), actor.Look);

        void ReleaseProp(FactoryCoopPlayer actor, Vector3 forward, bool thrown)
        {
            if (actor.Prop <= 0) return;
            var prop = props[actor.Prop - 1];
            if (prop && prop.TryGetComponent(out FactoryStapler stapler))
            {
                if (thrown)
                {
                    stapler.Press();
                    return;
                }
                stapler.EndGrip();
            }
            actor.Prop = 0;
            if (!prop || !prop.Body) return;
            float speed = thrown ? FactoryObjectCarry.ThrowSpeed(prop.Body.mass, prop.instructionSheet) : 0;
            prop.Release(forward * speed, thrown);
        }

        public bool IdentifyStroke(CardboardDrawingSurface surface, ref FactoryCoopStroke stroke)
        {
            if (!surfaceIds.TryGetValue(surface, out int index)) return false;
            stroke.Surface = index;
            return true;
        }

        void AddStroke(FactoryCoopPlayer actor, FactoryCoopStroke stroke)
        {
            if (strokes.Count >= 100000 || !ValidStroke(stroke) ||
                (stroke.Contact - actor.Position - Vector3.up * (1.62f + actor.Jump)).sqrMagnitude > 5 ||
                !surfaces[stroke.Surface].TryGetComponent(out Collider surfaceCollider) ||
                surfaceCollider.bounds.SqrDistance(stroke.Contact) > .25f) return;
            PaintStroke(stroke);
            strokes.Add(stroke);
        }

        bool ValidStroke(FactoryCoopStroke stroke) =>
            stroke.Surface >= 0 && stroke.Surface < surfaces.Length && surfaces[stroke.Surface] &&
            stroke.Color >= 0 && stroke.Color < 5 && stroke.Side >= 0 && stroke.Side <= 1 &&
            stroke.Radius >= .003f && stroke.Radius <= .1f &&
            InTexture(stroke.From) && InTexture(stroke.To) && stroke.Contact.sqrMagnitude < 20000;

        static bool InTexture(Vector2 uv) => uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;

        void PaintStroke(FactoryCoopStroke stroke)
        {
            var surface = surfaces[stroke.Surface];
            if (stroke.Erase) surface.EraseStroke(stroke.From, stroke.To, stroke.Radius, stroke.Side);
            else if (CardboardMarker.Instance)
                surface.PaintStroke(stroke.From, stroke.To, CardboardMarker.Instance.Colors[stroke.Color], stroke.Radius, stroke.Side);
        }

        void ReadSharedObjects()
        {
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                if (prop && prop.Body) world.Props[i] = new FactoryCoopPose { Position = prop.Body.position, Rotation = prop.Body.rotation };
            }
        }

        void SelectStrokes(int receivedStrokes)
        {
            world.StrokeStart = Mathf.Clamp(receivedStrokes, 0, strokes.Count);
            world.Strokes = strokes.GetRange(world.StrokeStart, Mathf.Min(strokes.Count - world.StrokeStart, StrokesPerFrame)).ToArray();
        }

        bool ApplySharedObjects(FactoryCoopWorld incoming)
        {
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                if (!prop || !prop.Body) continue;
                var pose = incoming.Props[i];
                prop.Body.position = pose.Position;
                prop.Body.rotation = pose.Rotation;
            }
            if (incoming.Strokes == null || incoming.Strokes.Length > StrokesPerFrame ||
                incoming.StrokeStart < 0 || incoming.StrokeStart > AppliedStrokes) return false;
            for (int i = AppliedStrokes - incoming.StrokeStart; i < incoming.Strokes.Length; i++)
            {
                var stroke = incoming.Strokes[i];
                if (!ValidStroke(stroke)) return false;
                PaintStroke(stroke);
                AppliedStrokes++;
            }
            return true;
        }
    }
}
#endif
