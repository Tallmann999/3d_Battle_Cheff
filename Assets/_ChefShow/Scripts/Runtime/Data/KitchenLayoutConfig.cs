using UnityEngine;
namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Kitchen Layout Config")]
    public sealed class KitchenLayoutConfig : ScriptableObject
    {
        [Min(26)] public float ArenaWidth=37.4f;
        [Min(36)] public float ArenaDepth=53.1f;
        public float ArenaCenterZ=-3.25f;
        [Min(1)] public float OvenScale=2;
        [Min(.1f)] public float OvenWallClearance=.25f;
        [Min(10)] public float DishwareTableX=11.65f;
        public float DishwareTableDepth=.8f;
        public float DishwareTableHeight=.85f;
        [Min(1.5f)] public float DishwareShelfWidth=1.65f;
        [Min(.7f)] public float DishwareShelfDepth=.9f;
        [Min(.4f)] public float DishwareShelfFirstLevel=.55f;
        [Min(.4f)] public float DishwareShelfLevelStep=.55f;
        public float DishwareShelfOvenOffset=1.9f;
    }
}
