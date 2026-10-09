using UnityEngine;
using UnityEngine.UI;
namespace ChefShow.UI
{
    public sealed class HandPromptGraphic : MaskableGraphic
    {
        public bool RightHand;
        public bool Taking;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float sign=RightHand?-1:1;
            Add(vh,sign,-8,-9,8,5); Add(vh,sign,-5,-15,5,-8);
            for(int i=0;i<4;i++) {float x=-7+i*4;Add(vh,sign,x,3,x+3,Taking?10+(i==1?3:0):8);}
            Add(vh,sign,-12,-3,-7,3);
        }
        private void Add(VertexHelper vh,float sign,float x0,float y0,float x1,float y1)
        {
            int n=vh.currentVertCount;var c=color;
            vh.AddVert(new Vector3(x0*sign,y0),c,Vector2.zero);vh.AddVert(new Vector3(x0*sign,y1),c,Vector2.zero);
            vh.AddVert(new Vector3(x1*sign,y1),c,Vector2.zero);vh.AddVert(new Vector3(x1*sign,y0),c,Vector2.zero);
            if(sign>0){vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
            else{vh.AddTriangle(n,n+2,n+1);vh.AddTriangle(n,n+3,n+2);}
        }
    }
}
