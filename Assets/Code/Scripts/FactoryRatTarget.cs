using UnityEngine;

namespace Cardboard
{
    public enum FactoryRatFood { LiveRat, RottenRat, Droppings }

    public sealed class FactoryRatTarget : MonoBehaviour
    {
        enum Motion { Hidden, Emerging, Foraging, Returning }

        public FactoryRatFood food;
        public Transform ratModel;
        public Vector3 den, mouth, forage;
        public float firstEmergence;

        public bool Available => gameObject.activeSelf && (food != FactoryRatFood.LiveRat ||
            (motion != Motion.Hidden && Vector3.Dot(transform.position - mouth, mouth - den) > .08f));
        Motion motion;
        float wait, gait;
        Vector3 waypoint;

        public void ResetTarget()
        {
            if (food == FactoryRatFood.LiveRat) Hide(firstEmergence);
            else gameObject.SetActive(true);
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
                if ((player - mouth).sqrMagnitude > 20 * 20 || (wait -= dt) > 0) return;
                gameObject.SetActive(true);
                motion = Motion.Emerging;
                waypoint = mouth;
            }

            if (motion == Motion.Foraging)
            {
                var displacement = player - transform.position;
                displacement.y = 0;
                if ((wait -= dt) > 0 && (displacement.sqrMagnitude > 3.8f * 3.8f ||
                    Physics.Linecast(transform.position + Vector3.up * .18f, player + Vector3.up * .85f, 1 << 8,
                        QueryTriggerInteraction.Ignore))) return;
                motion = Motion.Returning;
                waypoint = mouth;
            }

            float speed = motion == Motion.Returning ? 3.45f : 1.7f;
            Vector3 direction = waypoint - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), dt * 900);
            transform.position = Vector3.MoveTowards(transform.position, waypoint, speed * dt);
            gait += dt * speed * 14;
            ratModel.localPosition = Vector3.up * (.034f * Mathf.Abs(Mathf.Sin(gait)));
            ratModel.localRotation = Quaternion.Euler(0, 0, 6 * Mathf.Sin(gait));
            if (transform.position != waypoint) return;

            if (motion == Motion.Returning)
            {
                if (waypoint == mouth) waypoint = den;
                else Hide(3f + firstEmergence);
            }
            else if (waypoint == mouth) waypoint = forage;
            else
            {
                motion = Motion.Foraging;
                wait = 2.6f;
                ratModel.localPosition = Vector3.zero;
                ratModel.localRotation = Quaternion.identity;
            }
        }
    }
}
