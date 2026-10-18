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
            check(game.Mistakes==.5f && !game.Ended && game.LastMistakeReason.Contains("бег"),"Observed running awards half a star with its reason");
            hud.Refresh();Canvas.ForceUpdateCanvases();
            check(hud.fills[0].fillAmount==.5f && hud.fills[1].fillAmount==0 && hud.fills[2].fillAmount==0,"Half-star score visibly fills exactly half of the first star");
            check(hud.fills[0].type==UnityEngine.UI.Image.Type.Filled && hud.fills[0].fillMethod==UnityEngine.UI.Image.FillMethod.Horizontal,"Half-star uses a horizontal fill");
            check(Mathf.Abs(game.Suspicion-100f/6)<.001f && game.Disguise==87.5f,"Suspicion and disguise track fractional stars");
            for(int i=0;i<180;i++) game.ObserveConduct(true,6,0,false,.016f);
            check(game.Mistakes==.5f,"Continuous running does not award stars each frame");
            game.ObserveConduct(false,0,0,false,90);
            check(game.Mistakes==.5f,"Time out of sight never removes a half-star");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1));game.TryInstall(game.gears[0]);
            check(game.Mistakes==.5f && game.Repairs==1,"Successful repair never removes a half-star");
            game.ObserveConduct(true,0,.6f,false,.1f);
            for(int i=0;i<20;i++) game.ObserveConduct(true,0,.6f,false,.016f);
            check(game.Mistakes==1 && !game.Ended && game.LastMistakeReason.Contains("прыжок"),"One off-pump jump adds half a star without charging every airborne frame");
            hud.Refresh();Canvas.ForceUpdateCanvases();
            check(hud.fills[0].fillAmount==1 && hud.fills[1].fillAmount==0 && hud.fills[2].fillAmount==0,"Two half-star violations fill exactly one star");
            check(hud.reasons.text.Contains("бег") && hud.reasons.text.Contains("прыжок"),"Debug text retains each violation reason");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));game.TryInstall(game.gears[1]);
            game.ObserveConduct(true,0,0,false,.1f);game.ObserveConduct(true,0,.6f,false,.1f);
            hud.Refresh();
            check(game.Mistakes==2.5f && !game.CaptureActive && hud.fills[0].fillAmount==1 && hud.fills[1].fillAmount==1 && hud.fills[2].fillAmount==.5f,"Mixed violations show two and a half stars without starting pursuit");
            CardboardFactorySceneBuilder.Capture("TwoAndHalfMistakeStars");
            game.ObserveConduct(true,0,0,false,.1f);game.ObserveConduct(true,0,.6f,false,.1f);
            hud.Refresh();
            check(game.Mistakes==3 && game.State==FactoryState.Chased && game.CaptureActive && !game.CameraOverride && !game.PlayerBlocked,"Third cumulative mistake starts pursuit with normal first-person controls");
            check(hud.fills[2].fillAmount==1 && hud.reasons.text.Contains("прыжок"),"Third star and violation reasons remain visible during death");
            check(!game.RegisterMistake("extra") && game.Mistakes==3 && game.MistakeReasons.Count==5,"Post-capture callbacks cannot add further mistakes");
            game.ResetShift();hud.Refresh();
            check(game.Mistakes==0 && hud.reasons.text=="" && !game.Ended && hud.fills[0].fillAmount==0 && hud.fills[1].fillAmount==0 && hud.fills[2].fillAmount==0,"Only a new shift clears the cumulative counter and all star fills");
            game.TickShiftClock(241);game.TickShiftClock(241);
            check(game.ShiftRemaining==0 && game.Mistakes==0 && game.MistakeReasons.Count==0 && !game.CaptureActive,"Expired shift clock never awards a star or starts pursuit");
            game.ResetShift();game.ObserveConduct(true,6,0,false,.8f);game.ObserveConduct(true,0,0,false,.6f);game.ObserveConduct(true,6,0,false,.8f);
            check(game.Mistakes==1 && !game.Ended,"Separate observed running episodes each award half a star");
            game.ReceiveWorkbenchPart(FactoryRecipe.ForType(1,false));game.TryInstall(game.gears[0]);
            check(game.Mistakes==2 && !game.CaptureActive && game.LastMistakeReason.Contains("без обработки"),"Raw-part installation still adds one full star to running half-stars");
            game.TryInstall(game.gears[0]);
            check(game.Mistakes==3 && game.CaptureActive,"Repeated invalid installation reaches the same three-star limit");
            game.ResetShift();
            for(int i=0;i<6;i++)
            {
                game.ObserveConduct(true,6,0,false,.8f);
                check(game.Mistakes==(i+1)*.5f && game.CaptureActive==(i==5),"Six separate running violations trigger pursuit only on the sixth: "+(i+1));
                game.ObserveConduct(false,0,0,false,.6f);
            }
            game.ResetShift();game.RegisterMistake("Ползвезды",.5f);game.RegisterMistake("Целая звезда");game.RegisterMistake("Целая звезда");game.RegisterMistake("Целая звезда");
            hud.Refresh();
            check(game.Mistakes==3 && game.Suspicion==100 && game.CaptureActive && hud.fills[2].fillAmount==1,"A full-star violation crossing the threshold clamps the score to three stars");
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
