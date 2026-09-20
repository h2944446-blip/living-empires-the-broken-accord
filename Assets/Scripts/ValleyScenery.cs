using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingEmpires
{
    /// <summary>Original valley art: continuous painted meadow, sculpted horizon and batched civic details.
    /// All decoration is collider-free. The authoritative settlement grid remains WorldArt's level plane.</summary>
    public static class ValleyScenery
    {
        static Material meadowMaterial;
        static readonly Color Bark=ColorOf("6C5342"), Stone=ColorOf("B5AD96"), StoneDark=ColorOf("858979");
        static readonly Color Flax=ColorOf("D8CDB0"), Teal=ColorOf("315E61"), Reed=ColorOf("7B8C58");
        public static Material MeadowMaterial
        {
            get
            {
                if(meadowMaterial!=null)return meadowMaterial;
                // This original runtime texture is world-continuous, not one tiling swatch per building plot.
                const int size=512;
                var texture=new Texture2D(size,size,TextureFormat.RGB24,true,false)
                {name="Original Veyran meadow - painted grass and earth",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=2};
                var pixels=new Color32[size*size];
                Color moss=ColorOf("73866A"), grass=ColorOf("92A079"), dry=ColorOf("A8AB85"), earth=ColorOf("8C9573");
                for(int z=0;z<size;z++)for(int x=0;x<size;x++)
                {
                    float wx=x/(size-1f)*140f-70f,wz=z/(size-1f)*116f-58f;
                    float broad=Mathf.PerlinNoise(wx*.056f+11f,wz*.066f+27f);
                    float detail=Mathf.PerlinNoise(wx*.71f+12f,wz*.82f+7f);
                    float micro=Mathf.PerlinNoise(wx*2.9f+38f,wz*3.1f+45f);
                    Color tint=Color.Lerp(moss,grass,Mathf.Clamp01(broad*.92f+detail*.20f));
                    float worn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.77f,Mathf.PerlinNoise(wx*.18f+5,wz*.13f+9)));
                    tint=Color.Lerp(tint,dry,worn*.45f);
                    float river=Mathf.Exp(-Mathf.Pow((Mathf.Abs(wx)-2.6f)/1.7f,2));
                    tint=Color.Lerp(tint,earth,river*.58f);
                    // Fine directional flecks catch the camera without grass geometry or alpha overdraw.
                    float fleck=(micro-.5f)*.065f+(detail-.5f)*.035f;
                    tint.r+=fleck;tint.g+=fleck;tint.b+=fleck*.68f;
                    pixels[z*size+x]=tint;
                }
                texture.SetPixels32(pixels);texture.Apply(true,false);
                Shader shader=Shader.Find("Universal Render Pipeline/Lit");
                meadowMaterial=new Material(shader){name="Veyran meadow · original continuous ground",enableInstancing=true};
                meadowMaterial.SetTexture("_BaseMap",texture);meadowMaterial.SetColor("_BaseColor",Color.white);
                meadowMaterial.SetFloat("_Smoothness",.025f);meadowMaterial.SetFloat("_Metallic",0);
                return meadowMaterial;
            }
        }

        public static void AddRiverBanks(Transform parent)
        {
            var bank=new WorldArt.Builder();var details=new WorldArt.Builder();
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<116;i++)
            {
                float z=-58+i;
                float inner=2.10f+Mathf.Sin(z*.40f)*.065f;
                float outer=2.72f+Mathf.Sin(z*.28f+1)*.22f;
                float innerNext=2.10f+Mathf.Sin((z+1)*.40f)*.065f;
                float outerNext=2.72f+Mathf.Sin((z+1)*.28f+1)*.22f;
                Vector3 a=new Vector3(side*inner,-.135f,z),b=new Vector3(side*outer,.042f,z),
                    c=new Vector3(side*outerNext,.042f,z+1),d=new Vector3(side*innerNext,-.135f,z+1);
                if(side>0)bank.Quad(d,c,b,a,i%4==0?ColorOf("A7AB8A"):ColorOf("999F80"));
                else bank.Quad(a,b,c,d,i%4==0?ColorOf("A7AB8A"):ColorOf("999F80"));
                bool crossing=NearCrossing(z);
                if(i%3==0&&!crossing&&Mathf.Abs(z)<29)
                {
                    Vector3 p=new Vector3(side*(outer+.08f),.04f,z+.45f);
                    for(int r=0;r<4;r++)
                    {
                        Vector3 stem=p+new Vector3((r%2)*.11f,0,(r/2)*.13f);
                        details.Beam(stem,stem+new Vector3(.07f,.42f+(r%3)*.11f,.02f),.027f,.015f,Reed);
                        details.Beam(stem+Vector3.up*.14f,stem+new Vector3(-.13f,.32f,.09f),.035f,.012f,Reed);
                    }
                }
                if(i%4==0&&!crossing)
                {
                    Vector3 p=new Vector3(side*(inner+.09f),-.02f,z+.2f);
                    details.Ellipsoid(p,new Vector3(.18f,.12f,.25f),i%8==0?Stone:StoneDark,6,3);
                    details.Ellipsoid(p+new Vector3(side*.22f,.02f,.35f),new Vector3(.11f,.075f,.14f),StoneDark,5,3);
                }
                if(i%5==0)
                {
                    // Broken opaque foam strokes float just above the actual moving water.
                    details.Box(new Vector3(side*(inner-.10f),-.113f,z+.21f),new Vector3(.038f,.005f,.36f),ColorOf("9DB6A1"),Quaternion.Euler(0,side*9,0));
                    details.Box(new Vector3(side*(inner-.17f),-.112f,z+.57f),new Vector3(.022f,.005f,.15f),ColorOf("8FAE9C"));
                }
            }
            WorldArt.Render("Soft eroded riverbanks",bank.ToMesh("Basin mud and grass shelves"),parent);
            var detail=WorldArt.Render("Alder reeds, river stones and foam",details.ToMesh("River verge details"),parent);
            detail.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        public static Transform Build(Transform parent,int seed)
        {
            var root=new GameObject("Veyran Basin · rebuilt landscape").transform;root.SetParent(parent,false);
            BuildHorizon(root);
            BuildCivicDetails(root);
            BuildPasture(root,seed);
            return root;
        }

        static void BuildHorizon(Transform parent)
        {
            // Four surrounding strips share the meadow material. No primitive cones or giant flat board edges.
            for(int band=0;band<4;band++)
            {
                var b=new WorldArt.Builder();
                float x0=band==0?-70:band==1?44:-44,x1=band==0?-44:band==1?70:44;
                float z0=band==2?-58:band==3?30.8f:-58,z1=band==2?-30.8f:band==3?58:58;
                int nx=Mathf.CeilToInt((x1-x0)/2.5f),nz=Mathf.CeilToInt((z1-z0)/2.5f);
                for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
                {
                    float ax=Mathf.Lerp(x0,x1,x/(float)nx),bx=Mathf.Lerp(x0,x1,(x+1f)/nx);
                    float az=Mathf.Lerp(z0,z1,z/(float)nz),bz=Mathf.Lerp(z0,z1,(z+1f)/nz);
                    // Continue the river cleanly through the surrounding landscape.
                    if(ax<2.19f&&bx>-2.19f)continue;
                    b.Quad(new Vector3(ax,HorizonHeight(ax,bz),bz),new Vector3(bx,HorizonHeight(bx,bz),bz),
                        new Vector3(bx,HorizonHeight(bx,az),az),new Vector3(ax,HorizonHeight(ax,az),az),Color.white);
                }
                Mesh mesh=b.ToMesh("Rolling basin rim "+band);var verts=mesh.vertices;var uv=new Vector2[verts.Length];
                for(int i=0;i<verts.Length;i++)uv[i]=new Vector2((verts[i].x+70)/140f,(verts[i].z+58)/116f);
                mesh.uv=uv;
                var go=WorldArt.Render("Rolling wooded ridge "+band,mesh,parent,MeadowMaterial);
                go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            // Layered irregular silhouettes make a far horizon visible at a lower strategy-camera angle.
            var peaks=new WorldArt.Builder();
            for(int i=0;i<15;i++)
            {
                float x=-67+i*9.5f,z=-54f-Mathf.Sin(i*1.72f)*5f;
                float rise=5.5f+Mathf.PerlinNoise(i*.6f,21)*5f;
                Vector3 center=new Vector3(x,3.6f,z);
                peaks.Ellipsoid(center,new Vector3(8.5f,rise,6.2f),i%3==0?ColorOf("647F76"):ColorOf("728B7D"),7,4);
                peaks.Ellipsoid(center+new Vector3(2.7f,2.4f,-2),new Vector3(5.8f,rise*.95f,4.5f),ColorOf("80958A"),6,3);
            }
            var ridge=WorldArt.Render("High Pass · layered distant ridge",peaks.ToMesh("Faceted natural horizon"),parent);
            ridge.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        static float HorizonHeight(float x,float z)
        {
            float distance=Mathf.Max(Mathf.Abs(x)-44,Mathf.Abs(z)-30.8f);
            float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/15f));
            float hill=2.8f+Mathf.PerlinNoise(x*.050f+14,z*.046f+23)*8f;
            float bank=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,16,Mathf.Abs(x)));
            return WorldArt.GroundHeight(Mathf.Clamp(x,-44,44),Mathf.Clamp(z,-30.8f,30.8f))*(1-blend)+hill*blend*bank;
        }

        static void BuildCivicDetails(Transform parent)
        {
            var b=new WorldArt.Builder();
            // Signs stand beside, never on, the working bridge/ferry and Lower Ridge approaches.
            foreach(float z in new[]{WorldArt.BridgePosition.z,WorldArt.FerryPosition.z,WorldArt.RidgePosition.z})
            foreach(int side in new[]{-1,1})
            {
                Vector3 p=new Vector3(side*3.9f,0,z+1.2f);
                b.Box(p+Vector3.up*.69f,new Vector3(.075f,1.38f,.075f),Bark);
                b.Box(p+new Vector3(side*.23f,1.13f,0),new Vector3(.71f,.20f,.07f),Flax,Quaternion.Euler(0,side*8,0));
                b.Box(p+new Vector3(side*.17f,1.13f,.041f),new Vector3(.32f,.025f,.013f),Teal,Quaternion.Euler(0,side*8,0));
                b.Ellipsoid(p+Vector3.up*.07f,new Vector3(.16f,.10f,.15f),StoneDark,6,3);
            }
            // Landmark gateways belong to the rival settlements outside all player charter plots.
            foreach(var entry in new[]{new Vector3(22.3f,0,-8.9f),new Vector3(22.3f,0,20.7f)})
            {
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 p=entry+new Vector3(side*1.15f,0,0);
                    b.Box(p+Vector3.up*.43f,new Vector3(.36f,.86f,.40f),Stone);
                    b.Box(p+Vector3.up*1.33f,new Vector3(.17f,1.14f,.19f),Bark);
                    b.Roof(p+Vector3.up*1.90f,.53f,.63f,.22f,Teal,ColorOf("577873"));
                    b.Box(p+new Vector3(0,1.17f,.12f),new Vector3(.32f,.55f,.022f),entry.z<0?ColorOf("9A5946"):ColorOf("55764D"));
                }
            }
            // Small weathered relic in the nonbuildable eastern meadow, with no implied interactive feature.
            Vector3 ruin=new Vector3(32,0,1.8f);
            for(int i=0;i<5;i++)
            {
                Vector3 p=ruin+new Vector3(Mathf.Cos(i*1.4f)*1.1f,0,Mathf.Sin(i*1.4f)*.9f);
                b.Box(p+Vector3.up*(.28f+i%2*.19f),new Vector3(.45f,.56f+i%2*.38f,.42f),i%2==0?StoneDark:Stone,Quaternion.Euler(0,i*24,0));
            }
            WorldArt.Render("Crossing waymarkers and city gateways",b.ToMesh("Civic detail batch"),parent);
        }

        static void BuildPasture(Transform parent,int seed)
        {
            var b=new WorldArt.Builder();var random=new System.Random(seed+901);
            // Meadow detail stays beyond the charter boundary. Low opaque meshes avoid grass overdraw.
            for(int i=0;i<145;i++)
            {
                float x=i<80?-39.5f+(float)random.NextDouble()*2.2f:9f+(float)random.NextDouble()*25f;
                float z=-24f+(float)random.NextDouble()*47;
                if(Mathf.Abs(z-WorldArt.BridgePosition.z)<2||Mathf.Abs(z-WorldArt.FerryPosition.z)<2||Mathf.Abs(z-WorldArt.RidgePosition.z)<2)continue;
                bool town=false;foreach(Vector3 t in WorldArt.TownAnchors)if(Vector3.Distance(new Vector3(x,0,z),t)<6.5f)town=true;
                if(town||Mathf.Abs(x-WorldArt.GridToWorld(30,14).x)<1.3f)continue;
                Vector3 p=new Vector3(x,WorldArt.GroundHeight(x,z)+.02f,z);
                for(int stalk=0;stalk<3;stalk++)
                {
                    float h=.11f+(float)random.NextDouble()*.16f;
                    Vector3 stem=p+new Vector3(stalk*.10f,0,(stalk%2)*.09f);
                    b.Triangle(stem+Vector3.left*.035f,stem+new Vector3(.05f,h,0),stem+Vector3.right*.035f,Reed);
                    if(i%7==0)b.Ellipsoid(stem+Vector3.up*h,new Vector3(.043f,.025f,.043f),Flax,4,2);
                }
            }
            var go=WorldArt.Render("Wildflower meadow verges",b.ToMesh("Batched valley ground detail"),parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static bool NearCrossing(float z)
        {return Mathf.Abs(z-WorldArt.BridgePosition.z)<1.25f||Mathf.Abs(z-WorldArt.FerryPosition.z)<1.25f||Mathf.Abs(z-WorldArt.RidgePosition.z)<1.25f;}
        static Color ColorOf(string hex){Color color;ColorUtility.TryParseHtmlString("#"+hex,out color);return color;}
    }
}
