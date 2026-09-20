using System.Collections.Generic;
using UnityEngine;

namespace LivingEmpires
{
    /// <summary>Original readable military silhouettes built on the existing ten-bone human rig.
    /// Visual creation never recruits units, creates inventory, or adds gameplay colliders.</summary>
    public static class MilitaryArt
    {
        static readonly Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
        static readonly Color Timber=C("73563B"), Steel=C("626F71"), SteelLight=C("A4AEAA"), Leather=C("6B5140"), Flax=C("E0D3AC");

        public static GameObject CreateSoldier(string kind,bool hostile,Transform parent)
        {
            bool archer=(kind??"").ToLowerInvariant().Contains("arch")||(kind??"").ToLowerInvariant().Contains("bow");
            Color cloth=hostile?C("943F37"):C("245B65");
            // Odd civilian variants have no work tool; their existing walk/work animation remains authoritative.
            GameObject person=WorldArt.CreatePerson(Vector3.zero,parent,cloth,hostile?5:1,false);
            person.name=(hostile?"Raider · ":"Riverhold militia · ")+(archer?"archer":"spearman");
            person.transform.localPosition=Vector3.zero;person.transform.localScale=Vector3.one*.72f;
            var skeleton=person.GetComponent<SkinnedMeshRenderer>().bones;
            string faction=hostile?"raider":"riverhold";
            var armor=new WorldArt.Builder();
            armor.Box(new Vector3(0,1.20f,.137f),new Vector3(.32f,.36f,.065f),archer?Leather:Steel);
            armor.Box(new Vector3(0,1.39f,.025f),new Vector3(.37f,.07f,.28f),archer?Leather:Steel);
            armor.Box(new Vector3(0,1.22f,.176f),new Vector3(.18f,.26f,.018f),cloth);
            armor.Box(new Vector3(0,1.22f,.19f),new Vector3(.11f,.026f,.013f),Flax);
            armor.Box(new Vector3(0,1.02f,.145f),new Vector3(.37f,.055f,.06f),Leather);
            armor.Box(new Vector3(.055f,1.02f,.18f),new Vector3(.055f,.06f,.014f),Flax);
            Render("Militia cuirass and colors",faction+(archer?"_leather":"_cuirass"),armor,skeleton[0]);
            var helmet=new WorldArt.Builder();
            if(archer)
            {
                helmet.Ellipsoid(new Vector3(0,.145f,-.025f),new Vector3(.147f,.105f,.13f),Leather,8,4);
                helmet.Box(new Vector3(0,.11f,-.14f),new Vector3(.23f,.13f,.04f),cloth);
            }
            else
            {
                helmet.Ellipsoid(new Vector3(0,.15f,-.015f),new Vector3(.15f,.115f,.137f),Steel,8,4);
                helmet.Cylinder(new Vector3(0,.115f,-.005f),.173f,.045f,SteelLight,10);
                helmet.Box(new Vector3(0,.043f,.135f),new Vector3(.028f,.15f,.025f),SteelLight);
                helmet.Box(new Vector3(0,.238f,-.015f),new Vector3(.036f,.055f,.21f),cloth);
            }
            Render("Leather cap / iron helmet",faction+(archer?"_cap":"_helmet"),helmet,skeleton[9]);
            if(archer)
            {
                var bow=new WorldArt.Builder();
                Vector3 top=new Vector3(-.06f,.40f,.18f),bottom=new Vector3(-.06f,-.68f,.18f),grip=new Vector3(-.06f,-.14f,.38f);
                bow.Beam(top,new Vector3(-.06f,.18f,.33f),.035f,.035f,Timber);
                bow.Beam(new Vector3(-.06f,.18f,.33f),grip,.039f,.039f,Timber);
                bow.Beam(grip,new Vector3(-.06f,-.46f,.33f),.039f,.039f,Timber);
                bow.Beam(new Vector3(-.06f,-.46f,.33f),bottom,.035f,.035f,Timber);
                bow.Beam(top,bottom,.009f,.009f,Flax);
                bow.Beam(new Vector3(-.06f,-.22f,.38f),new Vector3(-.06f,-.06f,.38f),.053f,.055f,Leather);
                Render("Curved ash bow", "bow",bow,skeleton[2]);
                var quiver=new WorldArt.Builder();
                quiver.Box(new Vector3(.17f,1.18f,-.21f),new Vector3(.14f,.40f,.16f),Leather,Quaternion.Euler(0,0,-12));
                for(int i=0;i<3;i++)
                {
                    quiver.Beam(new Vector3(.13f+i*.041f,1.24f,-.21f),new Vector3(.19f+i*.038f,1.64f,-.21f),.018f,.018f,Timber);
                    quiver.Box(new Vector3(.18f+i*.038f,1.59f,-.21f),new Vector3(.040f,.09f,.013f),Flax,Quaternion.Euler(0,0,-9));
                }
                Render("Leather quiver and arrows","quiver",quiver,skeleton[0]);
            }
            else
            {
                var spear=new WorldArt.Builder();
                spear.Beam(new Vector3(0,-.66f,.12f),new Vector3(0,1.03f,.12f),.038f,.038f,Timber);
                spear.Cone(new Vector3(0,1.03f,.12f),.25f,.065f,SteelLight,4);
                spear.Cylinder(new Vector3(0,1.018f,.12f),.049f,.065f,Steel,6);
                Render("Iron tipped ash spear","spear",spear,skeleton[4]);
                var shield=new WorldArt.Builder();
                shield.Cylinder(new Vector3(-.085f,-.16f,.16f),.25f,.066f,Leather,10,Quaternion.Euler(90,0,0));
                shield.Cylinder(new Vector3(-.085f,-.16f,.204f),.221f,.030f,cloth,10,Quaternion.Euler(90,0,0));
                shield.Cylinder(new Vector3(-.085f,-.16f,.231f),.074f,.046f,SteelLight,8,Quaternion.Euler(90,0,0));
                shield.Box(new Vector3(-.085f,-.16f,.226f),new Vector3(.37f,.040f,.025f),Flax);
                Render("Round shield · "+faction,faction+"_shield",shield,skeleton[2]);
            }
            return person;
        }

        public static GameObject CreateCamp(Transform parent)
        {
            var root=new GameObject("Lower Ridge raider camp");root.transform.SetParent(parent,false);
            // Ensure WorldArt's shared palette and shader are initialized even when this is created in isolation.
            var standard=WorldArt.CreateBanner("raiders",Vector3.zero,root.transform);
            standard.name="Raider camp crossed blades standard";standard.transform.localPosition=new Vector3(-1.55f,0,-.55f);
            var b=new WorldArt.Builder();
            Tent(b,new Vector3(-.75f,0,-.85f),1.8f,1.6f,1.3f,C("A99270"));
            Tent(b,new Vector3(1.05f,0,-.58f),1.25f,1.45f,1.05f,C("8E7560"));
            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2/7;
                b.Ellipsoid(new Vector3(Mathf.Cos(a)*.40f,.08f,1+Mathf.Sin(a)*.40f),new Vector3(.16f,.11f,.12f),C("8C8D7E"),6,3);
            }
            foreach(float turn in new[]{-35f,45f})b.Box(new Vector3(0,.14f,1),new Vector3(.13f,.14f,.56f),Timber,Quaternion.Euler(0,turn,0));
            b.Cone(new Vector3(0,.20f,1),.35f,.15f,C("D89953"),5);
            b.Cone(new Vector3(.035f,.22f,1),.23f,.085f,C("E7BC73"),5);
            for(int i=0;i<3;i++)
            {
                Vector3 p=new Vector3(-1.64f+i*.30f,.25f,.46f);
                b.Box(p,new Vector3(.27f,.48f,.34f),Timber);
                b.Box(p+new Vector3(0,.01f,.18f),new Vector3(.30f,.045f,.03f),Steel);
            }
            foreach(float x in new[]{1.1f,1.8f})b.Box(new Vector3(x,.50f,.86f),new Vector3(.08f,1,.08f),Timber);
            b.Beam(new Vector3(1.1f,.75f,.86f),new Vector3(1.8f,.75f,.86f),.07f,.07f,Timber);
            for(int i=0;i<3;i++)b.Beam(new Vector3(1.24f+i*.2f,0,.76f),new Vector3(1.30f+i*.2f,1.36f,.90f),.035f,.035f,Timber);
            Render("Canvas tents, stores and campfire","raider_camp",b,root.transform);
            return root;
        }

        static void Tent(WorldArt.Builder b,Vector3 p,float width,float depth,float height,Color canvas)
        {
            Vector3 peak0=p+new Vector3(0,height,-depth*.5f),peak1=p+new Vector3(0,height,depth*.5f);
            Vector3 a=p+new Vector3(-width*.5f,.06f,-depth*.5f),c=p+new Vector3(-width*.5f,.06f,depth*.5f),
                d=p+new Vector3(width*.5f,.06f,depth*.5f),e=p+new Vector3(width*.5f,.06f,-depth*.5f);
            b.Quad(c,peak1,peak0,a,canvas);b.Quad(peak1,d,e,peak0,Color.Lerp(canvas,Flax,.13f));
            b.Triangle(e,a,peak0,canvas);
            b.Triangle(c,p+new Vector3(-.20f,.06f,depth*.5f),peak1,Color.Lerp(canvas,Timber,.13f));
            b.Triangle(p+new Vector3(.20f,.06f,depth*.5f),d,peak1,canvas);
            b.Beam(p+new Vector3(0,0,depth*.5f),peak1+Vector3.up*.12f,.055f,.055f,Timber);
            for(int side=-1;side<=1;side+=2)
            {
                b.Beam(peak1,p+new Vector3(side*(width*.5f+.25f),.025f,depth*.5f+.35f),.012f,.012f,Flax);
                b.Box(p+new Vector3(side*(width*.5f+.25f),.09f,depth*.5f+.35f),new Vector3(.04f,.18f,.04f),Timber);
            }
        }
        static GameObject Render(string name,string key,WorldArt.Builder b,Transform parent)
        {
            Mesh mesh;if(!Meshes.TryGetValue(key,out mesh)||mesh==null){mesh=b.ToMesh("Military · "+key);Meshes[key]=mesh;}
            return WorldArt.Render(name,mesh,parent);
        }
        static Color C(string hex){Color color;ColorUtility.TryParseHtmlString("#"+hex,out color);return color;}
    }
}
