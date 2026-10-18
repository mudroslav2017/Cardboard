using System;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class FactoryMistakeVerification
    {
        public static void Check(CardboardFactoryGame game,Action<bool,string> check)
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<FactoryMistakeHUD>();
            check(hud && hud.stars.Length==3,"Exactly three cardboard mistake stars exist");
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FF_troublemarker.otf");
            check(font && CardboardTypography.DefaultFont==font && hud.reasons.font==font,"Supplied Troublemarker is the shared world and HUD font");
            font.RequestCharactersInTexture("Ошибка Рабочие заметили бег Ж Я 123",24);
            check(font.HasCharacter('Я') && font.HasCharacter('Ж'),"Author font supports Russian debug reasons");
            foreach(var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                check(label.font==font,"World lettering uses supplied font: "+label.name);
            game.ResetShift();hud.Refresh();
            check(game.Mistakes==0 && game.MistakeReasons.Count==0 && hud.reasons.text=="","New shift clears all stars and reasons");
            game.ObserveConduct(false,6,.6f,false,2);
            check(game.Mistakes==0,"Unobserved conduct does not award a star");
            game.ObserveConduct(false,0,0,false,.6f);
            game.ObserveConduct(true,0,.6f,true,.1f);
            check(game.Mistakes==0,"Legitimate pump jump does not award a star");
            game.ObserveConduct(true,0,0,false,.1f);
            game.ObserveConduct(true,6,0,false,.8f);
            check(game.Mistakes==1 && !game.Ended && game.LastMistakeReason.Contains("бег"),"Observed running awards first star with its reason");
            for(int i=0;i<180;i++) game.ObserveConduct(true,6,0,false,.016f);
            check(game.Mistakes==1,"Continuous running does not award stars each frame");
            game.ObserveConduct(false,0,0,false,90);
            check(game.Mistakes==1,"Time out of sight never removes a star");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));game.TryInstall(game.gears[0]);
            check(game.Mistakes==1 && game.Repairs==1,"Successful repair never removes a star");
            game.StartInspection();game.CompleteInspection(true);
            check(game.Mistakes==1 && !game.Inspection,"Passing inspection never removes a star");
            game.ObserveConduct(true,0,.6f,false,.1f);
            for(int i=0;i<20;i++) game.ObserveConduct(true,0,.6f,false,.016f);
            check(game.Mistakes==2 && !game.Ended && game.LastMistakeReason.Contains("прыжок"),"One off-pump jump awards exactly the second star");
            hud.Refresh();Canvas.ForceUpdateCanvases();
            check(hud.stars[0].color==Color.white && hud.stars[1].color==Color.white && hud.stars[2].color!=Color.white,"Two mistakes fill exactly two stars");
            check(hud.reasons.text.Contains("бег") && hud.reasons.text.Contains("прыжок"),"Debug text retains each violation reason");
            CardboardFactorySceneBuilder.Capture("TwoMistakeStars");
            game.StartInspection();game.CompleteInspection(false);
            hud.Refresh();
            check(game.Mistakes==3 && game.State==FactoryState.Chased && game.CaptureActive && !game.CameraOverride && !game.PlayerBlocked,"Third cumulative mistake starts pursuit with normal first-person controls");
            check(hud.stars[2].color==Color.white && hud.reasons.text.Contains("проверку"),"Third star and inspection failure remain visible during death");
            check(!game.RegisterMistake("extra") && game.Mistakes==3 && game.MistakeReasons.Count==3,"Post-capture callbacks cannot add a fourth mistake");
            game.ResetShift();hud.Refresh();
            check(game.Mistakes==0 && hud.reasons.text=="" && !game.Ended,"Only a new shift clears the cumulative counter and HUD");
            game.TickShiftClock(241);game.TickShiftClock(241);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("смены"),"Missed deadline awards one star instead of repeating every frame");
            game.ResetShift();game.StartInspection();game.CompleteInspection(false);game.CompleteInspection(false);
            check(game.Mistakes==1,"One failed inspection cannot be charged twice");
            game.ResetShift();game.ObserveConduct(true,6,0,false,.8f);game.ObserveConduct(true,0,0,false,.6f);game.ObserveConduct(true,6,0,false,.8f);
            check(game.Mistakes==2 && !game.Ended,"Separate observed running episodes are separate mistakes");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));game.TryInstall(game.gears[0]);
            check(game.Mistakes==3 && game.CaptureActive && game.LastMistakeReason.Contains("без обработки"),"Running errors and raw-part installation add to the same fatal total");
            game.ResetShift();game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));game.BeginDip();game.FinishDip(2);game.TryInstall(game.gears[0]);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("неверной жидкости"),"Wrong finishing bath is explained in star debug text");
            game.ResetShift();game.ReceiveWorkbenchPart(FactoryRecipe.ForType(2));game.TryInstall(game.gears[0]);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("тип"),"Wrong gear type awards one explained star");
            game.ResetShift();game.ReceiveWorkbenchPart(FactoryRecipe.ForType(2));game.TryInstall(game.gears[1]);
            check(game.Mistakes==1 && game.LastMistakeReason.Contains("текущего заказа"),"Installing outside the punchcard order awards one star");
            game.ResetShift();
        }
    }
}
