using ChefShow.Data;
using UnityEngine;
namespace ChefShow.Inventory
{
    public sealed class DishwareView : MonoBehaviour
    {
        public Renderer Base;
        public Renderer[] Rim;
        private MaterialPropertyBlock block;
        public void Present(DishwareSnapshot profile)
        {
            bool visible=profile!=null;Base.enabled=visible;foreach(var part in Rim)part.enabled=visible;
            if(!visible)return;
            Base.transform.localPosition=Vector3.zero;
            Base.transform.localScale=new Vector3(profile.Diameter,.035f,profile.Diameter);
            block=block??new MaterialPropertyBlock();block.SetColor("_BaseColor",profile.Color);block.SetColor("_Color",profile.Color);Base.SetPropertyBlock(block);
            for(int i=0;i<Rim.Length;i++)
            {
                float angle=i*Mathf.PI*2/Rim.Length;
                var t=Rim[i].transform;t.localPosition=new Vector3(Mathf.Cos(angle)*profile.Diameter*.46f,profile.Depth*.5f,Mathf.Sin(angle)*profile.Diameter*.46f);
                t.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                t.localScale=new Vector3(profile.Diameter*.055f,profile.Depth,profile.Diameter*.25f);Rim[i].SetPropertyBlock(block);
            }
        }
    }
}
