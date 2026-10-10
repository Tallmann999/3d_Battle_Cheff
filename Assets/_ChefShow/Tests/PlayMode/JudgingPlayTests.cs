using System.Collections;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using ChefShow.Judging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private IEnumerator JudgingSubmit()
        {
            var station=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");yield return Aim(station.transform.Find("Plate Contents/Submit Dish").position);yield return HandClick();
            Assert.That(bootstrap.Judging.Current,Is.Not.Null);Assert.That(bootstrap.Judging.JudgedDish,Is.SameAs(State.SubmittedDish));
        }
        private IEnumerator JudgingReset()
        {
            var station=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");yield return Aim(station.transform.Find("Plate Contents/Reset Submission").position);yield return HandClick();
            Assert.That(bootstrap.Judging.Current,Is.Null);Assert.That(bootstrap.Judging.Panel.activeInHierarchy,Is.False);
        }
        private void JudgingTextFits()
        {
            Canvas.ForceUpdateCanvases();foreach(var text in new[]{bootstrap.Judging.Total,bootstrap.Judging.Breakdown,bootstrap.Judging.Reasons})
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height),text.text+" clipped");
        }
        [UnityTest] public IEnumerator MouseJudgingRawCookedSaltAndQuantityUseSubmittedSnapshots()
        {
            yield return MouseTrip("beef","salt","beef","beef","beef");yield return Aim(TrayPoint(0));yield return HandClick();var beef=State.Held;yield return Aim(Socket(1));yield return HandClick();yield return JudgingSubmit();
            var raw=bootstrap.Judging.Current;var rawDish=State.SubmittedDish;Assert.That(raw.UnroundedTotal,Is.LessThanOrEqualTo(45));JudgingTextFits();Capture("judging-raw-cap.png");
            yield return JudgingReset();yield return Aim(ServingFoodPoint());yield return HandClick();var pan=Appliance(CookerKind.Pan);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();yield return HeatTwice(pan);yield return RecognitionReady(beef);
            yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();yield return JudgingSubmit();
            var cooked=bootstrap.Judging.Current;Assert.That(cooked.UnroundedTotal,Is.GreaterThan(raw.UnroundedTotal));Assert.That(cooked.RecipeId,Is.EqualTo("beef_steak"));
            Assert.That(rawDish.Recognition.RecipeId,Is.EqualTo("experimental"));var exactCooked=cooked.UnroundedTotal;JudgingTextFits();Capture("judging-good-steak.png");
            string task=bootstrap.Hud.TaskCard.text;yield return Press(Key.I);Assert.That(bootstrap.Judging.Panel.activeInHierarchy,Is.False);yield return BookClick(bootstrap.RecipeBook.Choose);yield return Press(Key.I);
            Assert.That(bootstrap.Judging.Current,Is.SameAs(cooked));Assert.That(bootstrap.Hud.TaskCard.text,Is.EqualTo(task));Assert.That(bootstrap.Judging.Panel.activeInHierarchy,Is.True);Capture("judging-with-recipe-list.png");
            bootstrap.SetPaused(true);yield return null;Assert.That(bootstrap.Judging.Panel.activeInHierarchy,Is.False);bootstrap.SetPaused(false);yield return null;
            yield return JudgingReset();yield return Aim(TrayPoint(0));yield return HandClick();for(int i=0;i<8;i++){yield return Aim(Socket(1));yield return HandClick();}yield return Press(Key.Backspace);yield return JudgingSubmit();
            var salty=bootstrap.Judging.Current;Assert.That(salty.UnroundedTotal,Is.LessThan(exactCooked));Assert.That(salty.Categories[1].Points,Is.LessThan(cooked.Categories[1].Points));Assert.That(salty.Categories[4].Points,Is.EqualTo(cooked.Categories[4].Points));
            Assert.That(salty.RecipeId,Is.EqualTo("beef_steak"));Assert.That(cooked.UnroundedTotal,Is.EqualTo(exactCooked));JudgingTextFits();Capture("judging-excess-salt.png");
            yield return JudgingReset();var additions=new System.Collections.Generic.List<FoodPortion>();
            for(int i=0;i<3;i++){yield return Aim(TrayPoint(1));yield return HandClick();additions.Add(State.Held);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();}
            yield return RecognitionReady(additions.Last());
            for(int i=0;i<3;i++){yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();}
            yield return JudgingSubmit();var heap=bootstrap.Judging.Current;Assert.That(heap.RecipeId,Is.EqualTo("beef_steak"));Assert.That(heap.Categories[0].Points,Is.EqualTo(salty.Categories[0].Points));Assert.That(heap.Categories[4].Points,Is.LessThan(salty.Categories[4].Points));JudgingTextFits();Capture("judging-excess-quantity.png");
            bootstrap.RestartShow();yield return null;Assert.That(bootstrap.Judging.Current,Is.Null);Assert.That(bootstrap.Judging.Panel.activeInHierarchy,Is.False);Assert.That(raw.UnroundedTotal,Is.LessThanOrEqualTo(45));
        }
        [UnityTest] public IEnumerator MouseJudgingEmptyAndAutomaticMixtureSubmissionAreFrozen()
        {
            int facts=0;ScoreBreakdown last=null;var subscription=bootstrap.Run.Events.Subscribe<DishJudged>(fact=>{facts++;last=fact.Score;});
            yield return JudgingSubmit();Assert.That(bootstrap.Judging.Current.UnroundedTotal,Is.Zero);Assert.That(facts,Is.EqualTo(1));yield return null;yield return null;Assert.That(facts,Is.EqualTo(1));JudgingTextFits();Capture("judging-empty.png");subscription.Dispose();
            bootstrap.RestartShow();yield return null;yield return MouseTrip("egg","cheese");var bowl=bootstrap.Mixing.Stations.Single(s=>s.StationId=="A1");
            for(int i=0;i<2;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(bowl.transform.position);yield return HandClick();}
            yield return RecognitionMix();var mixed=State.Held;var pan=Appliance(CookerKind.Pan);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();yield return HeatTwice(pan);yield return RecognitionReady(mixed);
            yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();Assert.That(bootstrap.Judging.Current,Is.Null);
            facts=0;subscription=bootstrap.Run.Events.Subscribe<DishJudged>(fact=>{facts++;last=fact.Score;});bootstrap.Run.SetRemaining(.01f);yield return null;yield return null;
            Assert.That(bootstrap.Judging.Current.RecipeId,Is.EqualTo("cheese_omelet"));Assert.That(bootstrap.Judging.Current.UnroundedTotal,Is.GreaterThan(45));Assert.That(facts,Is.EqualTo(1));Assert.That(last,Is.SameAs(bootstrap.Judging.Current));
            var frozen=bootstrap.Judging.Current;yield return null;yield return null;Assert.That(bootstrap.Judging.Current,Is.SameAs(frozen));Assert.That(facts,Is.EqualTo(1));JudgingTextFits();Capture("judging-auto-omelet.png");subscription.Dispose();
        }
    }
}
