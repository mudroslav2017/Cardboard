using UnityEngine;

namespace Cardboard
{
    public enum FactoryRatFood { LiveRat, RottenRat, Droppings }

    public sealed class FactoryRatTarget : MonoBehaviour
    {
        enum Motion { Hidden, Emerging, Roaming, Returning }

        [System.Serializable]
        public struct Route
        {
            public Vector3[] points;
        }

        public FactoryRatFood food;
        public Transform ratModel;
        public Vector3 den, mouth, forage;
        public float firstEmergence;
        public Route[] roamRoutes;

        bool? replicatedAvailability;
        public bool Available => replicatedAvailability ?? (gameObject.activeSelf && (food != FactoryRatFood.LiveRat ||
            (motion == Motion.Roaming && (waypointIndex > 1 || ClearOfMouth)) ||
            (motion == Motion.Returning && (waypointIndex > 0 || waypointIndex == 0 && ClearOfMouth))));
        bool ClearOfMouth => Vector3.Dot(transform.position - mouth, mouth - den) > .08f;
        Motion motion;
        float wait, gait, distanceToPause;
        int waypointIndex, nextRoute;
        bool startled;
        Vector3[] activeRoute;

        public void ResetTarget()
        {
            replicatedAvailability = null;
            if (food == FactoryRatFood.LiveRat)
            {
                nextRoute = 0;
                Hide(firstEmergence);
            }
            else gameObject.SetActive(true);
        }

        internal FactoryCoopRat ReadCoopState() => new FactoryCoopRat
        {
            Visible = gameObject.activeSelf, Available = Available,
            Pose = new FactoryCoopPose { Position = transform.position, Rotation = transform.rotation }
        };

        internal void ApplyCoopState(FactoryCoopRat state)
        {
            replicatedAvailability = state.Available;
            gameObject.SetActive(state.Visible);
            transform.SetPositionAndRotation(state.Pose.Position, state.Pose.Rotation);
        }

        public void Remove()
        {
            if (food == FactoryRatFood.LiveRat) Hide(5f + firstEmergence);
            else gameObject.SetActive(false);
        }

        void Hide(float seconds)
        {
            motion = Motion.Hidden;
            wait = seconds;
            activeRoute = null;
            gait = 0;
            transform.position = den;
            transform.rotation = Quaternion.LookRotation(mouth - den);
            ratModel.localPosition = Vector3.zero;
            ratModel.localRotation = Quaternion.identity;
            gameObject.SetActive(false);
        }

        public void Tick(float dt, Vector3 player)
        {
            if (food != FactoryRatFood.LiveRat) return;
            if (motion == Motion.Hidden)
            {
                if ((player - mouth).sqrMagnitude > 32 * 32 || (wait -= dt) > 0) return;
                gameObject.SetActive(true);
                motion = Motion.Emerging;
                waypointIndex = 0;
                if (roamRoutes != null && roamRoutes.Length > 0)
                {
                    activeRoute = roamRoutes[nextRoute].points;
                    nextRoute = (nextRoute + 1) % roamRoutes.Length;
                }
                startled = false;
                wait = 0;
                distanceToPause = 5f + firstEmergence;
            }

            if (Available && !startled && SeesPlayer(player))
            {
                startled = true;
                if (motion == Motion.Roaming) StartReturn();
            }
            if ((wait -= dt) > 0) return;

            Vector3 goal = waypointIndex switch
            {
                -1 => den,
                0 => mouth,
                1 => forage,
                _ => activeRoute[waypointIndex - 2]
            };
            float speed = startled ? 3.45f : 1.7f;
            Vector3 current = transform.position;
            Vector3 direction = goal - current;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), dt * 900);
            Vector3 step = Vector3.ClampMagnitude(direction, speed * dt);
            Vector3 next = current + step;
            // The tunnel banks and water channels have different floor heights.
            if (waypointIndex > 0 && Physics.Raycast(new Vector3(next.x, forage.y + .55f, next.z),
                Vector3.down, out var floor, 1.15f, 1 << 8, QueryTriggerInteraction.Ignore))
                next.y = floor.point.y + .012f;
            else
                next.y = Mathf.MoveTowards(current.y, goal.y, speed * dt);
            transform.position = next;
            gait += step.magnitude * 14;
            ratModel.localPosition = Vector3.up * (.034f * Mathf.Abs(Mathf.Sin(gait)));
            ratModel.localRotation = Quaternion.Euler(0, 0, 6 * Mathf.Sin(gait));

            if (direction.sqrMagnitude <= step.sqrMagnitude + .000001f)
            {
                if (motion == Motion.Returning)
                {
                    if (waypointIndex-- == -1) Hide(3f + firstEmergence);
                    return;
                }
                if (motion == Motion.Emerging) motion = Motion.Roaming;
                if (waypointIndex == 1 + (activeRoute?.Length ?? 0))
                {
                    StartReturn();
                    return;
                }
                waypointIndex++;
            }

            if (motion == Motion.Roaming && (distanceToPause -= step.magnitude) <= 0)
            {
                distanceToPause = 5f + firstEmergence;
                wait = .35f + firstEmergence * .2f;
                ratModel.localPosition = Vector3.zero;
                ratModel.localRotation = Quaternion.identity;
            }
        }

        void StartReturn()
        {
            motion = Motion.Returning;
            waypointIndex--;
            wait = 0;
        }

        bool SeesPlayer(Vector3 player)
        {
            return (player - transform.position).sqrMagnitude <= 3.8f * 3.8f &&
                !Physics.Linecast(transform.position + Vector3.up * .18f, player + Vector3.up * .85f,
                    1 << 8, QueryTriggerInteraction.Ignore);
        }
    }
}
