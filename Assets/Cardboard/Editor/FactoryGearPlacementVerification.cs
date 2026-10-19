using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryGearPlacementVerification
    {
        const string Active="Cardboard.GearPlacementVerification";
        static readonly List<string> report=new List<string>();
        static CardboardFactoryGame game;static FactoryGearPlacementPreview preview;
        static int stage;static double started;static float at;static bool background;
        static Vector3 eyeHome;static Quaternion eyeRotation;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static FactoryGearPlacementVerification(){EditorApplication.update+=Tick;}
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);}
        static void Save(){File.WriteAllLines("Library/FactoryGearPlacementVerification.txt",new[]{"RESULT: "+report.Count(l=>l.StartsWith("PASS"))+" PASS, "+report.Count(l=>l.StartsWith("FAIL"))+" FAIL"}.Concat(report));}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            report.Clear();stage=0;started=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            SessionState.SetBool(Active,true);FactoryMainMenu.SkipNextMenu=true;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        static void Finish()
        {
            if(game)
            {
                CardboardMarker.Instance.SetDrawingMode(false,false);game.ResetShift();preview.Refresh();game.enabled=true;game.station.enabled=true;game.mainMenu.enabled=true;
                foreach(var worker in game.crowd.runners)worker.enabled=true;
                Camera.main.transform.SetPositionAndRotation(eyeHome,eyeRotation);Camera.main.GetComponent<CardboardCamera>().enabled=true;
            }
            Application.runInBackground=background;SessionState.SetBool(Active,false);Save();EditorApplication.isPlaying=false;
        }
        static int Visible()=>game.gears.Count(g=>g.placementOutline.gameObject.activeInHierarchy);
        static void Focus(Vector3 eye,Vector3 point)
        {
            Camera.main.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(point-eye));
            typeof(CardboardFactoryGame).GetMethod("UpdateFocus",Private).Invoke(game,null);preview.Refresh();
        }
        static void Aim(FactoryGear gear,float distance=1.2f)
        {var point=gear.transform.position;Focus(new Vector3(point.x,1.62f,point.z+distance),point);}
        static void Frame(int type)
        {
            game.ResetShift();var gear=game.gears.Where(g=>g.recipe.Type==type).OrderBy(g=>g.transform.position.y).First();
            foreach(var other in game.gears)other.SetBroken(other==gear);
            typeof(CardboardFactoryGame).GetMethod("SetOrder",Private).Invoke(game,new object[]{gear.index+1});
            game.ReceiveWorkbenchPart(gear.recipe);Aim(gear,2.2f);at=Time.time;
            Check(preview.Target==gear && Visible()==1,"Preview follows the actual E target for screenshot type "+type);
        }
        static void Setup()
        {
            game=CardboardFactoryGame.Instance;preview=game.GetComponent<FactoryGearPlacementPreview>();
            eyeHome=Camera.main.transform.position;eyeRotation=Camera.main.transform.rotation;
            game.enabled=false;game.station.enabled=false;
            typeof(FactoryMainMenu).GetMethod("Close",Private).Invoke(game.mainMenu,null);game.mainMenu.enabled=false;
            foreach(var worker in game.crowd.runners)worker.enabled=false;Camera.main.GetComponent<CardboardCamera>().enabled=false;
            game.ResetShift();Check(preview && game.gears.Length==17,"Placement preview is installed on all seventeen sockets");
            foreach(var gear in game.gears)
            {
                var outline=gear.placementOutline.GetComponent<LineRenderer>();
                Check(outline.loop && outline.positionCount>=24 && !outline.useWorldSpace && outline.GetComponentsInChildren<Collider>().Length==0,"Outline is a closed authored silhouette without interaction colliders: "+(gear.index+1));
            }
            var target=game.Memorized;Aim(target);Check(preview.Target==null && Visible()==0,"Empty hands do not show a placement outline");
            game.ReceiveWorkbenchPart(target.recipe);
            foreach(var gear in game.gears)
            {
                foreach(var other in game.gears)other.SetBroken(other==gear);
                Aim(gear);Check(game.Focus && game.Focus.gear==gear && preview.Target==gear && Visible()==1,"Only the aimed socket is outlined at standing height: "+(gear.index+1));
            }
            target=game.gears[4];foreach(var gear in game.gears)gear.SetBroken(gear==target);Aim(target);
            Focus(Camera.main.transform.position,Camera.main.transform.position+Vector3.up*3);Check(!preview.Target && Visible()==0,"Looking away clears the previous outline");
            Aim(target,6);Check(!preview.Target && Visible()==0,"A socket outside interaction range is not highlighted");
            var healthy=game.gears.First(g=>g!=target);Aim(healthy);Check(!preview.Target && Visible()==0,"Healthy gears are not shown as installation targets");
            Aim(target);Check(preview.Target==target,"Valid broken target returns when the player aims back");
            var blocker=new GameObject("Temporary preview occlusion check");
            try
            {
                blocker.transform.position=Vector3.Lerp(Camera.main.transform.position,target.transform.position,.5f);
                blocker.AddComponent<BoxCollider>().size=Vector3.one*.35f;
                Focus(Camera.main.transform.position,target.transform.position);Check(!preview.Target && Visible()==0,"A solid obstacle blocks both E interaction and the preview");
            }
            finally{Object.DestroyImmediate(blocker);}
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(target.recipe.Type,false));Aim(target);Check(game.BeginDip(),"Unfinished held gear begins dipping");preview.Refresh();Check(!preview.Target && Visible()==0,"Dipping hides the placement preview");game.FinishDip(target.recipe.finish);Aim(target);
            CardboardMarker.Instance.SetDrawingMode(true);preview.Refresh();Check(!preview.Target && Visible()==0,"Marker drawing mode hides the preview");CardboardMarker.Instance.SetDrawingMode(false,false);Aim(target);
            game.mainMenu.Show();preview.Refresh();Check(!preview.Target && Visible()==0,"Opening the menu hides the preview");typeof(FactoryMainMenu).GetMethod("Close",Private).Invoke(game.mainMenu,null);Aim(target);
            game.captureSequence.Begin();preview.Refresh();Check(!preview.Target && Visible()==0,"Capture hides the preview even with a stale focus");game.captureSequence.Cancel();
            game.ResetShift();target=game.Memorized;game.ReceiveWorkbenchPart(target.recipe);Aim(target);game.ReleasePart(true);preview.Refresh();Check(!preview.Target && Visible()==0,"Throwing the held gear immediately clears the outline");
            game.ResetShift();target=game.Memorized;game.ReceiveWorkbenchPart(target.recipe);Aim(target);Check(game.TryInstall(target),"Highlighted socket accepts the matching finished gear");preview.Refresh();Check(!preview.Target && Visible()==0 && !target.broken,"Successful repair clears the outline in the same frame");
            Frame(1);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false))return;
            if(EditorApplication.timeSinceStartup-started>45){report.Add("FAIL verification timed out");Finish();return;}
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !CardboardFactoryGame.Instance || !CardboardRunner.Player)return;
            try
            {
                if(stage==0){Setup();stage=1;Save();return;}
                if(Time.time-at<.25f)return;
                if(stage<=3){CardboardFactorySceneBuilder.Capture("GearPlacementType"+stage);if(stage<3)Frame(stage+1);else{Finish();return;}stage++;}
            }
            catch(Exception error){report.Add("FAIL "+error);Finish();}
        }
    }
}