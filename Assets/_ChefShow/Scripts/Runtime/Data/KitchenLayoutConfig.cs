using UnityEngine;
namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Kitchen Layout Config")]
    public sealed class KitchenLayoutConfig : ScriptableObject
    {
        [Min(26)] public float ArenaWidth=28;
        [Min(36)] public float ArenaDepth=38;
        [Min(1)] public float OvenScale=2;
        [Min(.1f)] public float OvenWallClearance=.25f;
        [Min(10)] public float DishwareTableX=11.65f;
        public float DishwareTableDepth=.8f;
        public float DishwareTableHeight=.85f;
    }
}
