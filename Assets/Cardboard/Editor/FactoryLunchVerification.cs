using System;
using UnityEngine;
using UnityEditor;

namespace Cardboard.Editor
{
    public static class FactoryLunchVerification
    {
        static void MovePlayer(Vector3 position)
        {
            var player=CardboardRunner.Player;var body=player.GetComponent<CharacterController>();
            body.enabled=false;player.transform.position=position;body.enabled=true;
            Camera.main.transform.position=position+Vector3.up*1.62f;Physics.SyncTransforms();
        }
        static void AtCounter(FactoryLunch lunch)
        {
            MovePlayer(new Vector3(lunch.pickup.position.x,0,lunch.pickup.position.z+1.5f));
            Camera.main.transform.rotation=Quaternion.LookRotation(lunch.pickup.position-Camera.main.transform.position);
        }
        public static void Check(CardboardFactoryGame game,Action<bool,string> check)
        {
            var lunch=game.lunch;check(lunch,"Shift has a lunch minigame installed");if(!lunch) return;
            Vector3 original=CardboardRunner.Player.transform.position;
            Quaternion view=Camera.main.transform.rotation;
            game.ResetShift();AtCounter(lunch);
            check(lunch.State==LunchState.Waiting && !lunch.TryTake(),"Tray cannot be taken before lunchtime");
            lunch.TickSchedule(59);check(lunch.State==LunchState.Waiting,"Lunch does not open before one minute");
            lunch.TickSchedule(1);check(lunch.State==LunchState.Ready && game.Mistakes==0,"Lunch opens at one minute without awarding suspicion");
            lunch.TickSchedule(600);check(lunch.State==LunchState.Ready && game.Mistakes==0,"Missing the lunch announcement has no deadline penalty");
            var model=lunch.tray.Find("podnos • normalized model");
            check(model && Array.Exists(model.GetComponentsInChildren<MeshFilter>(),filter=>AssetDatabase.GetAssetPath(filter.sharedMesh)=="Assets/podnos.fbx"),"Tray uses the supplied podnos.fbx asset");
            check(lunch.food.Length==3 && lunch.food[0].GetComponent<MeshFilter>().sharedMesh.name=="Cube","Meal consists of cube placeholders");
            var target=game.TargetForRay(new Ray(Camera.main.transform.position,lunch.pickup.position-Camera.main.transform.position));
            check(target && target.GetComponent<FactoryLunchControl>() && !target.GetComponent<FactoryLunchControl>().serve,"Player ray reaches the tray on the serving counter");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));
            check(!lunch.TryTake() && !game.HandsBusy,"A held machine part prevents taking a tray");game.DropPart();
            check(lunch.TryTake() && game.HandsBusy && !game.PlayerBlocked && lunch.tray.parent==Camera.main.transform,"Taking tray starts balance while leaving walking and camera available");
            check(!lunch.TryTake() && !game.station.Operate(StationPart.Feed),"Tray cannot be taken twice and its clicks cannot operate machinery");
            CardboardMarker.Instance.SetDrawingMode(true);
            check(!CardboardMarker.DrawingMode,"Marker cannot steal balance clicks while carrying lunch");
            float velocity=lunch.AngularSpeed;lunch.TickBalance(.02f,true,false);
            check(lunch.AngularSpeed<velocity,"Left click applies a leftward balancing impulse");
            velocity=lunch.AngularSpeed;lunch.TickBalance(.02f,false,true);
            check(lunch.AngularSpeed>velocity,"Right click applies a rightward balancing impulse");
            check(!lunch.TryServe(),"Lunch cannot be completed at the serving counter");
            for(int i=0;i<700 && lunch.Carrying;i++)
            {
                bool left=lunch.Tilt>.12f && lunch.AngularSpeed>0;
                bool right=lunch.Tilt<-.12f && lunch.AngularSpeed<0;
                lunch.TickBalance(.02f,left,right);
            }
            check(lunch.Carrying && lunch.CarryTime>13,"Correct alternating clicks keep the tray balanced for a complete carry");
            var hud=UnityEngine.Object.FindFirstObjectByType<FactoryLunchHUD>();
            Camera.main.transform.rotation=Quaternion.Euler(8,180,0);
            hud.SendMessage("LateUpdate");CardboardFactorySceneBuilder.Capture("LunchBalancing");
            MovePlayer(new Vector3(lunch.destination.position.x,0,lunch.destination.position.z+1.6f));
            Camera.main.transform.rotation=Quaternion.LookRotation(lunch.destination.position-Camera.main.transform.position);
            target=game.TargetForRay(new Ray(Camera.main.transform.position,lunch.destination.position-Camera.main.transform.position));
            check(target && target.GetComponent<FactoryLunchControl>() && target.GetComponent<FactoryLunchControl>().serve,"Player ray reaches the lunch delivery spot");
            check(lunch.TryServe() && lunch.State==LunchState.Served && !game.HandsBusy && game.Mistakes==0,"Balanced tray can be served without suspicion");
            check(!lunch.TryTake() && !lunch.TryServe(),"A served lunch cannot restart or resolve twice");
            hud.SendMessage("LateUpdate");CardboardFactorySceneBuilder.Capture("LunchServed");
            game.ResetShift();lunch.TickSchedule(60);AtCounter(lunch);lunch.TryTake();
            for(int i=0;i<1000 && lunch.Carrying;i++) lunch.TickBalance(.02f,false,false);
            lunch.TickSpill(2);
            check(lunch.State==LunchState.Spilled && game.Mistakes==1 && game.LastMistakeReason.Contains("еду"),"Uncorrected wobble spills the meal and adds exactly one full star");
            bool allFell=true;foreach(var food in lunch.food) allFell &= food.parent==lunch.transform && Mathf.Abs(food.position.y+.08f)<.001f;
            check(allFell && !game.HandsBusy,"All food falls onto the floor and normal controls resume");
            for(int i=0;i<50;i++) lunch.TickBalance(.02f,false,false);
            check(game.Mistakes==1 && !lunch.TryTake(),"A spill cannot award repeated penalties or restart the same lunch");
            hud.SendMessage("LateUpdate");UnityEngine.Object.FindFirstObjectByType<FactoryMistakeHUD>().Refresh();
            Camera.main.transform.rotation=Quaternion.LookRotation(lunch.food[1].position-Camera.main.transform.position);
            CardboardFactorySceneBuilder.Capture("LunchSpilled");
            game.ResetShift();lunch.TickSchedule(60);AtCounter(lunch);lunch.TryTake();game.Capture(FactoryDeath.Press);
            check(!lunch.Carrying && !game.HandsBusy && !lunch.tray.gameObject.activeSelf,"Capture cancels balancing without an extra lunch penalty");
            game.ResetShift();lunch.TickSchedule(60);AtCounter(lunch);lunch.TryTake();
            game.RegisterMistake("Полторы звезды",1.5f);game.RegisterMistake("Ползвезды",.5f);
            for(int i=0;i<1000 && lunch.Carrying;i++) lunch.TickBalance(.02f,false,false);
            check(game.Mistakes==3 && game.CaptureActive,"Lunch spill shares the three-star capture threshold with other violations");
            game.ResetShift();
            bool restored=true;foreach(var food in lunch.food) restored &= food.parent==lunch.tray && food.gameObject.activeSelf;
            check(restored && lunch.State==LunchState.Waiting && lunch.Elapsed==0 && lunch.tray.gameObject.activeSelf,"New shift restores tray, meal, schedule and lunch state");
            MovePlayer(original);Camera.main.transform.rotation=view;hud.SendMessage("LateUpdate");
        }
    }
}
