using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChefShow.Data
{
    [CreateAssetMenu(menuName="Chef Show/Dishware Config")]
    public sealed class DishwareConfig : ScriptableObject
    {
        public DishwareDefinition[] Types;
        public string StartingId="small_flat";
        public string Validate()=>Types==null || Types.Length!=5 || Types.Any(t=>t==null || t.Validate()!=null) || Types.Select(t=>t.Id).Distinct().Count()!=5 || !Types.Any(t=>t.Id==StartingId)?"Нужны пять разных профилей посуды и начальная тарелка.":null;
        public DishwareSettings Capture(){var e=Validate();if(e!=null)throw new InvalidOperationException(e);return new DishwareSettings(Types.Select(t=>t.Capture()),StartingId);}
    }
    public sealed class DishwareSettings
    {
        public IReadOnlyList<DishwareSnapshot> Types {get;}
        public DishwareSnapshot Starting {get;}
        public DishwareSettings(IEnumerable<DishwareSnapshot> types,string starting)
        {Types=Array.AsReadOnly(types.ToArray());Starting=Types.Single(t=>t.Id==starting);}
    }
}
