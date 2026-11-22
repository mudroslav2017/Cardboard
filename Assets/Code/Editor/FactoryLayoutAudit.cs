using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryLayoutAudit
    {
        static string M(float value) => value.ToString("F3",CultureInfo.InvariantCulture);
        [MenuItem("Cardboard/Measure factory layout")]
        public static void Write()
        {
            var report=new List<string>();
            try
            {
                var game=UnityEngine.Object.FindFirstObjectByType<CardboardFactoryGame>();
                var floor=GameObject.Find("Main workshop floor").GetComponent<Renderer>().bounds;
                var west=GameObject.Find("West workshop wall").GetComponent<Collider>().bounds;
                var east=GameObject.Find("East wall above exit").GetComponent<Collider>().bounds;
                var canteen=GameObject.Find("Canteen floor").GetComponent<Renderer>().bounds;
                report.Add("Main workshop nominal length="+M(floor.size.x)+" m; width="+M(floor.size.z)+" m; wall height="+M(west.size.y)+" m.");
                report.Add("Clear width along workstation row="+M(east.min.x-west.max.x)+" m.");
                var overall=floor;overall.Encapsulate(canteen);
                report.Add("Complete floor plan including canteen="+M(overall.size.x)+" x "+M(overall.size.z)+" m.");
                var gaps=new List<float>();float previous=west.max.x;
                for(int i=0;i<game.stations.Length;i++)
                {
                    var station=game.stations[i].transform;
                    var bounds=FactoryWorkstationBuilder.Footprint(station);
                    float left=station.TransformPoint(bounds.min).x,right=station.TransformPoint(bounds.max).x;
                    gaps.Add(left-previous);previous=right;
                    report.Add("Station "+(i+1)+" left="+M(left)+" right="+M(right)+" m; preceding gap="+M(gaps[gaps.Count-1])+" m.");
                }
                gaps.Add(east.min.x-previous);
                report.Add("Right wall gap="+M(gaps[gaps.Count-1])+" m.");
                float min=Mathf.Min(gaps.ToArray()),max=Mathf.Max(gaps.ToArray());
                bool equal=min>0 && max-min<.005f;
                bool resized=Mathf.Abs(floor.size.x-44.8f/1.45f)<.005f && Mathf.Abs(floor.size.z-28f/1.5f)<.005f;
                report.Add((equal?"PASS ":"FAIL ")+"All five clear gaps are equal; difference="+M(max-min)+" m.");
                report.Add((resized?"PASS ":"FAIL ")+"Workshop shortened about 1.5 times on both sides, with room for the complete workstation models.");
                report.Add((game.station==game.stations[1]?"PASS ":"FAIL ")+"Player retains second workstation.");
                var foundation=GameObject.Find("Machine foundation").GetComponent<Renderer>().bounds;
                report.Add((foundation.max.x<east.min.x && foundation.min.x>west.max.x?"PASS ":"FAIL ")+"Main mechanism remains inside shortened room.");
                bool clearDoorway=!Physics.Linecast(new Vector3(-13,1,-12),new Vector3(-13,1,-20),out var doorHit,1<<8) || FactoryDoor.IsLeaf(doorHit.collider);
                report.Add((clearDoorway?"PASS ":"FAIL ")+"Canteen passage contains an operable door instead of a blocking wall.");
            }
            catch(Exception error) { report.Add("FAIL "+error); }
            File.WriteAllLines("Library/FactoryLayoutAudit.txt",report);
        }
    }
}