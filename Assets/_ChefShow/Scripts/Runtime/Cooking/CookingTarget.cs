using UnityEngine;
namespace ChefShow.Cooking
{
    public sealed class CookingTarget : MonoBehaviour
    {
        public CookingStation Station;
        public CookingTargetKind Kind;
        public int Index;
    }
}
