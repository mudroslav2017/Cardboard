using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryRatHuntVerification
    {
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FactoryShift.unity");
            Verify(UnityEngine.Object.FindAnyObjectByType<CardboardFactoryGame>());
        }

        static void Verify(CardboardFactoryGame game)
        {
            var player = game.GetComponentsInChildren<CardboardRunner>().Single(runner => runner.isPlayer);
            var previous = CardboardRunner.Player;
            CardboardRunner.Player = player;
            game.mainMenu.CloseForFilming();
            try { CheckHunt(game, player); }
            finally { CardboardRunner.Player = previous; }
        }

        static void CheckHunt(CardboardFactoryGame game, CardboardRunner player)
        {
            var hunt = game.ratHunt;
            Check(hunt, "Missing rat hunt in FactoryShift.");
            var hatches = hunt.transform.parent.Find("Maintenance hatches");
            var hatch = hatches.Find("A • workshop hatch").GetComponent<FactoryInteractable>();
            var surface = hatch.transform.Find("Workshop landing");
            var entrance = hatches.Find("A • sewer landing");
            var climb = hatches.Find("A • climb to workshop").GetComponent<FactoryInteractable>();
            var delivery = hunt.GetComponentsInChildren<FactoryInteractable>().Single(target => target.action == FactoryAction.RatDelivery);
            var live = hunt.targets.Where(target => target.food == FactoryRatFood.LiveRat).OrderBy(target =>
                (target.mouth - entrance.position).sqrMagnitude).ToArray();
            var wrong = hunt.targets.Where(target => target.food != FactoryRatFood.LiveRat).ToArray();
            Check(live.Length >= FactoryRatHunt.Quota && wrong.Select(target => target.food).Distinct().Count() == 2,
                "Both forbidden types and enough distinct pipes are present.");
            var focus = typeof(CardboardFactoryGame).GetProperty(nameof(CardboardFactoryGame.Focus));

            void Use(FactoryInteractable target, Vector3 eye)
            {
                Physics.SyncTransforms();
                var goal = target.GetComponent<Collider>().bounds.center;
                var ray = new Ray(eye, goal - eye);
                var found = game.TargetForRay(ray);
                Check(found == target, "Could not aim at " + target.name + " from " + eye + ", hit " + found?.name);
                focus.SetValue(game, found);
                game.Interact();
            }
            void Grab(FactoryRatTarget target)
            {
                var collider = target.GetComponent<Collider>();
                var action = target.GetComponent<FactoryInteractable>();
                Physics.SyncTransforms();
                foreach (Vector3 offset in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    Vector3 eye = target.transform.position + offset * 1.55f + Vector3.up * 1.62f;
                    if (game.TargetForRay(new Ray(eye, collider.bounds.center - eye)) != action) continue;
                    Use(action, eye);
                    return;
                }
                throw new InvalidOperationException("No clear grab ray for " + target.name + " at " + target.transform.position);
            }

            hunt.ResetHunt();
            Use(hatch, surface.position + Vector3.up * 1.62f);
            Check(player.InSewer && hunt.CanGrab, "Hatch opens the task.");
            var first = live[0];
            Check(!first.Available, "A hidden rat cannot be grabbed.");
            for (int i = 0; i < 65; i++) first.Tick(.04f, first.forage + Vector3.up * 12 + Vector3.left * 8);
            Check(first.Available && Vector3.Distance(first.transform.position, first.forage) < .02f,
                "Rat emerges and runs to the bank.");
            Vector3 before = first.transform.position;
            first.Tick(.1f, before + Vector3.right * .5f);
            Check(Vector3.Distance(first.transform.position, first.mouth) < Vector3.Distance(before, first.mouth),
                "Approaching player sends the rat back to its own pipe.");
            for (int i = 0; i < 32; i++) first.Tick(.04f, before);
            Check(!first.Available && !first.gameObject.activeSelf && Vector3.Distance(first.transform.position, first.den) < .02f,
                "Returning rat disappears inside the original pipe.");
            for (int i = 0; i < 100; i++) first.Tick(.04f, first.forage);
            Check(first.Available, "A rat reappears from the pipe.");

            Grab(wrong.First(target => target.food == FactoryRatFood.RottenRat));
            Check(hunt.Food == 0 && hunt.Strikes == 1 && !game.CaptureActive, "Rotten rat costs one strike and yields zero food.");
            Grab(wrong.First(target => target.food == FactoryRatFood.Droppings));
            Check(hunt.Food == 0 && hunt.Strikes == 2 && !game.CaptureActive, "Droppings cost one strike and yield zero food.");
            foreach (var rat in live.Take(FactoryRatHunt.Quota))
            {
                rat.ResetTarget();
                for (int i = 0; i < 65; i++) rat.Tick(.04f, rat.forage + Vector3.left * 9);
                Grab(rat);
            }
            Check(hunt.Food == FactoryRatHunt.Quota && !hunt.CanGrab && !hunt.CanDeliver &&
                hunt.basketContents.All(rat => rat.activeSelf), "Ten live rats fill the basket and cap the quota.");
            player.UseSewerAccess(entrance, surface);
            Use(climb, entrance.position + Vector3.up * 1.62f);
            Check(!player.InSewer && hunt.CanDeliver, "Climbing out preserves the full basket.");
            Use(delivery, delivery.transform.position + new Vector3(0, .62f, 1.45f));
            Check(hunt.Delivered && !hunt.Deliver() && hunt.basketContents.All(rat => !rat.activeSelf),
                "Kitchen accepts the full basket once.");

            hunt.ResetHunt();
            Check(hunt.Food == 0 && hunt.Strikes == 0 && !hunt.Delivered && wrong.All(item => item.Available),
                "A new task restores quota, strikes and forbidden items.");
            Use(hatch, surface.position + Vector3.up * 1.62f);
            for (int i = 0; i < 3; i++) Grab(wrong[i]);
            Check(hunt.Strikes == 3 && game.State == FactoryState.Chased && game.CaptureActive && !player.InSewer,
                "Only the third strike starts the existing factory pursuit at the hatch.");
            game.captureSequence.Cancel();
            Debug.Log("Kitchen rat hunt verified: emergence, fleeing, hiding, respawn, collection, quota, delivery, strikes and capture.");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
