using System.Collections;
using System.Linq;
using ChefShow.Cooking;
using ChefShow.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private IEnumerator RecognitionTool(int index)
        {
            var drawer=bootstrap.Tools.Drawers.Single(d=>d.StationId=="A1");
            if(!drawer.IsOpen){yield return AimDrawer(drawer);yield return HandClick();yield return new WaitForSecondsRealtime(.4f);}
            yield return AimTool(Tool(drawer,index));yield return HandClick(true);
        }
        private IEnumerator RecognitionMix()
        {
            yield return RecognitionTool(2);var bowl=bootstrap.Mixing.Stations.Single(s=>s.StationId=="A1");yield return Aim(bowl.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
            float deadline=Time.realtimeSinceStartup+5;while(State.Bowl.Count>1&&Time.realtimeSinceStartup<deadline)yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;Assert.That(State.Bowl.Count,Is.EqualTo(1));
            yield return Aim(bowl.Food[0].Visual.bounds.center);yield return HandClick();Assert.That(State.Held,Is.Not.Null);
        }
        private IEnumerator RecognitionReady(FoodPortion food)
        {
            bootstrap.Run.Clock.SetSpeed(20);float deadline=Time.realtimeSinceStartup+5;
            while(food.Cooking!=CookState.Cooked&&Time.realtimeSinceStartup<deadline)yield return null;
            bootstrap.Run.Clock.SetSpeed(1);Assert.That(food.Cooking,Is.EqualTo(CookState.Cooked));
        }
        private void AssertRecognition(string id)
        {
            Assert.That(State.CaptureDish().Recognition.RecipeId,Is.EqualTo(id));Assert.That(bootstrap.Recipes.Current.RecipeId,Is.EqualTo(id));
            Canvas.ForceUpdateCanvases();Assert.That(bootstrap.Recipes.Label.preferredHeight,Is.LessThanOrEqualTo(bootstrap.Recipes.Label.rectTransform.rect.height),bootstrap.Recipes.Label.text+" clipped");
        }
        [UnityTest] public IEnumerator MouseRecognitionSteakKeepsBookTaskSeparateAndSubmitResetSnapshot()
        {
            yield return MouseTrip("beef");yield return Aim(TrayPoint(0));yield return HandClick();var beef=State.Held;yield return Aim(Socket(1));yield return HandClick();
            AssertRecognition("experimental");Assert.That(bootstrap.Recipes.Label.text,Does.Contain("Нужен нагрев"));Capture("recognition-raw-beef.png");
            yield return Aim(ServingFoodPoint());yield return HandClick();var pan=Appliance(CookerKind.Pan);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();yield return HeatTwice(pan);yield return RecognitionReady(beef);
            yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();AssertRecognition("beef_steak");Capture("recognition-steak.png");
            string task=bootstrap.Hud.TaskCard.text;yield return Press(Key.I);Assert.That(bootstrap.Recipes.Panel.activeInHierarchy,Is.False);
            yield return BookClick(bootstrap.RecipeBook.Choose);Assert.That(bootstrap.RecipeBook.SelectedRecipeId,Is.EqualTo("fried_potatoes"));yield return Press(Key.I);
            AssertRecognition("beef_steak");Assert.That(bootstrap.Hud.TaskCard.text,Is.EqualTo(task));Assert.That(bootstrap.Recipes.Panel.activeInHierarchy,Is.True);
            var station=bootstrap.Serving.Stations.Single(s=>s.StationId=="A1");yield return Aim(station.transform.Find("Plate Contents/Submit Dish").position);yield return HandClick();var snapshot=State.SubmittedDish;
            Assert.That(snapshot.Recognition.RecipeId,Is.EqualTo("beef_steak"));Assert.That(bootstrap.Recipes.Label.text,Does.StartWith("Подано: "));
            yield return Aim(station.transform.Find("Plate Contents/Reset Submission").position);yield return HandClick();yield return Aim(ServingFoodPoint());yield return HandClick();AssertRecognition("no_dish");
            Assert.That(snapshot.Recognition.RecipeId,Is.EqualTo("beef_steak"));yield return Aim(Socket(1));yield return HandClick();bootstrap.Run.SetRemaining(.01f);yield return null;yield return null;
            Assert.That(State.SubmittedDish.Recognition.RecipeId,Is.EqualTo("beef_steak"));AssertRecognition("beef_steak");
        }
        [UnityTest] public IEnumerator MouseRecognitionRealMixturePanBecomesCheeseOmelet()
        {
            yield return MouseTrip("egg","cheese");var bowl=bootstrap.Mixing.Stations.Single(s=>s.StationId=="A1");
            for(int i=0;i<2;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(bowl.transform.position);yield return HandClick();}
            yield return RecognitionMix();var mixed=State.Held;var pan=Appliance(CookerKind.Pan);yield return Aim(pan.transform.position+Vector3.up*.025f);yield return HandClick();yield return HeatTwice(pan);yield return RecognitionReady(mixed);
            yield return Aim(CookFoodPoint(pan,0));yield return HandClick();yield return Aim(Socket(1));yield return HandClick();AssertRecognition("cheese_omelet");
            Assert.That(State.CaptureDish().Recognition.Ingredients.Count,Is.EqualTo(2));Capture("recognition-omelet.png");
        }
        [UnityTest] public IEnumerator MouseRecognitionChoppedAppleRealMixtureOvenBecomesTart()
        {
            yield return MouseTrip("apple","flour","egg","butter","sugar");yield return Aim(TrayPoint(0));yield return HandClick();var apple=State.Held;yield return Aim(Socket(0));yield return HandClick();
            yield return RecognitionTool(0);yield return Aim(Socket(0));for(int i=0;i<6;i++)yield return HandClick(true);Assert.That(apple.Preparation,Is.EqualTo(PreparationState.Chopped));yield return HandClick();
            var bowl=bootstrap.Mixing.Stations.Single(s=>s.StationId=="A1");yield return Aim(bowl.transform.position);yield return HandClick();
            for(int i=0;i<4;i++){yield return Aim(TrayPoint(0));yield return HandClick();yield return Aim(bowl.transform.position);yield return HandClick();}
            yield return RecognitionMix();var mixture=State.Held;var oven=Appliance(CookerKind.Oven);
            Teleport(new Vector3(oven.transform.position.x+2.16f,.05f,-6.25f));yield return Aim(oven.transform.Find("Bake Form").position);yield return HandClick();yield return HeatTwice(oven);
            yield return Aim(oven.transform.Find("Door Handle").position);yield return HandClick();yield return RecognitionReady(mixture);yield return HandClick();yield return Aim(oven.Food[0].Visual.bounds.center);yield return HandClick();
            Teleport(new Vector3(-9.7f,.05f,-6.25f));yield return Aim(Socket(1));yield return HandClick();AssertRecognition("apple_tart");
            Assert.That(State.CaptureDish().Recognition.Ingredients.Sum(x=>x.Amount),Is.EqualTo(5));Capture("recognition-apple-tart.png");
        }
    }
}
