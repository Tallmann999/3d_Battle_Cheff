using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace ChefShow.Tests
{
    public sealed partial class InventoryPlayTests
    {
        private IEnumerator BookClick(Button button)
        {
            Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();
            var point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,button.GetComponent<RectTransform>().position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
        }
        [UnityTest] public IEnumerator RecipeBookIBlocksMovementLookAndBothHandsWhileTimerContinues()
        {
            yield return Aim(TrayPoint(0),false);var book=bootstrap.RecipeBook;Assert.That(book,Is.Not.Null);
            var position=bootstrap.Player.transform.position;var rotation=bootstrap.Player.ViewCamera.transform.rotation;int version=State.Version;float before=bootstrap.Run.RemainingSeconds;
            yield return Press(Key.I);Assert.That(book.IsOpen,Is.True);Assert.That(Cursor.visible,Is.True);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            Assert.That(bootstrap.UiInput.actionsAsset.FindActionMap("Gameplay").enabled,Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.F,Key.Tab,Key.G,Key.Q));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(2,2),delta=new Vector2(70,-90)}.WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Vector3.Distance(position,bootstrap.Player.transform.position),Is.LessThan(.001f));Assert.That(Quaternion.Angle(rotation,bootstrap.Player.ViewCamera.transform.rotation),Is.LessThan(.01f));
            Assert.That(State.Version,Is.EqualTo(version));Assert.That(bootstrap.Run.RemainingSeconds,Is.LessThan(before));Assert.That(bootstrap.Hud.InteractionKey.enabled,Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
            yield return Press(Key.I);Assert.That(book.IsOpen,Is.False);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.Locked));Assert.That(Cursor.visible,Is.False);
            Assert.That(bootstrap.UiInput.actionsAsset.FindActionMap("Gameplay").enabled,Is.True);
        }
        [UnityTest] public IEnumerator RecipeBookAllTwelvePagesUseMouseArrowsAndSelectionPersistsAndReplaces()
        {
            var book=bootstrap.RecipeBook;string task=bootstrap.Hud.TaskCard.text;string runId=bootstrap.Run.RunId;var titles=new HashSet<string>();
            yield return Press(Key.I);
            for(int i=0;i<12;i++)
            {
                Assert.That(book.PageIndex,Is.EqualTo(i));Assert.That(book.Title.text,Is.EqualTo(book.Catalog.Recipes[i].DisplayName));
                Assert.That(book.StepIcons.Take(book.CurrentPage.Steps.Count+1).All(x=>x.gameObject.activeInHierarchy),Is.True);
                Canvas.ForceUpdateCanvases();
                Assert.That(book.StepIcons.Where(x=>x.gameObject.activeInHierarchy).All(x=>x.GetComponent<CanvasRenderer>()!=null),Is.True);
                foreach(var caption in book.IngredientCaptions.Where(x=>x.gameObject.activeInHierarchy))
                    Assert.That(caption.preferredHeight,Is.LessThanOrEqualTo(caption.rectTransform.rect.height),caption.text+" is clipped");
                titles.Add(book.Title.text);Capture("recipe-page-"+(i+1).ToString("00")+".png");
                if(i==0){yield return BookClick(book.Choose);Assert.That(book.SelectedRecipeId,Is.EqualTo("fried_potatoes"));Assert.That(book.IngredientList.activeSelf,Is.True);Assert.That(book.SelectedIngredients.text,Does.Contain("×2"));}
                yield return BookClick(book.Next);
            }
            Assert.That(titles.Count,Is.EqualTo(12));Assert.That(book.PageIndex,Is.Zero);
            yield return BookClick(book.Previous);Assert.That(book.PageIndex,Is.EqualTo(11));yield return BookClick(book.Next);yield return BookClick(book.Next);
            yield return BookClick(book.Choose);Assert.That(book.SelectedRecipeId,Is.EqualTo("boiled_potatoes"));Assert.That(book.SelectedIngredients.text,Does.Contain("Сливочное масло"));Assert.That(book.SelectedIngredients.text,Does.Not.Contain("Лук"));
            Assert.That(bootstrap.Hud.TaskCard.text,Is.EqualTo(task));Assert.That(bootstrap.Run.RunId,Is.EqualTo(runId));
            string list=book.SelectedIngredients.text;yield return Press(Key.I);
            Assert.That(book.IngredientList.activeInHierarchy,Is.True);Assert.That(book.SelectedIngredients.text,Is.EqualTo(list));Capture("recipe-selected-list.png");
            yield return Press(Key.I);bootstrap.SetPaused(true);yield return null;Assert.That(book.IsOpen,Is.False);yield return Press(Key.I);Assert.That(book.IsOpen,Is.False);
            bootstrap.SetPaused(false);yield return Press(Key.I);Assert.That(book.IsOpen,Is.True);bootstrap.RestartShow();yield return null;
            Assert.That(book.IsOpen,Is.False);Assert.That(book.SelectedRecipeId,Is.Null);Assert.That(book.IngredientList.activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator RecipeBookCloseDoesNotLeakHeldMouseClickIntoCooking()
        {
            yield return MouseTrip("beef");yield return Aim(TrayPoint(0));var book=bootstrap.RecipeBook;yield return Press(Key.I);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(2,2)}.WithButton(MouseButton.Left).WithButton(MouseButton.Right));yield return null;yield return null;
            yield return Press(Key.I);Assert.That(book.IsOpen,Is.False);Assert.That(State.Held,Is.Null);
            Assert.That(bootstrap.UiInput.actionsAsset.FindActionMap("Gameplay").enabled,Is.False);
            yield return new WaitForSecondsRealtime(.1f);Assert.That(State.Held,Is.Null);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;yield return null;
            Assert.That(bootstrap.UiInput.actionsAsset.FindActionMap("Gameplay").enabled,Is.True);Assert.That(State.Held,Is.Null);
            yield return Aim(TrayPoint(0));yield return HandClick();Assert.That(State.Held.Ingredient.Id,Is.EqualTo("beef"));
        }
    }
}
