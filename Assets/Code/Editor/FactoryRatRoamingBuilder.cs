using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryRatRoamingBuilder
    {
        const int TunnelMask = 1 << 8;

        public static void BakeBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FactoryShift.unity");
            Bake(Object.FindAnyObjectByType<FactoryRatHunt>());
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the rat routes in FactoryShift.");
        }

        public static void BakeAndVerifyBatch()
        {
            BakeBatch();
            FactoryRatHuntVerification.RunBatch();
        }

        internal static void Bake(FactoryRatHunt hunt)
        {
            var sewer = hunt.transform.parent;
            float floor = sewer.Find("Maintenance hatches/A • sewer landing").position.y - .18f;
            var tiles = sewer.Find("Tunnel tiles").Cast<Transform>().ToDictionary(tile => tile.name.Substring(0, 5),
                tile => new Vector3(tile.position.x, floor, tile.position.z));
            var neighbors = tiles.Keys.ToDictionary(tile => tile, _ => new List<string>());
            Physics.SyncTransforms();
            foreach (var (from, to) in FactorySewerBuilder.TunnelConnections())
            {
                if (!Walkable(tiles[from], tiles[to], floor)) continue;
                neighbors[from].Add(to);
                neighbors[to].Add(from);
            }

            int baked = 0;
            foreach (var rat in hunt.targets.Where(target => target.food == FactoryRatFood.LiveRat))
            {
                string start = tiles.Keys.Where(tile => neighbors[tile].Count > 0)
                    .OrderBy(tile => (tiles[tile] - rat.forage).sqrMagnitude)
                    .FirstOrDefault(tile => Walkable(rat.forage, tiles[tile], floor));
                if (start == null)
                    throw new InvalidOperationException($"Rat pipe near {rat.mouth} has no walkable connection to the tunnels.");

                rat.roamRoutes = neighbors[start].OrderByDescending(tile => (tiles[tile] - rat.mouth).sqrMagnitude)
                    .Take(2).Select(next =>
                    {
                        string end = neighbors[next].Where(tile => tile != start)
                            .OrderByDescending(tile => (tiles[tile] - rat.mouth).sqrMagnitude).FirstOrDefault();
                        return new FactoryRatTarget.Route
                        {
                            points = end == null ? new[] { tiles[start], tiles[next] } :
                                new[] { tiles[start], tiles[next], tiles[end] }
                        };
                    }).ToArray();
                EditorUtility.SetDirty(rat);
                baked += rat.roamRoutes.Length;
            }
            EditorSceneManager.MarkSceneDirty(hunt.gameObject.scene);
            Debug.Log($"Baked {baked} rat routes through connected sewer passages.");
        }

        static bool Walkable(Vector3 from, Vector3 to, float level)
        {
            Vector3 start = new Vector3(from.x, level + .28f, from.z);
            Vector3 end = new Vector3(to.x, level + .28f, to.z);
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            const float clearance = .14f;
            if (Physics.CheckSphere(start, clearance, TunnelMask, QueryTriggerInteraction.Ignore) ||
                Physics.CheckSphere(end, clearance, TunnelMask, QueryTriggerInteraction.Ignore) ||
                (distance > .001f && Physics.SphereCast(start, clearance, delta / distance, out _, distance,
                    TunnelMask, QueryTriggerInteraction.Ignore))) return false;
            int samples = Mathf.CeilToInt(distance / .48f);
            for (int i = 0; i <= samples; i++)
            {
                Vector3 point = samples == 0 ? from : Vector3.Lerp(from, to, i / (float)samples);
                point.y = level;
                if (!FactoryRatHuntBuilder.FloorAt(point, out _)) return false;
            }
            return true;
        }
    }
}
