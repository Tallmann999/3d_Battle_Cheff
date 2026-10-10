using System;
using UnityEngine;

namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Dishware Definition")]
    public sealed class DishwareDefinition : ScriptableObject
    {
        public string Id, DisplayName;
        [Min(1)] public int NominalCapacity=6;
        [Min(.1f)] public float Diameter=.58f;
        [Min(.01f)] public float Depth=.03f;
        public bool SupportsLiquid;
        public Color Color=Color.white;
        public string Validate()=>string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName) || NominalCapacity<1 || !Positive(Diameter) || !Positive(Depth)?"Некорректный профиль посуды.":null;
        private static bool Positive(float n)=>n>0 && !float.IsNaN(n) && !float.IsInfinity(n);
        public DishwareSnapshot Capture()
        {var error=Validate();if(error!=null)throw new InvalidOperationException(error);return new DishwareSnapshot(Id,DisplayName,NominalCapacity,Diameter,Depth,SupportsLiquid,Color);}
    }
    public sealed class DishwareSnapshot
    {
        public string Id {get;} public string DisplayName {get;}
        public int NominalCapacity {get;} public float Diameter {get;} public float Depth {get;}
        public bool SupportsLiquid {get;} public Color Color {get;}
        public DishwareSnapshot(string id,string name,int capacity,float diameter,float depth,bool liquid,Color color)
        {Id=id;DisplayName=name;NominalCapacity=capacity;Diameter=diameter;Depth=depth;SupportsLiquid=liquid;Color=color;}
        public static DishwareSnapshot Default=>new DishwareSnapshot("small_flat","Маленькая тарелка",6,.58f,.03f,false,new Color(.86f,.89f,.95f));
    }
}
