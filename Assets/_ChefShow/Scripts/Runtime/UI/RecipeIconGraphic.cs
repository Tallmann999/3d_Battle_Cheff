using ChefShow.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ChefShow.UI
{
    // Saved vector illustrations. No texture allocation or runtime scene construction.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecipeIconGraphic : MaskableGraphic
    {
        public RecipeSymbol Symbol;
        public string DishId;
        public bool LeftArrow;
        private VertexHelper mesh;
        private static readonly Color Ink=new Color(.20f,.16f,.12f), Cream=new Color(1,.94f,.78f), Gold=new Color(.96f,.65f,.18f), Green=new Color(.30f,.51f,.22f);
        public void Show(RecipeSymbol symbol,string dish=null){Symbol=symbol;DishId=dish;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();mesh=vh;
            switch(Symbol)
            {
                case RecipeSymbol.Potato:
                    Oval(0,0,36,26,Ink);Oval(0,0,33,23,new Color(.77f,.57f,.32f));Dot(-17,3,3,Ink);Dot(8,13,2,Ink);Dot(17,-6,3,Ink);Dot(-1,-10,2,Ink);break;
                case RecipeSymbol.Onion:
                    Oval(0,-4,28,31,Ink);Oval(0,-4,25,28,Cream);Oval(0,-4,15,22,Gold);Oval(0,-4,12,19,Cream);Line(0,23,7,40,4,Green);Line(2,26,-6,38,3,Green);Line(-8,-32,0,-40,2,Ink);break;
                case RecipeSymbol.Beef: Steak(0,0,1);break;
                case RecipeSymbol.Carrot:
                    Poly(new[]{new Vector2(-25,21),new Vector2(17,26),new Vector2(8,-38)},Ink);
                    Poly(new[]{new Vector2(-21,18),new Vector2(13,22),new Vector2(7,-31)},new Color(.97f,.43f,.11f));
                    for(int i=0;i<3;i++)Line(-14+i*5,11-i*13,5+i*2,14-i*13,2,Ink);
                    Line(-4,23,-20,43,5,Green);Line(-4,23,0,45,5,Green);Line(-4,23,21,37,5,Green);break;
                case RecipeSymbol.Egg:
                    Oval(0,0,25,35,Ink);Oval(0,0,22,32,new Color(1,.96f,.87f));Oval(1,-8,13,14,Gold);break;
                case RecipeSymbol.Cheese:
                    Poly(new[]{new Vector2(-37,-23),new Vector2(30,-25),new Vector2(30,23),new Vector2(-37,5)},Ink);
                    Poly(new[]{new Vector2(-33,-20),new Vector2(27,-21),new Vector2(27,18),new Vector2(-33,3)},Gold);
                    Dot(-18,-8,5,Ink);Dot(6,3,6,Ink);Dot(19,-12,4,Ink);Line(-33,3,27,-3,2,Ink);break;
                case RecipeSymbol.Flour: Bag(false);break;
                case RecipeSymbol.Sugar: Bag(true);break;
                case RecipeSymbol.Butter:
                    Box(-35,-21,32,20,Ink);Box(-32,-18,29,17,new Color(1,.84f,.32f));Box(-25,13,25,22,Cream);Line(0,-16,0,17,2,Gold);break;
                case RecipeSymbol.Apple:
                    Oval(-13,-3,22,29,Ink);Oval(13,-3,22,29,Ink);Oval(-12,-3,19,26,new Color(.83f,.25f,.16f));Oval(12,-3,19,26,new Color(.9f,.29f,.19f));Line(0,23,5,36,4,Ink);Oval(15,33,12,6,Green);Line(-21,8,-18,16,3,Cream);break;
                case RecipeSymbol.Salt:
                    Box(-20,-29,20,19,Ink);Box(-17,-26,17,16,new Color(.88f,.94f,.95f));Oval(0,21,20,8,Ink);Oval(0,22,17,5,Cream);
                    Dot(-8,22,2,Ink);Dot(0,24,2,Ink);Dot(8,22,2,Ink);Line(-10,-5,10,-5,2,new Color(.5f,.67f,.77f));break;
                case RecipeSymbol.Oil:
                    Box(-20,-30,20,15,Ink);Box(-17,-27,17,12,new Color(.57f,.63f,.26f));Box(-8,12,8,35,Ink);Box(-5,15,5,29,Gold);Box(-10,31,10,38,Ink);Box(-12,-10,12,6,Cream);Oval(0,-2,5,6,Gold);break;
                case RecipeSymbol.Knife:
                    Box(-42,-31,38,22,Ink);Box(-39,-28,35,19,new Color(.70f,.48f,.26f));
                    Poly(new[]{new Vector2(-24,-12),new Vector2(-21,0),new Vector2(19,31),new Vector2(23,21)},Ink);
                    Poly(new[]{new Vector2(-20,-7),new Vector2(-19,-1),new Vector2(18,26),new Vector2(19,22)},Cream);
                    Line(20,26,39,40,8,Ink);Line(-20,-21,-10,-12,3,Cream);Line(-9,-23,1,-14,3,Cream);Line(3,-24,13,-15,3,Cream);break;
                case RecipeSymbol.Mix:
                    Oval(0,-6,39,26,Ink);Oval(0,-5,35,22,new Color(.65f,.79f,.78f));Oval(0,7,38,13,Ink);Oval(0,8,34,9,Cream);
                    Line(10,3,29,39,7,Ink);Line(10,4,29,37,3,new Color(.65f,.40f,.21f));Line(-13,18,-27,26,3,Gold);Line(-27,26,-14,32,3,Gold);break;
                case RecipeSymbol.Pan: Pan();break;
                case RecipeSymbol.Pot: Pot(false);break;
                case RecipeSymbol.Braise: Pot(true);break;
                case RecipeSymbol.Oven:
                    Box(-39,-34,39,38,Ink);Box(-35,-30,35,34,new Color(.58f,.63f,.68f));Box(-28,-23,28,15,Ink);Box(-24,-19,24,11,new Color(.33f,.40f,.48f));Line(-28,22,28,22,4,Ink);Dot(-18,29,4,Ink);Dot(0,29,4,Ink);Dot(18,29,4,Ink);Oval(0,-5,16,9,Gold);Line(-18,-12,18,-12,3,Cream);break;
                case RecipeSymbol.Dish: Dish();break;
                case RecipeSymbol.Arrow:
                    float sign=LeftArrow?-1:1;Line(-32*sign,0,25*sign,0,7,color);
                    Poly(new[]{new Vector2(34*sign,0),new Vector2(13*sign,18),new Vector2(13*sign,-18)},color);break;
            }
        }
        private void Steak(float x,float y,float scale)
        {
            Oval(x,y,35*scale,26*scale,Ink);Oval(x,y,32*scale,23*scale,new Color(.60f,.26f,.18f));
            Oval(x-10*scale,y+6*scale,9*scale,7*scale,Cream);
            Line(x-3*scale,y-17*scale,x+16*scale,y+14*scale,3*scale,Cream);Line(x-15*scale,y-11*scale,x-1*scale,y+16*scale,2*scale,Cream);
        }
        private void Bag(bool sugar)
        {
            Poly(new[]{new Vector2(-27,-32),new Vector2(27,-32),new Vector2(23,29),new Vector2(-23,29)},Ink);
            Poly(new[]{new Vector2(-24,-29),new Vector2(24,-29),new Vector2(20,25),new Vector2(-20,25)},Cream);Box(-25,27,25,33,Ink);
            if(sugar){Box(-13,-13,2,2,Gold);Box(2,-5,17,10,new Color(1,.84f,.49f));}
            else {Line(0,-20,0,17,3,Gold);for(int i=0;i<4;i++){Line(0,i*7-10,12,i*7-3,4,Gold);Line(0,i*7-10,-12,i*7-3,4,Gold);}}
        }
        private void Pan()
        {
            Oval(-7,4,31,27,Ink);Oval(-7,4,26,22,new Color(.48f,.52f,.55f));Oval(-7,4,21,17,Cream);Oval(-6,3,9,9,Gold);Line(20,-7,44,-24,9,Ink);Flame(-12,-30);Flame(7,-30);
        }
        private void Pot(bool lid)
        {
            Box(-29,-21,29,17,Ink);Box(-25,-18,25,15,new Color(.68f,.80f,.86f));Line(-30,7,-42,7,8,Ink);Line(30,7,42,7,8,Ink);
            Oval(0,18,30,9,Ink);Oval(0,19,25,5,new Color(.47f,.71f,.85f));
            if(lid){Line(-30,26,30,26,5,Ink);Box(-8,27,8,32,Ink);}
            for(int x=-18;x<=18;x+=18){Line(x,33,x-4,40,2,Gold);Line(x-4,40,x,47,2,Gold);}
            Flame(-10,-30);Flame(10,-30);
        }
        private void Flame(float x,float y)
        {
            Poly(new[]{new Vector2(x-6,y-11),new Vector2(x+7,y-11),new Vector2(x+3,y+11),new Vector2(x-2,y+1)},new Color(.98f,.45f,.12f));
            Poly(new[]{new Vector2(x-3,y-9),new Vector2(x+4,y-9),new Vector2(x+1,y+3)},Gold);
        }
        private void Dish()
        {
            Oval(0,-5,44,32,Ink);Oval(0,-5,41,29,Cream);Oval(0,-5,33,23,new Color(.88f,.82f,.66f));Oval(0,-5,31,21,Cream);
            if(DishId=="fried_egg"){Oval(0,-1,25,18,Color.white);Dot(3,0,11,Gold);}
            else if(DishId=="cheese_omelet"||DishId=="scrambled_eggs"){Oval(0,-3,28,15,Gold);for(int i=0;i<4;i++)Line(-18+i*9,-8,-13+i*9,8,3,Cream);}
            else if(DishId=="apple_tart"){Oval(0,0,28,20,new Color(.68f,.36f,.12f));Oval(0,0,24,16,Gold);for(int i=0;i<5;i++)Oval(-16+i*8,0,3,12,new Color(.87f,.37f,.22f));}
            else if(DishId=="potato_pancakes"){Oval(-14,-7,15,10,Ink);Oval(-14,-6,13,9,Gold);Oval(14,-6,15,10,Ink);Oval(14,-5,13,9,Gold);Oval(0,11,15,10,Ink);Oval(0,12,13,9,Gold);}
            else if(DishId=="beef_steak"||DishId=="steak_vegetables"||DishId=="braised_beef_onion"){Steak(-3,1,.65f);if(DishId!="beef_steak"){Dot(-21,-11,7,Gold);Dot(20,-12,7,Green);Dot(23,5,5,Gold);}}
            else {Color food=DishId=="buttered_carrots"?new Color(.96f,.44f,.14f):DishId=="caramel_apples"?new Color(.72f,.33f,.10f):Gold;
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Oval(Mathf.Cos(a)*16,Mathf.Sin(a)*10,9,7,food);}}
            Line(-24,24,-10,28,3,Green);Line(-18,21,-12,31,3,Green);
        }
        private Vector3 Point(Vector2 p){var r=rectTransform.rect;return new Vector3(r.center.x+p.x*r.width/100,r.center.y+p.y*r.height/100,0);}
        private void Poly(Vector2[] points,Color c)
        {
            int n=mesh.currentVertCount;foreach(var p in points)mesh.AddVert(Point(p),c,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)mesh.AddTriangle(n,n+i,n+i+1);
        }
        private void Box(float x0,float y0,float x1,float y1,Color c)=>Poly(new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)},c);
        private void Oval(float x,float y,float rx,float ry,Color c)
        {
            int n=mesh.currentVertCount;mesh.AddVert(Point(new Vector2(x,y)),c,Vector2.zero);
            const int sides=32;for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;mesh.AddVert(Point(new Vector2(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry)),c,Vector2.zero);}
            for(int i=0;i<sides;i++)mesh.AddTriangle(n,n+1+i,n+1+(i+1)%sides);
        }
        private void Dot(float x,float y,float r,Color c)=>Oval(x,y,r,r,c);
        private void Line(float x0,float y0,float x1,float y1,float width,Color c)
        {
            var p=new Vector2(x0,y0);var q=new Vector2(x1,y1);var n=new Vector2(-(q-p).y,(q-p).x).normalized*width*.5f;Poly(new[]{p+n,q+n,q-n,p-n},c);
        }
    }
}
