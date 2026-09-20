// Original procedural art for Living Empires. See WORLD_ART_LICENSE.md.
// Unity coordinates: +Y up, +Z forward. Licensed imported presentation uses ImportedBuildings/ImportedEnvironment.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingEmpires
{
    /// <summary>Original reusable meshes, palette materials and a single lightweight motion driver.</summary>
    public sealed class WorldArt : MonoBehaviour
    {
        public const float TileSize = 2.2f;
        private static WorldArt runner;
        private static Material opaqueMaterial, waterMaterial;
        private static Texture2D paletteTexture, waterTexture;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Color32, int> PaletteSlots = new Dictionary<Color32, int>();
        private static int nextPaletteSlot;
        private static float animationSpeed = 1f;
        private static bool winter;
        public static readonly Vector3 BridgePosition = new Vector3(0,0,7.7f);
        public static readonly Vector3 FerryPosition = new Vector3(0,0,14.3f);
        public static readonly Vector3 RidgePosition = new Vector3(0,0,25.3f);
        public static readonly Vector3[] TownAnchors = { GridToWorld(11,14), GridToWorld(31,8), GridToWorld(31,22) };

        public static readonly Color RiverTeal = Hex("23565A");
        public static readonly Color Ochre = Hex("B58A49");
        private static readonly Color Stone = Hex("C9B99A"), StoneDark = Hex("A49880");
        private static readonly Color Timber = Hex("725746"), TimberLight = Hex("AD8A60");
        private static readonly Color Plaster = Hex("E1D6BB"), Flax = Hex("E6DCC5");
        private static readonly Color Iron = Hex("343B3C"), Patina = Hex("6F8C7B");
        private static readonly Color Roof = Hex("596D67"), RoofLight = Hex("708477");
        private static readonly Color Soil = Hex("756343"), Crop = Hex("CDB26D");
        private static readonly Color Leaf = Hex("486B51"), LeafLight = Hex("6C885B");

        private readonly List<Rotor> rotors = new List<Rotor>();
        private readonly List<CartRig> carts = new List<CartRig>();
        private readonly List<PersonRig> people = new List<PersonRig>();
        private readonly Dictionary<GameObject, PersonRig> personLookup = new Dictionary<GameObject, PersonRig>();
        private readonly List<FloatRig> floats = new List<FloatRig>();
        private readonly List<GameObject> winterObjects = new List<GameObject>();
        private float clock;

        private sealed class Rotor { public Transform pivot; public float rate; }
        private sealed class FloatRig { public Transform pivot; public Vector3 origin; public float phase; }
        private sealed class CartRig { public Transform root; public Transform[] wheels; public Vector3 previous; }
        private sealed class PersonRig
        {
            public Transform root;
            public Transform[] bones;
            public Vector3 previous;
            public float phase, activityBlend;
            public GameObject identity;
            public int mode; // 0 infer walking from movement, 1 idle, 2 walking, 3 work.
        }

        public static Vector3 GridToWorld(int x, int z)
        {
            return new Vector3((x - 19.5f) * TileSize, 0f, (z - 13.5f) * TileSize);
        }

        public static Vector2Int WorldToGrid(Vector3 position)
        { return new Vector2Int(Mathf.RoundToInt(position.x/TileSize+19.5f),Mathf.RoundToInt(position.z/TileSize+13.5f)); }

        /// <summary>Display paths, not a navigation/traffic simulation. Units face local +Z.</summary>
        public static Vector3[] GetCargoWaypoints(int from,int to,string route)
        {
            from=Mathf.Clamp(from,0,2);to=Mathf.Clamp(to,0,2);
            Vector3 a=TownAnchors[from],b=TownAnchors[to];
            float west=GridToWorld(11,14).x,east=GridToWorld(30,14).x;
            var points=new List<Vector3>(); points.Add(a+Vector3.up*0.055f);
            if(from!=0&&to!=0)
            {
                points.Add(new Vector3(east,0.055f,a.z));points.Add(new Vector3(east,0.055f,b.z));
            }
            else
            {
                string name=(route??"bridge").ToLowerInvariant();
                bool ridge=name.Contains("ridge")||name.Contains("relief");bool ferry=name.Contains("ferry");
                float z=ridge?RidgePosition.z:ferry?FerryPosition.z:BridgePosition.z;
                float ax=from==0?west:east,bx=to==0?west:east,sign=from==0?-1f:1f;
                float deck=ridge?0.09f:ferry?0.24f:0.42f;
                points.Add(new Vector3(ax,0.055f,a.z));points.Add(new Vector3(ax,0.055f,z));
                points.Add(new Vector3(sign*3.5f,0.055f,z));points.Add(new Vector3(sign*2.9f,deck,z));
                points.Add(new Vector3(-sign*2.9f,deck,z));points.Add(new Vector3(-sign*3.5f,0.055f,z));
                points.Add(new Vector3(bx,0.055f,z));points.Add(new Vector3(bx,0.055f,b.z));
            }
            points.Add(b+Vector3.up*0.055f);return points.ToArray();
        }

        public static void SetWinter(bool enabled)
        {
            winter=enabled;if(runner==null)return;
            for(int i=runner.winterObjects.Count-1;i>=0;i--)
            {if(runner.winterObjects[i]==null)runner.winterObjects.RemoveAt(i);else runner.winterObjects[i].SetActive(enabled);}
        }

        /// <summary>Ground, river, clustered foliage, roads and settlement squares; no simulation buildings.</summary>
        public static Transform BuildLandscape(Transform parent = null, int seed = 71829)
        {
            EnsureResources();
            Transform root = MakeRoot("Veyran valley · landscape", Vector3.zero, parent).transform;
            EnsureRunner(parent);
            var random = new System.Random(seed);
            // One continuous original meadow texture removes the former checkerboard of plots.
            // Only the perimeter rises; the complete settlement and every transport route remain level.
            Material meadow=ValleyScenery.MeadowMaterial;
            for (int bx = 0; bx < 2; bx++)
            for (int bz = 0; bz < 2; bz++)
            {
                var land = new Builder();
                for (int x = bx * 20; x < (bx + 1) * 20; x++)
                for (int z = bz * 14; z < (bz + 1) * 14; z++)
                {
                    if (x == 19 || x == 20) continue;
                    Vector3 p = GridToWorld(x, z);
                    land.Quad(new Vector3(p.x-1.1f,GroundHeight(p.x-1.1f,p.z+1.1f),p.z+1.1f),
                        new Vector3(p.x+1.1f,GroundHeight(p.x+1.1f,p.z+1.1f),p.z+1.1f),
                        new Vector3(p.x+1.1f,GroundHeight(p.x+1.1f,p.z-1.1f),p.z-1.1f),
                        new Vector3(p.x-1.1f,GroundHeight(p.x-1.1f,p.z-1.1f),p.z-1.1f),Color.white);
                }
                Mesh mesh=land.ToMesh("Continuous valley meadow "+bx+":"+bz);
                Vector3[] vertices=mesh.vertices;var uv=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)uv[i]=new Vector2((vertices[i].x+70f)/140f,(vertices[i].z+58f)/116f);
                mesh.uv=uv;
                GameObject ground = Render("Meadow terrain " + bx + ":" + bz,mesh,root,meadow);
                ground.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            ValleyScenery.AddRiverBanks(root);
            ImportedEnvironment.AddLandscape(root);
            var water = new Builder();
            water.Quad(new Vector3(-2.19f,-0.12f,58),new Vector3(2.19f,-0.12f,58),
                new Vector3(2.19f,-0.12f,-58),new Vector3(-2.19f,-0.12f,-58),Hex("457D7C"));
            Mesh waterMesh = water.ToMesh("River surface");
            waterMesh.uv = new[] {new Vector2(0,12),new Vector2(1,12),new Vector2(1,0),new Vector2(0,0)};
            var river=Render("Alder River",waterMesh,root,waterMaterial);
            river.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var roads = new Builder();
            Road(roads,GridToWorld(10,17),GridToWorld(18,17),0.72f);
            Road(roads,GridToWorld(21,17),GridToWorld(32,17),0.72f);
            Road(roads,GridToWorld(30,8),GridToWorld(30,25),0.65f);
            Road(roads,GridToWorld(11,14),GridToWorld(11,25),0.62f);
            Road(roads,GridToWorld(11,20),GridToWorld(18,20),0.54f);
            Road(roads,GridToWorld(21,20),GridToWorld(30,20),0.54f);
            Road(roads,GridToWorld(11,25),GridToWorld(30,25),0.48f);
            // Permanent shallow stepping-stone ford for the Lower Ridge relief route.
            for (int i = -3; i <= 3; i++)
                roads.Box(new Vector3(i*0.73f,0.01f,GridToWorld(20,25).z),new Vector3(0.58f,0.12f,0.70f),StoneDark);
            foreach (Vector2Int c in new[]{new Vector2Int(11,14),new Vector2Int(31,8),new Vector2Int(31,22)})
            {
                Vector3 p = GridToWorld(c.x,c.y);
                // Small worn civic paving replaces the former large blank beige square.
                roads.Cylinder(p+Vector3.up*.014f,1.24f,.028f,Hex("959A80"),11);
                for(int px=-3;px<=3;px++)for(int pz=-3;pz<=3;pz++)
                {
                    if(px*px+pz*pz>11)continue;
                    Vector3 tile=p+new Vector3(px*.33f+(pz%2)*.06f,.036f,pz*.33f);
                    roads.Box(tile,new Vector3(.29f,.019f,.27f),(px+pz)%3==0?Hex("B1AC95"):Hex("9D9D85"));
                }
            }
            Render("Valley roads and squares",roads.ToMesh("Road network"),root);
            ValleyScenery.Build(root,seed);
            var winterSnow = new Builder();
            for(int i=0;i<12;i++)
            {
                Vector3 p=new Vector3(-54+i*9.5f,5.3f,-43f-Range(random,0,6));
                float height=Range(random,3.5f,5.5f);
                winterSnow.Cone(p,height,Range(random,2.8f,4.2f),Hex("DFE5DA"),7);
            }
            GameObject winterLayer=Render("Winter snow above the valley routes",winterSnow.ToMesh("Seasonal snow caps"),root);
            winterLayer.SetActive(winter);EnsureRunner(parent).winterObjects.Add(winterLayer);
            return root;
        }

        public static GameObject CreateConstruction(bool bridge,Vector3 position,Transform parent=null)
        {
            EnsureResources();var root=MakeRoot(bridge?"Bridge repair scaffolding":"Landing works",position,parent);var b=new Builder();
            foreach(float x in new[]{-2.8f,2.8f})
            {
                foreach(float z in new[]{-0.78f,0.78f})b.Box(new Vector3(x,0.61f,z),new Vector3(0.10f,1.6f,0.10f),Timber);
                b.Beam(new Vector3(x,-0.14f,-0.78f),new Vector3(x,1.24f,0.78f),0.07f,0.07f,TimberLight);
                b.Box(new Vector3(x,1.32f,0),new Vector3(0.42f,0.08f,1.85f),TimberLight);
                AddCrate(b,new Vector3(x+(x<0?-0.5f:0.5f),0,0.45f),0.4f);
            }
            if(bridge)
            {
                foreach(float z in new[]{-0.83f,0.83f})b.Beam(new Vector3(-2.8f,1.38f,z),new Vector3(2.8f,1.38f,z),0.035f,0.035f,TimberLight);
                b.Box(new Vector3(-2.3f,0.36f,0),new Vector3(1.05f,0.16f,1.50f),StoneDark);
            }
            Render("Construction timber, crates and guide lines",Cached(bridge?"bridge_scaffold":"ferry_scaffold",b),root.transform);return root;
        }

        /// <summary>Supported: farm, lumberyard, mill, bakery, mine, smelter/forge, toolsmith, house,
        /// warehouse, market, townhall/hall. Each has an original, cached combined mesh.</summary>
        public static GameObject CreateBuilding(string type, Vector3 position, Transform parent = null, Color? accent = null)
        {
            EnsureResources();
            string kind=(type??"house").ToLowerInvariant();
            if(kind=="forge")kind="smelter";
            if(kind=="hall")kind="townhall";
            Color cloth=accent??RiverTeal;
            if(ImportedBuildings.TryCreate(kind,position,parent,out GameObject imported))
            {
                if(accent.HasValue)
                {
                    string faction=cloth.r>cloth.g?"ironvale":cloth.g>cloth.b?"pinewatch":"riverhold";
                    var banner=CreateBanner(faction,Vector3.zero,imported.transform);
                    banner.transform.localPosition=new Vector3(.72f,.12f,.72f);
                    banner.transform.localScale=Vector3.one*.32f;
                }
                return imported;
            }
            string key="building:"+kind+":"+ColorUtility.ToHtmlStringRGB(cloth);
            Mesh mesh;
            if(!Meshes.TryGetValue(key,out mesh)||mesh==null)
            {
                var b=new Builder();
                BuildArchitecture(b,kind,cloth);
                mesh=b.ToMesh(key); Meshes[key]=mesh;
            }
            GameObject root=MakeRoot(kind,position,parent);
            Render(kind+" · combined structure",mesh,root.transform);
            var collider=root.AddComponent<BoxCollider>();
            collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;
            if(kind=="mill")
            {
                collider.center=new Vector3(0,1.44f,0);collider.size=new Vector3(2.35f,2.88f,1.96f);
                var blades=new Builder();
                for(int i=0;i<4;i++)
                {
                    Quaternion r=Quaternion.Euler(0,0,i*90);
                    blades.Box(r*new Vector3(0,0.53f,0),new Vector3(0.075f,1.04f,0.10f),Timber,r);
                    blades.Box(r*new Vector3(0.12f,0.74f,0.035f),new Vector3(0.28f,0.63f,0.055f),Flax,r);
                    for(int j=0;j<4;j++) blades.Box(r*new Vector3(0.12f,0.48f+j*0.15f,0.077f),new Vector3(0.30f,0.025f,0.02f),TimberLight,r);
                }
                blades.Cylinder(Vector3.zero,0.115f,0.2f,Timber,10,Quaternion.Euler(90,0,0));
                Transform pivot=MakeRoot("Windmill sail axle",position,root.transform).transform;
                pivot.localPosition=new Vector3(0,1.78f,0.82f);
                Render("Canvas sails",Cached("mill_sails",blades),pivot);
                EnsureRunner(parent).rotors.Add(new Rotor{pivot=pivot,rate=18f});
            }
            if(kind=="farm")ImportedEnvironment.AddWorkplaceDetails(kind,root.transform);
            return root;
        }

        /// <summary>Explicit faction identity; recolored Riverhold marks are never used as another faction.</summary>
        public static GameObject CreateBanner(string faction,Vector3 position,Transform parent=null)
        {
            EnsureResources();faction=(faction??"riverhold").ToLowerInvariant();
            Color cloth=faction=="raiders"?Hex("943F37"):faction=="ironvale"?Iron:faction=="pinewatch"?Hex("3D5A4C"):faction=="tidemark"?Hex("293F52"):faction=="stonewake"?Hex("626B6B"):RiverTeal;
            var b=new Builder();AddBanner(b,Vector3.zero,cloth,faction);
            GameObject root=MakeRoot(faction+" civic banner",position,parent);Render("Original heraldic mesh",Cached("banner:"+faction,b),root.transform);return root;
        }

        public static GameObject CreateCrossing(bool bridge, Vector3 position, Transform parent = null)
        {
            EnsureResources();
            GameObject root=MakeRoot(bridge?"Accord stone bridge":"Ira's ferry",position,parent);
            var b=new Builder();
            for(int side=-1;side<=1;side+=2)
            {
                b.Box(new Vector3(side*2.83f,-0.12f,0),new Vector3(1.0f,1.0f,1.65f),StoneDark);
                if(!bridge)
                {
                    for(int plank=0;plank<8;plank++) b.Box(new Vector3(side*2.78f,0.20f,(plank-3.5f)*0.19f),new Vector3(1.65f,0.14f,0.17f),plank%2==0?TimberLight:Timber);
                    foreach(float z in new[]{-0.81f,0.81f})b.Cylinder(new Vector3(side*2.7f,0.4f,z),0.075f,1.25f,Timber,6);
                }
            }
            if(bridge)
            {
                b.Arch(new Vector3(0,-0.59f,0),2.65f,0.75f,0.22f,1.35f,Stone,12);
                b.Box(new Vector3(0,0.30f,0),new Vector3(6.2f,0.20f,1.45f),Stone);
                for(int i=0;i<14;i++)foreach(int side in new[]{-1,1})
                    b.Box(new Vector3(-2.85f+i*0.44f,0.60f,side*0.73f),new Vector3(0.42f,0.48f,0.20f),i%3==0?StoneDark:Stone);
                for(int i=0;i<13;i++)b.Box(new Vector3(-2.75f+i*0.45f,0.411f,0),new Vector3(0.027f,0.015f,1.2f),StoneDark);
                Render("Stone arch and parapet",Cached("bridge",b),root.transform);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,0.21f,0);collider.size=new Vector3(6.2f,0.4f,1.45f);
            }
            else
            {
                b.Beam(new Vector3(-3.0f,1.0f,-0.72f),new Vector3(3.0f,1.0f,-0.72f),0.025f,0.025f,TimberLight);
                Render("Ferry landings and guide rope",Cached("ferry_landings",b),root.transform);
                var boat=new Builder();
                boat.Hull(Vector3.zero,2.25f,1.25f,0.30f,Timber);
                for(int i=0;i<9;i++)boat.Box(new Vector3((i-4)*0.23f,0.14f,0),new Vector3(0.21f,0.10f,1.15f),TimberLight);
                boat.Beam(new Vector3(-0.83f,0.4f,-0.45f),new Vector3(-0.45f,1.1f,-0.45f),0.06f,0.06f,Timber);
                AddCrate(boat,new Vector3(0.5f,0.18f,0.22f),0.42f);
                Transform raft=MakeRoot("Ferry platform",Vector3.zero,root.transform).transform;
                raft.localPosition=new Vector3(0,-0.04f,0);
                Render("Ferry hull",Cached("ferry_hull",boat),raft);
                EnsureRunner(parent).floats.Add(new FloatRig{pivot=raft,origin=raft.localPosition,phase=0.8f});
            }
            return root;
        }

        /// <summary>Cart forward is +Z. Root pivot is under the cargo bed; movement remains caller-owned.</summary>
        public static GameObject CreateCart(Vector3 position, Transform parent = null, Color? accent = null)
        {
            EnsureResources(); Color cloth=accent??Ochre;
            GameObject root=MakeRoot("Mule cargo cart",position,parent);
            var b=new Builder();
            b.Box(new Vector3(0,0.55f,0),new Vector3(0.92f,0.12f,1.25f),Timber);
            foreach(int side in new[]{-1,1})
            {
                b.Box(new Vector3(side*0.48f,0.77f,0),new Vector3(0.08f,0.40f,1.3f),TimberLight);
                b.Beam(new Vector3(side*0.33f,0.5f,0.25f),new Vector3(side*0.27f,0.56f,1.85f),0.055f,0.055f,Timber);
            }
            foreach(float z in new[]{-0.62f,0.62f})b.Box(new Vector3(0,0.74f,z),new Vector3(0.93f,0.34f,0.06f),TimberLight);
            AddCrate(b,new Vector3(-0.18f,0.65f,-0.2f),0.43f);
            b.Ellipsoid(new Vector3(0.20f,0.87f,0.18f),new Vector3(0.24f,0.25f,0.35f),Flax,8,5);
            b.Box(new Vector3(0,1.0f,-0.28f),new Vector3(0.56f,0.035f,0.48f),cloth);
            AddMule(b,new Vector3(0,0,1.6f));
            Render("Cart, harness and mule",Cached("cart:"+ColorUtility.ToHtmlStringRGB(cloth),b),root.transform);
            var wheel=new Builder();
            wheel.Cylinder(Vector3.zero,0.27f,0.065f,Timber,12,Quaternion.Euler(0,0,90));
            wheel.Cylinder(Vector3.zero,0.06f,0.13f,Iron,8,Quaternion.Euler(0,0,90));
            for(int i=0;i<6;i++)
            {
                Quaternion r=Quaternion.Euler(i*30,0,0);
                wheel.Box(Vector3.zero,new Vector3(0.082f,0.47f,0.035f),TimberLight,r);
            }
            Mesh wheelMesh=Cached("cart_wheel",wheel);var wheelRoots=new Transform[4];int n=0;
            foreach(float x in new[]{-0.54f,0.54f})foreach(float z in new[]{-0.41f,0.41f})
            {
                Transform pivot=MakeRoot("Wheel",Vector3.zero,root.transform).transform;pivot.localPosition=new Vector3(x,0.28f,z);
                Render("Spoked wheel",wheelMesh,pivot);wheelRoots[n++]=pivot;
            }
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,0.55f,0.7f);collider.size=new Vector3(1.1f,1.1f,2.9f);
            EnsureRunner(parent).carts.Add(new CartRig{root=root.transform,wheels=wheelRoots,previous=position});
            return root;
        }

        /// <summary>Original rigid-weight low-poly human, one SkinnedMeshRenderer and ten bones.
        /// Variant changes skin/clothing/tool; these are townspeople, not portraits of named principals.</summary>
        public static GameObject CreatePerson(Vector3 position, Transform parent = null, Color? accent = null, int variant = 0, bool collidable = true)
        {
            EnsureResources();Color cloth=accent??RiverTeal;variant=((variant%6)+6)%6;
            GameObject root=MakeRoot("Worker "+variant,position,parent);
            root.transform.localScale=Vector3.one*0.58f;
            Vector3[] bind={Vector3.zero,new Vector3(-0.235f,1.37f,0),new Vector3(-0.235f,1.09f,0),
                new Vector3(0.235f,1.37f,0),new Vector3(0.235f,1.09f,0),new Vector3(-0.09f,0.91f,0),
                new Vector3(-0.09f,0.51f,0),new Vector3(0.09f,0.91f,0),new Vector3(0.09f,0.51f,0),new Vector3(0,1.54f,0)};
            int[] parents={-1,0,1,0,3,0,5,0,7,0};var bones=new Transform[10];
            for(int i=0;i<bones.Length;i++)
            {
                bones[i]=new GameObject("Rig "+i).transform;
                bones[i].SetParent(parents[i]<0?root.transform:bones[parents[i]],false);
                bones[i].localPosition=parents[i]<0?bind[i]:bind[i]-bind[parents[i]];
            }
            string key="person:"+variant+":"+ColorUtility.ToHtmlStringRGB(cloth);Mesh mesh;
            if(!Meshes.TryGetValue(key,out mesh)||mesh==null)
            {
                var b=new Builder();Color skin=new[]{Hex("B68259"),Hex("785039"),Hex("D4A57C"),Hex("8F5E43"),Hex("C19571"),Hex("624333")}[variant];
                Color coat=variant%2==0?cloth:Color.Lerp(cloth,Flax,0.23f);
                b.Box(new Vector3(0,1.18f,0),new Vector3(0.35f,0.42f,0.23f),coat);
                b.Box(new Vector3(0,0.98f,0),new Vector3(0.31f,0.12f,0.24f),Timber);
                b.Box(new Vector3(0,1.12f,0.127f),new Vector3(0.23f,0.40f,0.025f),variant%2==0?Ochre:Flax);
                b.Cylinder(new Vector3(0,1.43f,0),0.065f,0.13f,skin,7);
                b.Bone=9;
                b.Ellipsoid(new Vector3(0,1.58f,0),new Vector3(0.13f,0.165f,0.125f),skin,8,6);
                b.Ellipsoid(new Vector3(0,1.70f,-0.02f),new Vector3(0.135f,0.055f,0.12f),variant==4?StoneDark:Timber,8,4);
                b.Box(new Vector3(0,1.56f,0.12f),new Vector3(0.045f,0.055f,0.035f),skin);
                if(variant%3==0)b.Cylinder(new Vector3(0,1.74f,0),0.17f,0.065f,Flax,10);
                for(int side=-1;side<=1;side+=2)
                {
                    int upper=side<0?1:3;int lower=side<0?2:4;
                    b.Bone=upper;b.Box(new Vector3(side*0.235f,1.235f,0),new Vector3(0.115f,0.28f,0.13f),coat);
                    b.Bone=lower;b.Box(new Vector3(side*0.235f,0.955f,0),new Vector3(0.09f,0.25f,0.105f),Flax);
                    b.Ellipsoid(new Vector3(side*0.235f,0.805f,0.01f),new Vector3(0.055f,0.07f,0.055f),skin,6,4);
                    b.Bone=side<0?5:7;b.Box(new Vector3(side*0.09f,0.71f,0),new Vector3(0.13f,0.40f,0.15f),Iron);
                    b.Bone=side<0?6:8;b.Box(new Vector3(side*0.09f,0.325f,0),new Vector3(0.11f,0.37f,0.13f),Timber);
                    b.Box(new Vector3(side*0.09f,0.09f,0.06f),new Vector3(0.14f,0.14f,0.25f),Timber);
                }
                if(variant%2==0)
                {
                    b.Bone=4;b.Beam(new Vector3(0.235f,0.80f,-0.14f),new Vector3(0.235f,0.80f,0.40f),0.035f,0.035f,TimberLight);
                    b.Box(new Vector3(0.235f,0.80f,0.39f),new Vector3(0.19f,0.08f,0.10f),Iron);
                }
                mesh=b.ToMesh(key,true);var poses=new Matrix4x4[10];
                for(int i=0;i<poses.Length;i++)poses[i]=Matrix4x4.TRS(bind[i],Quaternion.identity,Vector3.one).inverse;
                mesh.bindposes=poses;Meshes[key]=mesh;
            }
            var renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=opaqueMaterial;
            renderer.bones=bones;renderer.rootBone=bones[0];renderer.updateWhenOffscreen=false;
            renderer.localBounds=new Bounds(new Vector3(0,0.92f,0),new Vector3(1.3f,2.1f,1.3f));
            if(collidable){var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,0.88f,0);collider.height=1.76f;collider.radius=0.23f;}
            WorldArt driver=EnsureRunner(parent);
            var rig=new PersonRig{root=root.transform,bones=bones,previous=position,phase=variant*0.73f,identity=root};
            driver.people.Add(rig);driver.personLookup[root]=rig;
            return root;
        }

        public static void SetPersonActivity(GameObject person, bool walking, bool working)
        {
            if(runner==null||person==null)return;PersonRig rig;
            if(runner.personLookup.TryGetValue(person,out rig))rig.mode=working?3:walking?2:1;
        }
        public static void SetAnimationSpeed(float value) { animationSpeed=Mathf.Clamp(value,0f,3f); }

        private void Update()
        {
            if(animationSpeed<=0f)return;
            float dt=Time.deltaTime;clock+=dt*animationSpeed;
            if(waterMaterial!=null)waterMaterial.SetTextureOffset(waterMaterial.HasProperty("_BaseMap")?"_BaseMap":"_MainTex",new Vector2(0,-clock*0.024f));
            for(int i=rotors.Count-1;i>=0;i--)
            {
                if(rotors[i].pivot==null){rotors.RemoveAt(i);continue;}
                rotors[i].pivot.Rotate(0,0,-rotors[i].rate*dt*animationSpeed,Space.Self);
            }
            for(int i=floats.Count-1;i>=0;i--)
            {
                FloatRig f=floats[i];if(f.pivot==null){floats.RemoveAt(i);continue;}
                f.pivot.localPosition=f.origin+Vector3.up*(Mathf.Sin(clock*1.8f+f.phase)*0.018f);
            }
            for(int i=carts.Count-1;i>=0;i--)
            {
                CartRig c=carts[i];if(c.root==null){carts.RemoveAt(i);continue;}
                float distance=Vector3.Distance(c.root.position,c.previous);c.previous=c.root.position;
                foreach(Transform wheel in c.wheels)if(wheel!=null)wheel.Rotate(distance/0.27f*Mathf.Rad2Deg,0,0,Space.Self);
            }
            for(int i=people.Count-1;i>=0;i--)
            {
                PersonRig p=people[i];if(p.root==null){personLookup.Remove(p.identity);people.RemoveAt(i);continue;}
                bool walking=p.mode==2||(p.mode==0&&(p.root.position-p.previous).sqrMagnitude>0.000005f);
                bool working=p.mode==3;p.previous=p.root.position;
                p.activityBlend=Mathf.MoveTowards(p.activityBlend,walking||working?1f:0f,dt*animationSpeed*5f);
                float stride=Mathf.Sin(clock*(working?4.4f:7.4f)+p.phase)*p.activityBlend;
                p.bones[0].localPosition=Vector3.up*(Mathf.Abs(stride)*(working?0.008f:0.028f));
                p.bones[1].localRotation=Quaternion.Euler(working?-45f+stride*22f:-stride*28f,0,working?-8f:-3f);
                p.bones[3].localRotation=Quaternion.Euler(working?-45f+stride*30f:stride*28f,0,working?8f:3f);
                p.bones[2].localRotation=Quaternion.Euler(working?-38f:Mathf.Max(0,stride)*-18f,0,0);
                p.bones[4].localRotation=Quaternion.Euler(working?-45f:Mathf.Max(0,-stride)*-18f,0,0);
                p.bones[5].localRotation=Quaternion.Euler(working?0:stride*30f,0,0);
                p.bones[7].localRotation=Quaternion.Euler(working?0:-stride*30f,0,0);
                p.bones[6].localRotation=Quaternion.Euler(working?0:Mathf.Max(0,-stride)*36f,0,0);
                p.bones[8].localRotation=Quaternion.Euler(working?0:Mathf.Max(0,stride)*36f,0,0);
                p.bones[9].localRotation=Quaternion.Euler(working?12f:0,Mathf.Sin(clock*0.6f+p.phase)*4f,0);
            }
        }
        private void OnDestroy(){if(runner==this)runner=null;}

        private static void BuildArchitecture(Builder b,string type,Color accent)
        {
            b.Box(new Vector3(0,0.07f,0),new Vector3(1.96f,0.14f,1.96f),StoneDark);
            if(type=="farm")
            {
                b.Box(new Vector3(0,0.15f,0),new Vector3(1.84f,0.10f,1.84f),Soil);
                for(int row=0;row<(ImportedEnvironment.Available?0:7);row++)for(int col=0;col<6;col++)
                {
                    Vector3 p=new Vector3(-0.78f+row*0.26f,0.24f,-0.76f+col*0.30f);
                    b.Beam(p,p+new Vector3(0.015f,0.30f,0),0.021f,0.021f,LeafLight);
                    b.Ellipsoid(p+new Vector3(0.02f,0.30f,0),new Vector3(0.045f,0.095f,0.032f),Crop,5,3);
                }
                Fence(b,new Vector3(-0.91f,0.21f,-0.92f),new Vector3(0.91f,0.21f,-0.92f));
                Fence(b,new Vector3(-0.92f,0.21f,-0.92f),new Vector3(-0.92f,0.21f,0.91f));
                b.Beam(new Vector3(0.78f,0.17f,0.72f),new Vector3(0.78f,1.1f,0.72f),0.05f,0.05f,Timber);
                b.Beam(new Vector3(0.55f,0.91f,0.72f),new Vector3(1.00f,0.91f,0.72f),0.045f,0.045f,Timber);
                b.Box(new Vector3(0.78f,0.80f,0.72f),new Vector3(0.25f,0.24f,0.035f),accent);return;
            }
            if(type=="barracks"||type=="muster_yard")
            {
                b.Box(new Vector3(-.22f,.34f,-.25f),new Vector3(1.28f,.40f,1.22f),Stone);
                b.Box(new Vector3(-.22f,.88f,-.43f),new Vector3(1.28f,.70f,.82f),Plaster);
                foreach(float x in new[]{-.84f,.40f})
                {
                    b.Box(new Vector3(x,.94f,-.26f),new Vector3(.10f,1.53f,1.24f),Timber);
                    b.Box(new Vector3(x,1.05f,.37f),new Vector3(.12f,1.75f,.13f),Timber);
                }
                b.Roof(new Vector3(-.22f,1.74f,-.25f),1.57f,1.50f,.49f,Hex("31595A"),Hex("487170"));
                b.Gable(new Vector3(-.22f,1.72f,-.25f),1.27f,1.24f,.43f,Plaster);
                b.Box(new Vector3(-.22f,.84f,.195f),new Vector3(.58f,1.02f,.055f),Timber);
                foreach(float x in new[]{-.80f,.35f})b.Box(new Vector3(x,1.04f,.195f),new Vector3(.16f,.31f,.045f),Iron);
                foreach(float z in new[]{-.74f,.34f})b.Box(new Vector3(.77f,.61f,z),new Vector3(.065f,.98f,.065f),Timber);
                b.Box(new Vector3(.77f,.76f,-.20f),new Vector3(.075f,.075f,1.14f),TimberLight);
                for(int i=0;i<4;i++)
                {
                    float z=-.64f+i*.25f;
                    b.Beam(new Vector3(.64f,.17f,z),new Vector3(.83f,1.25f,z),.025f,.025f,TimberLight);
                    b.Cone(new Vector3(.83f,1.25f,z),.16f,.044f,Iron,4);
                }
                b.Box(new Vector3(-.55f,.21f,.68f),new Vector3(.68f,.09f,.35f),TimberLight);
                b.Box(new Vector3(-.55f,.44f,.68f),new Vector3(.065f,.50f,.065f),Timber);
                b.Cylinder(new Vector3(-.55f,.68f,.68f),.17f,.045f,accent,10,Quaternion.Euler(90,0,0));
                b.Box(new Vector3(.47f,1.12f,.70f),new Vector3(.036f,1.94f,.036f),Timber);
                b.Box(new Vector3(.65f,1.80f,.70f),new Vector3(.35f,.44f,.025f),accent);
                b.Box(new Vector3(.65f,1.80f,.719f),new Vector3(.23f,.035f,.012f),Flax);
                Fence(b,new Vector3(-.90f,.14f,.89f),new Vector3(-.25f,.14f,.89f));
                return;
            }
            if(type=="mine")
            {
                AddRock(b,new Vector3(-0.45f,0.25f,-0.15f),0.80f);AddRock(b,new Vector3(0.35f,0.27f,-0.28f),0.85f);
                b.Box(new Vector3(0,0.65f,0.43f),new Vector3(0.88f,1.03f,0.13f),Iron);
                foreach(float x in new[]{-0.48f,0.48f})b.Box(new Vector3(x,0.65f,0.51f),new Vector3(0.14f,1.04f,0.20f),Timber);
                b.Box(new Vector3(0,1.19f,0.51f),new Vector3(1.18f,0.18f,0.22f),TimberLight);
                foreach(float x in new[]{-0.23f,0.23f})b.Box(new Vector3(x,0.19f,0.76f),new Vector3(0.04f,0.07f,0.45f),Iron);
                AddCrate(b,new Vector3(0.64f,0.14f,0.64f),0.4f);return;
            }
            if(type=="market")
            {
                foreach(int side in new[]{-1,1})
                {
                    float x=side*0.51f;
                    b.Box(new Vector3(x,0.59f,0),new Vector3(0.75f,0.65f,1.4f),Timber);
                    foreach(float z in new[]{-0.75f,0.75f})b.Box(new Vector3(x,1.01f,z),new Vector3(0.065f,1.8f,0.065f),Timber);
                    b.Roof(new Vector3(x,1.63f,0),0.92f,1.82f,0.21f,accent,Flax);
                    for(int i=0;i<3;i++)b.Ellipsoid(new Vector3(x,0.99f,-0.44f+i*0.44f),new Vector3(0.22f,0.17f,0.20f),i==1?Crop:Flax,7,4);
                }return;
            }
            if(type=="lumberyard")
            {
                foreach(float x in new[]{-0.73f,0.35f})foreach(float z in new[]{-0.64f,0.65f})
                    b.Box(new Vector3(x,0.73f,z),new Vector3(0.13f,1.22f,0.13f),Timber);
                b.Roof(new Vector3(-0.18f,1.35f,0),1.55f,1.85f,0.51f,Roof,RoofLight);
                for(int i=0;i<5;i++)b.Cylinder(new Vector3(-0.59f+i*0.19f,0.30f,0),0.085f,1.45f,TimberLight,7,Quaternion.Euler(90,0,0));
                b.Box(new Vector3(0.69f,0.53f,0),new Vector3(0.30f,0.10f,1.35f),TimberLight);
                foreach(float z in new[]{-0.52f,0.52f})b.Box(new Vector3(0.69f,0.33f,z),new Vector3(0.08f,0.40f,0.08f),Timber);
                b.Box(new Vector3(0.69f,0.61f,0),new Vector3(0.035f,0.08f,0.98f),Iron);return;
            }
            float width=type=="warehouse"?1.83f:type=="townhall"?1.76f:1.48f;
            float depth=type=="warehouse"?1.70f:1.37f;
            float wall=type=="mill"?1.55f:type=="townhall"?1.48f:1.18f;
            bool industrial=type=="smelter"||type=="toolsmith";
            b.Box(new Vector3(0,0.36f,0),new Vector3(width,0.44f,depth),Stone);
            b.Box(new Vector3(0,0.40f+wall*0.5f,0),new Vector3(width,wall-0.36f,depth),industrial?StoneDark:Plaster);
            float eave=wall+0.22f;
            b.Roof(new Vector3(0,eave,0),width+0.28f,depth+0.32f,type=="mill"?0.73f:0.58f,industrial?Hex("735F50"):Roof,RoofLight);
            // Gable infill, exposed studs, braces and ridge cap are silhouette-visible geometry.
            b.Gable(new Vector3(0,eave-0.01f,0),width,depth,type=="mill"?0.65f:0.51f,Plaster);
            foreach(float x in new[]{-width*0.45f,0f,width*0.45f})
                b.Box(new Vector3(x,wall*0.55f+0.26f,depth*0.505f),new Vector3(0.07f,wall-0.12f,0.065f),Timber);
            foreach(float y in new[]{0.54f,wall+0.13f})b.Box(new Vector3(0,y,depth*0.515f),new Vector3(width+0.03f,0.07f,0.08f),Timber);
            foreach(int side in new[]{-1,1})b.Beam(new Vector3(side*width*0.45f,0.62f,depth*0.52f),new Vector3(side*0.08f,wall+0.09f,depth*0.52f),0.052f,0.052f,Timber);
            float doorWidth=type=="warehouse"?0.83f:0.36f;
            b.Box(new Vector3(0,0.60f,depth*0.525f),new Vector3(doorWidth,0.88f,0.04f),type=="warehouse"?Timber:accent);
            for(int i=0;i<4;i++)b.Box(new Vector3(-doorWidth*0.38f+i*doorWidth*0.25f,0.60f,depth*0.55f),new Vector3(0.014f,0.83f,0.012f),TimberLight);
            if(type!="warehouse")foreach(float x in new[]{-0.48f,0.48f})
            {
                b.Box(new Vector3(x,0.91f,depth*0.535f),new Vector3(0.26f,0.31f,0.055f),Iron);
                b.Box(new Vector3(x,0.91f,depth*0.57f),new Vector3(0.18f,0.24f,0.026f),Hex("D8BC7C"));
                b.Box(new Vector3(x,0.91f,depth*0.59f),new Vector3(0.022f,0.25f,0.015f),Timber);
            }
            if(type=="bakery"||industrial)
            {
                Vector3 chimney=new Vector3(width*0.31f,1.53f,-depth*0.28f);
                b.Box(chimney,new Vector3(0.29f,1.5f,0.32f),type=="bakery"?Hex("AB8261"):StoneDark);
                b.Box(chimney+Vector3.up*0.77f,new Vector3(0.39f,0.12f,0.43f),Stone);
                b.Box(chimney+Vector3.up*0.84f,new Vector3(0.21f,0.025f,0.23f),Iron);
                for(int i=0;i<5;i++)b.Box(chimney+new Vector3(0,i*0.24f-0.45f,0.165f),new Vector3(0.30f,0.025f,0.012f),StoneDark);
            }
            if(type=="bakery")
            {
                b.Box(new Vector3(-0.56f,0.46f,0.85f),new Vector3(0.60f,0.12f,0.26f),TimberLight);
                for(int i=0;i<3;i++)b.Ellipsoid(new Vector3(-0.76f+i*0.20f,0.58f,0.85f),new Vector3(0.08f,0.075f,0.13f),Crop,7,4);
            }
            if(type=="warehouse")
            {
                AddCrate(b,new Vector3(-0.66f,0.14f,0.85f),0.45f);AddCrate(b,new Vector3(0.67f,0.14f,0.86f),0.44f);
                b.Beam(new Vector3(0,1.62f,0.81f),new Vector3(0,1.62f,1.06f),0.08f,0.08f,Timber);
                b.Beam(new Vector3(0,1.62f,1.02f),new Vector3(0,1.18f,1.02f),0.016f,0.016f,TimberLight);
            }
            if(industrial)
            {
                b.Box(new Vector3(-0.80f,0.42f,0.58f),new Vector3(0.25f,0.55f,0.37f),Timber);
                b.Box(new Vector3(-0.80f,0.75f,0.58f),new Vector3(0.43f,0.13f,0.24f),Iron);
                b.Box(new Vector3(0.72f,0.40f,0.60f),new Vector3(0.36f,0.25f,0.45f),Iron);
                if(type=="smelter")b.Box(new Vector3(0.72f,0.54f,0.60f),new Vector3(0.22f,0.025f,0.30f),Hex("CE884F"));
            }
            if(type=="townhall")AddBanner(b,new Vector3(0.79f,0.2f,0.78f),accent,ColorUtility.ToHtmlStringRGB(accent)==ColorUtility.ToHtmlStringRGB(RiverTeal)?"riverhold":"plain");
        }

        private static void AddBanner(Builder b,Vector3 p,Color cloth,string faction="riverhold")
        {
            b.Cylinder(p+Vector3.up*1.02f,0.028f,2.04f,Timber,6);
            b.Box(p+new Vector3(0.24f,1.73f,0),new Vector3(0.50f,0.55f,0.025f),cloth);
            Vector3 center=p+new Vector3(0.24f,1.73f,0.023f);
            if(faction=="plain")return;
            if(faction=="raiders")
            {
                b.Beam(center+new Vector3(-.12f,-.15f,0),center+new Vector3(.12f,.15f,0),.032f,.016f,Flax);
                b.Beam(center+new Vector3(.12f,-.15f,0),center+new Vector3(-.12f,.15f,0),.032f,.016f,Flax);
                b.Box(center,new Vector3(.16f,.06f,.02f),Iron);return;
            }
            if(faction=="ironvale")
            {
                b.Box(center+new Vector3(0,-0.12f,0),new Vector3(0.23f,0.035f,0.015f),Patina);
                b.Box(center+new Vector3(0,-0.06f,0),new Vector3(0.10f,0.11f,0.015f),Patina);
                b.Box(center+new Vector3(0,0.01f,0),new Vector3(0.31f,0.055f,0.015f),Patina);
                b.Box(center+new Vector3(0,0.13f,0),new Vector3(0.095f,0.075f,0.015f),Patina);return;
            }
            if(faction=="pinewatch")
            {
                b.Box(center+new Vector3(0,-0.02f,0),new Vector3(0.025f,0.22f,0.015f),Flax);
                foreach(Vector3 lobe in new[]{new Vector3(-0.085f,0.05f,0),new Vector3(0,0.13f,0),new Vector3(0.085f,0.05f,0)})
                    b.Ellipsoid(center+lobe,new Vector3(0.075f,0.075f,0.008f),Flax,7,4);
                for(int i=0;i<6;i++)
                {
                    float a=Mathf.PI+i*Mathf.PI/6f,c=Mathf.PI+(i+1)*Mathf.PI/6f;
                    b.Beam(center+new Vector3(Mathf.Cos(a)*0.14f,-0.09f+Mathf.Sin(a)*0.06f,0),center+new Vector3(Mathf.Cos(c)*0.14f,-0.09f+Mathf.Sin(c)*0.06f,0),0.019f,0.015f,Flax);
                }return;
            }
            if(faction=="tidemark")
            {
                b.Quad(center+new Vector3(-0.11f,-0.04f,0),center+new Vector3(0.13f,-0.04f,0),center+new Vector3(0.09f,0.18f,0),center+new Vector3(-0.10f,0.18f,0),Flax);
                b.Box(center+new Vector3(0,-0.13f,0),new Vector3(0.27f,0.025f,0.015f),Flax);return;
            }
            if(faction=="stonewake")
            {
                foreach(float x in new[]{-0.12f,0.12f})b.Box(center+new Vector3(x,-0.025f,0),new Vector3(0.055f,0.23f,0.015f),Flax);
                b.Box(center+new Vector3(0,0.075f,0),new Vector3(0.29f,0.055f,0.015f),Flax);
                foreach(float x in new[]{-0.11f,0f,0.11f})b.Box(center+new Vector3(x,0.13f,0),new Vector3(0.066f,0.065f,0.015f),Flax);return;
            }
            // One broad bridge arch over two river strokes: Riverhold's original civic motif.
            for(int i=0;i<7;i++)
            {
                float a=Mathf.PI*i/7f,c=Mathf.PI*(i+1)/7f;
                b.Beam(p+new Vector3(0.24f+Mathf.Cos(a)*0.145f,1.69f+Mathf.Sin(a)*0.15f,0.022f),
                    p+new Vector3(0.24f+Mathf.Cos(c)*0.145f,1.69f+Mathf.Sin(c)*0.15f,0.022f),0.032f,0.018f,Flax);
            }
            foreach(float y in new[]{1.63f,1.56f})b.Box(p+new Vector3(0.24f,y,0.026f),new Vector3(0.32f,0.025f,0.012f),Flax);
        }
        private static void AddTree(Builder b,Vector3 p,float height,int variant)
        {
            b.Cylinder(p+Vector3.up*(height*0.29f),0.10f,height*0.58f,Timber,6);
            if(variant==0)
            {
                b.Cone(p+Vector3.up*(height*0.23f),height*0.70f,height*0.29f,Leaf,7);
                b.Cone(p+Vector3.up*(height*0.54f),height*0.51f,height*0.20f,LeafLight,7);
            }
            else
            {
                b.Ellipsoid(p+new Vector3(-0.21f,height*0.65f,0),new Vector3(height*0.26f,height*0.34f,height*0.27f),Leaf,7,4);
                b.Ellipsoid(p+new Vector3(0.24f,height*0.78f,0.10f),new Vector3(height*0.25f,height*0.25f,height*0.23f),LeafLight,7,4);
            }
        }
        private static void AddRock(Builder b,Vector3 p,float size)
        {
            b.Ellipsoid(p+Vector3.up*size*0.35f,new Vector3(size,size*0.67f,size*0.77f),StoneDark,7,4);
        }
        private static void AddReeds(Builder b,Vector3 p,int phase)
        {
            for(int i=0;i<4;i++)b.Beam(p+new Vector3(i*0.06f,0,0),p+new Vector3(i*0.04f,0.38f+(i%2)*0.15f,0.03f),0.02f,0.02f,i%2==0?LeafLight:Crop);
        }
        private static void AddCrate(Builder b,Vector3 p,float size)
        {
            Vector3 c=p+Vector3.up*(size*0.5f);b.Box(c,new Vector3(size,size,size),TimberLight);
            foreach(float x in new[]{-size*0.35f,size*0.35f})b.Box(c+new Vector3(x,0,size*0.505f),new Vector3(size*0.09f,size*0.96f,0.02f),Timber);
            b.Beam(c+new Vector3(-size*0.43f,-size*0.4f,size*0.53f),c+new Vector3(size*0.43f,size*0.4f,size*0.53f),size*0.085f,0.024f,Timber);
        }
        private static void AddMule(Builder b,Vector3 p)
        {
            Color hide=Hex("8F826D");
            b.Ellipsoid(p+new Vector3(0,0.70f,0),new Vector3(0.25f,0.27f,0.49f),hide,8,5);
            b.Beam(p+new Vector3(0,0.74f,0.31f),p+new Vector3(0,1.02f,0.49f),0.21f,0.24f,hide);
            b.Ellipsoid(p+new Vector3(0,1.06f,0.58f),new Vector3(0.13f,0.17f,0.25f),hide,7,4);
            b.Box(p+new Vector3(0,1.0f,0.76f),new Vector3(0.21f,0.15f,0.15f),StoneDark);
            foreach(float x in new[]{-0.085f,0.085f})b.Ellipsoid(p+new Vector3(x,1.31f,0.54f),new Vector3(0.038f,0.18f,0.05f),hide,5,3);
            foreach(float x in new[]{-0.16f,0.16f})foreach(float z in new[]{-0.31f,0.30f})
            {
                b.Beam(p+new Vector3(x,0.72f,z),p+new Vector3(x,0.11f,z+0.025f),0.075f,0.085f,hide);
                b.Box(p+new Vector3(x,0.09f,z+0.045f),new Vector3(0.10f,0.12f,0.13f),Iron);
            }
            b.Box(p+new Vector3(0,0.91f,-0.03f),new Vector3(0.52f,0.065f,0.13f),Timber);
            b.Beam(p+new Vector3(0,0.75f,-0.44f),p+new Vector3(0,0.34f,-0.57f),0.045f,0.045f,Timber);
        }
        private static void Fence(Builder b,Vector3 a,Vector3 c)
        {
            for(int i=0;i<4;i++)b.Box(Vector3.Lerp(a,c,i/3f)+Vector3.up*0.23f,new Vector3(0.055f,0.46f,0.055f),Timber);
            foreach(float y in new[]{0.16f,0.35f})b.Beam(a+Vector3.up*y,c+Vector3.up*y,0.035f,0.035f,TimberLight);
        }
        private static void Road(Builder b,Vector3 a,Vector3 c,float width)
        {
            width*=.74f;
            Vector3 delta=c-a;float length=delta.magnitude;Vector3 forward=delta.normalized;
            Quaternion rotation=Quaternion.LookRotation(delta);Vector3 right=Vector3.Cross(Vector3.up,forward);
            Vector3 middle=(a+c)*.5f+Vector3.up*.028f;
            b.Box(middle,new Vector3(width+.10f,.042f,length+width*.4f),Hex("8D896B"),rotation);
            b.Box(middle+Vector3.up*.024f,new Vector3(width*.80f,.013f,length+width*.15f),Hex("A99F84"),rotation);
            b.Cylinder(a+Vector3.up*.025f,(width+.10f)*.50f,.035f,Hex("8D896B"),8);
            b.Cylinder(c+Vector3.up*.025f,(width+.10f)*.50f,.035f,Hex("8D896B"),8);
            int steps=Mathf.Max(1,Mathf.FloorToInt(length/.40f));
            for(int i=0;i<steps;i++)
            {
                Vector3 p=a+forward*((i+.5f)*length/steps)+Vector3.up*.065f;
                for(int side=-1;side<=1;side+=2)
                    b.Box(p+right*(side*width*.26f+((i%2)*.035f)),new Vector3(width*.36f,.018f,.27f),i%3==0?Hex("B6AD96"):Hex("999780"),rotation);
            }

        }
        public static float GroundHeight(float x,float z)
        {
            float edgeX=Mathf.SmoothStep(0,1,Mathf.InverseLerp(36.5f,44f,Mathf.Abs(x)));
            float edgeZ=Mathf.SmoothStep(0,1,Mathf.InverseLerp(25.8f,31f,Mathf.Abs(z)));
            float edge=Mathf.Max(edgeX,edgeZ);
            float banks=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.3f,10f,Mathf.Abs(x)));
            // The Lower Ridge road crosses z=25.3: flatten its approaches even on the rim.
            float route=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.0f,2.4f,Mathf.Abs(z-RidgePosition.z)));
            float clearing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5f,8f,Vector2.Distance(new Vector2(x,z),new Vector2(40.7f,12.1f))));
            float noise=.35f+Mathf.PerlinNoise(x*.066f+5,z*.075f+9)*.90f;
            return edge*noise*banks*(1f-route)*clearing;

        }
        private static float Range(System.Random random,float min,float max){return min+(float)random.NextDouble()*(max-min);}
        private static Color Hex(string hex){Color c;ColorUtility.TryParseHtmlString("#"+hex,out c);return c;}
        private static WorldArt EnsureRunner(Transform parent)
        {
            if(runner==null)
            {
                var go=new GameObject("World art motion");go.transform.SetParent(parent,false);runner=go.AddComponent<WorldArt>();
            }return runner;
        }
        private static GameObject MakeRoot(string name,Vector3 position,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;return go;
        }
        private static Mesh Cached(string key,Builder b)
        {
            Mesh mesh;if(Meshes.TryGetValue(key,out mesh)&&mesh!=null)return mesh;
            mesh=b.ToMesh(key);Meshes[key]=mesh;return mesh;
        }
        internal static GameObject Render(string name,Mesh mesh,Transform parent,Material material=null)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material??opaqueMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return go;
        }
        private static void EnsureResources()
        {
            if(opaqueMaterial!=null)return;
            PaletteSlots.Clear();Meshes.Clear();nextPaletteSlot=0;
            paletteTexture=new Texture2D(16,16,TextureFormat.RGBA32,false,false){name="Original Living Empires palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[256];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
            paletteTexture.SetPixels(pixels);paletteTexture.Apply(false,false);
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)shader=Shader.Find("Unlit/Texture");
            if(shader==null)throw new InvalidOperationException("WorldArt needs URP/Lit, Standard, or Unlit/Texture retained in the build.");
            opaqueMaterial=new Material(shader){name="Living Empires · original shared palette",enableInstancing=true};
            SetSurface(opaqueMaterial,paletteTexture);
            waterTexture=new Texture2D(32,64,TextureFormat.RGBA32,false,false){name="Original river brush texture",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
            for(int y=0;y<64;y++)for(int x=0;x<32;x++)
            {
                float wave=Mathf.Sin(y*Mathf.PI/8f+Mathf.Sin(x*Mathf.PI/16f)*1.2f)*0.5f+0.5f;
                Color color=Color.Lerp(Hex("3E7071"),Hex("63918B"),wave*0.38f);
                waterTexture.SetPixel(x,y,color);
            }
            waterTexture.Apply(false,false);
            waterMaterial=new Material(shader){name="Living Empires · flowing river"};SetSurface(waterMaterial,waterTexture);
            if(waterMaterial.HasProperty("_Smoothness"))waterMaterial.SetFloat("_Smoothness",0.32f);
        }
        private static void SetSurface(Material material,Texture texture)
        {
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",0.10f);
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",0.10f);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0f);
        }
        private static Vector2 PaletteUV(Color color)
        {
            Color32 key=color;int slot;
            if(!PaletteSlots.TryGetValue(key,out slot))
            {
                if(nextPaletteSlot>=256)throw new InvalidOperationException("WorldArt palette exceeded 256 colors; use the shared faction palette.");
                slot=nextPaletteSlot++;PaletteSlots.Add(key,slot);paletteTexture.SetPixel(slot%16,slot/16,color);paletteTexture.Apply(false,false);
            }
            return new Vector2((slot%16+0.5f)/16f,(slot/16+0.5f)/16f);
        }

        /// <summary>Original cached pictograms. All goods and build choices use shapes, never font glyphs.</summary>
        public static Sprite Icon(string id,int size=64)
        {
            size=Mathf.Clamp(size,16,256);id=(id??"house").ToLowerInvariant();
            string key=id+":"+size;Sprite cached;
            if(Icons.TryGetValue(key,out cached)&&cached!=null)return cached;
            var r=new IconCanvas(size);Color shadow=Hex("213C36"),light=Flax,dark=Iron;
            r.Ellipse(32,10,23,5,new Color(0.08f,0.12f,0.10f,0.25f));
            if(id=="barracks"||id=="muster_yard"||id=="spear"||id=="spearman")
            {
                r.Line(13,10,48,56,3.3f,TimberLight);r.Line(50,10,17,56,3.3f,TimberLight);
                r.Polygon(Stone,V(42,51),V(54,61),V(51,46));r.Polygon(Stone,V(13,46),V(11,61),V(24,51));
                r.Polygon(Timber,V(18,42),V(46,42),V(43,20),V(32,10),V(21,20));
                r.Polygon(RiverTeal,V(21,39),V(43,39),V(40,22),V(32,15),V(24,22));
                r.Line(26,30,38,30,3,Flax);r.Ellipse(32,29,4,4,Stone);
            }
            else if(id=="archer"||id=="bow")
            {
                r.Line(18,10,18,56,1.5f,Flax);
                r.Line(18,56,34,44,3,TimberLight);r.Line(34,44,39,32,3,TimberLight);
                r.Line(39,32,34,20,3,TimberLight);r.Line(34,20,18,10,3,TimberLight);
                r.Line(10,32,51,32,2.2f,TimberLight);r.Polygon(Stone,V(48,37),V(58,32),V(48,27));
            }
            else if(id=="grain"||id=="farm")
            {
                if(id=="farm")
                {
                    r.Polygon(Soil,V(7,13),V(38,6),V(58,21),V(28,30));
                    for(int i=0;i<4;i++)r.Line(13+i*8,13+i*0.2f,31+i*7,25+i*0.2f,2,TimberLight);
                }
                for(int k=0;k<3;k++)
                {
                    float x=23+k*9,y=20+(k==1?5:0);r.Line(x,12,x+2,y+27,2.3f,Ochre);
                    for(int i=0;i<4;i++)
                    {r.Ellipse(x-3,y+i*5,4,2.5f,Crop);r.Ellipse(x+5,y+i*5+2,4,2.5f,Flax);}
                }
            }
            else if(id=="flour")
            {
                r.Polygon(Ochre,V(18,13),V(13,23),V(21,45),V(20,53),V(43,53),V(42,45),V(51,23),V(46,13));
                r.Polygon(Flax,V(21,16),V(17,25),V(25,44),V(38,44),V(46,24),V(43,16));
                r.Line(21,45,42,45,4,Timber);r.Line(31,23,31,36,2,Ochre);
                for(int i=0;i<3;i++){r.Ellipse(27,27+i*4,3,2,Crop);r.Ellipse(35,29+i*4,3,2,Crop);}
                r.Ellipse(46,12,9,3,light);
            }
            else if(id=="bread")
            {
                r.Ellipse(32,29,24,17,Timber);r.Ellipse(32,32,24,16,Ochre);r.Ellipse(32,35,21,12,Crop);
                for(int i=0;i<3;i++)r.Line(18+i*11,31,24+i*11,41,3,Flax);
            }
            else if(id=="wood")
            {
                for(int i=0;i<3;i++)
                {float y=18+i*12;r.Polygon(Timber,V(12,y),V(15,y+9),V(49,y+10),V(54,y+2));r.Ellipse(15,y+5,7,6,TimberLight);r.Ellipse(15,y+5,4,3,Ochre);r.Ellipse(15,y+5,2,1.5f,Timber);r.Line(25,y+6,47,y+7,1,TimberLight);}
            }
            else if(id=="ore"||id=="stone")
            {
                Color baseColor=id=="ore"?Hex("5A7277"):StoneDark;
                r.Polygon(baseColor,V(9,18),V(15,41),V(35,51),V(52,38),V(57,17),V(35,11));
                r.Polygon(id=="ore"?Patina:Stone,V(15,41),V(35,51),V(39,31),V(22,24));
                r.Polygon(id=="ore"?Hex("8DA6A3"):Flax,V(35,51),V(52,38),V(39,31));
                r.Line(22,24,35,11,2,baseColor);
                if(id=="ore"){r.Line(17,34,30,40,3,Ochre);r.Line(30,40,35,34,3,Ochre);}
                else {r.Line(13,22,46,18,2,Timber);r.Line(39,31,50,20,2,Timber);}
            }
            else if(id=="iron")
            {
                for(int i=0;i<3;i++)
                {
                    float y=13+i*11;r.Polygon(dark,V(10,y),V(49,y),V(55,y+7),V(51,y+12),V(18,y+12),V(10,y+5));
                    r.Polygon(Patina,V(12,y+6),V(20,y+12),V(50,y+12),V(47,y+6));r.Line(14,y+3,47,y+3,2,StoneDark);
                }
            }
            else if(id=="tools")
            {
                r.Line(18,13,42,48,7,Timber);r.Line(19,13,41,46,3,TimberLight);
                r.Polygon(Patina,V(27,47),V(34,55),V(55,42),V(49,34));r.Line(15,44,47,14,4,Iron);
                r.Line(14,44,12,52,4,Iron);r.Line(14,44,22,47,4,Iron);r.Ellipse(45,15,4,4,Patina);
            }
            else if(id=="mine")
            {
                r.Polygon(StoneDark,V(5,13),V(16,46),V(30,55),V(52,42),V(60,13));
                r.Polygon(Stone,V(16,46),V(30,55),V(37,39),V(20,30));r.Rect(21,13,24,25,dark);
                r.Line(19,13,19,38,5,Timber);r.Line(47,13,47,38,5,Timber);r.Line(17,39,49,39,6,TimberLight);
                r.Line(25,6,28,20,2,Patina);r.Line(40,6,37,20,2,Patina);
            }
            else
            {
                bool wide=id=="warehouse"||id=="market";float left=wide?9:15,right=wide?55:49;
                r.Rect(left,12,right-left,28,Stone);r.Rect(left,12,right-left,7,StoneDark);
                r.Polygon(Roof,V(left-4,37),V(32,57),V(right+4,37));
                r.Polygon(RoofLight,V(32,57),V(right+4,37),V(32,39));
                r.Line(left+1,19,left+1,36,3,Timber);r.Line(right-1,19,right-1,36,3,Timber);
                r.Rect(27,12,10,18,RiverTeal);r.Rect(19,25,6,8,Flax);r.Rect(41,25,6,8,Flax);
                if(id=="mill")
                {
                    r.Rect(27,17,10,35,Stone);
                    foreach(float angle in new[]{0f,90f,180f,270f})
                    {
                        float a=angle*Mathf.Deg2Rad;Vector2 end=V(33+Mathf.Sin(a)*24,35+Mathf.Cos(a)*24);
                        r.Line(33,35,end.x,end.y,4,Timber);Vector2 t=V(Mathf.Cos(a)*6,-Mathf.Sin(a)*6);
                        Vector2 mid=Vector2.Lerp(V(33,35),end,0.45f);r.Polygon(Flax,mid,end,end+t,mid+t);
                    }
                    r.Ellipse(33,35,4,4,Ochre);
                }
                else if(id=="bakery"||id=="smelter"||id=="forge"||id=="toolsmith")
                {
                    r.Rect(43,37,7,20,StoneDark);r.Rect(41,54,11,4,Stone);
                    if(id=="bakery")
                    {r.Ellipse(32,24,14,9,Ochre);r.Ellipse(32,26,12,7,Crop);r.Line(26,22,29,30,2,Flax);r.Line(34,22,37,30,2,Flax);}
                    else if(id=="toolsmith")
                    {r.Line(21,14,40,38,4,TimberLight);r.Polygon(Patina,V(30,36),V(36,43),V(49,33),V(43,26));}
                    else
                    {r.Rect(23,14,18,18,Iron);r.Polygon(Ochre,V(26,15),V(25,23),V(30,20),V(33,30),V(39,21),V(37,15));}
                }
                else if(id=="warehouse")
                {r.Rect(21,12,23,22,Timber);r.Rect(11,11,14,14,TimberLight);r.Line(12,12,23,23,2,Timber);r.Line(23,12,12,23,2,Timber);r.Rect(45,10,13,14,Ochre);}
                else if(id=="lumberyard")
                {r.Rect(20,15,23,20,shadow);for(int i=0;i<3;i++){r.Rect(12,13+i*7,37,5,Timber);r.Ellipse(13,16+i*7,4,3,TimberLight);} }
                else if(id=="market")
                {for(int i=0;i<5;i++)r.Rect(9+i*9,29,9,11,i%2==0?RiverTeal:Flax);r.Rect(11,18,43,7,TimberLight);r.Ellipse(18,25,5,4,Crop);r.Ellipse(44,25,5,4,LeafLight);}
                else if(id=="townhall"||id=="hall")
                {r.Line(48,34,48,61,2,Timber);r.Rect(48,49,11,10,RiverTeal);r.Line(51,52,56,52,1.4f,Flax);}
            }
            Texture2D texture=r.Finish("Living Empires icon · "+key);
            Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(0.5f,0.5f),size);
            sprite.name="Original "+id+" pictogram";Icons[key]=sprite;return sprite;
        }
        private static Vector2 V(float x,float y){return new Vector2(x,y);}
        private sealed class IconCanvas
        {
            private readonly int size;private readonly Color32[] pixels;private readonly float scale;
            public IconCanvas(int value){size=value;scale=size/64f;pixels=new Color32[size*size];}
            public void Rect(float x,float y,float w,float h,Color color){Polygon(color,V(x,y),V(x+w,y),V(x+w,y+h),V(x,y+h));}
            public void Ellipse(float x,float y,float rx,float ry,Color color)
            {
                for(int py=Mathf.Max(0,Mathf.FloorToInt((y-ry)*scale));py<Mathf.Min(size,Mathf.CeilToInt((y+ry)*scale));py++)
                for(int px=Mathf.Max(0,Mathf.FloorToInt((x-rx)*scale));px<Mathf.Min(size,Mathf.CeilToInt((x+rx)*scale));px++)
                {float u=((px+0.5f)/scale-x)/rx,v=((py+0.5f)/scale-y)/ry;if(u*u+v*v<=1f)pixels[py*size+px]=color;}
            }
            public void Line(float ax,float ay,float bx,float by,float width,Color color)
            {
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(V(ax,ay),V(bx,by))*scale));
                for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(V(ax,ay),V(bx,by),i/(float)steps);Ellipse(p.x,p.y,width/2,width/2,color);}
            }
            public void Polygon(Color color,params Vector2[] points)
            {
                float minX=64,minY=64,maxX=0,maxY=0;
                foreach(Vector2 p in points){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
                for(int y=Mathf.Max(0,Mathf.FloorToInt(minY*scale));y<Mathf.Min(size,Mathf.CeilToInt(maxY*scale));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(minX*scale));x<Mathf.Min(size,Mathf.CeilToInt(maxX*scale));x++)
                {
                    float px=(x+0.5f)/scale,py=(y+0.5f)/scale;bool inside=false;
                    for(int i=0,j=points.Length-1;i<points.Length;j=i++)
                    {Vector2 a=points[i],b=points[j];if((a.y>py)!=(b.y>py)&&px<(b.x-a.x)*(py-a.y)/(b.y-a.y)+a.x)inside=!inside;}
                    if(inside)pixels[y*size+x]=color;
                }
            }
            public Texture2D Finish(string name)
            {
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                texture.SetPixels32(pixels);texture.Apply(false,false);return texture;
            }
        }

        /// <summary>Flat-shaded mesh authoring. Palette UVs avoid one draw call per colored detail.</summary>
        internal sealed class Builder
        {
            public int Bone;
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Vector3> normals=new List<Vector3>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<int> triangles=new List<int>();
            private readonly List<BoneWeight> weights=new List<BoneWeight>();
            private void Vertex(Vector3 p,Vector3 normal,Vector2 tex)
            {vertices.Add(p);normals.Add(normal);uv.Add(tex);weights.Add(new BoneWeight{boneIndex0=Bone,weight0=1});}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                int n=vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;Vector2 t=PaletteUV(color);
                Vertex(a,normal,t);Vertex(b,normal,t);Vertex(c,normal,t);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            {
                int n=vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;Vector2 t=PaletteUV(color);
                Vertex(a,normal,t);Vertex(b,normal,t);Vertex(c,normal,t);Vertex(d,normal,t);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            public void Box(Vector3 center,Vector3 size,Color color){Box(center,size,color,Quaternion.identity);}
            public void Box(Vector3 center,Vector3 size,Color color,Quaternion rotation)
            {
                Vector3 h=size*0.5f;var p=new Vector3[8];
                for(int i=0;i<8;i++)p[i]=center+rotation*new Vector3((i&1)==0?-h.x:h.x,(i&2)==0?-h.y:h.y,(i&4)==0?-h.z:h.z);
                Quad(p[4],p[5],p[7],p[6],color);Quad(p[1],p[0],p[2],p[3],color);
                Quad(p[6],p[7],p[3],p[2],color);Quad(p[0],p[1],p[5],p[4],color);
                Quad(p[0],p[4],p[6],p[2],color);Quad(p[5],p[1],p[3],p[7],color);
            }
            public void Beam(Vector3 a,Vector3 b,float width,float depth,Color color)
            {Box((a+b)*0.5f,new Vector3(width,Vector3.Distance(a,b),depth),color,Quaternion.FromToRotation(Vector3.up,b-a));}
            public void Roof(Vector3 eave,float width,float depth,float rise,Color left,Color right)
            {
                Vector3 a=eave+new Vector3(-width/2,0,-depth/2),b=eave+new Vector3(width/2,0,-depth/2);
                Vector3 c=eave+new Vector3(-width/2,0,depth/2),d=eave+new Vector3(width/2,0,depth/2);
                Vector3 r0=eave+new Vector3(0,rise,-depth/2),r1=eave+new Vector3(0,rise,depth/2);
                Quad(c,r1,r0,a,left);Quad(r1,d,b,r0,right);
                Beam(r0,r1,0.09f,0.09f,Timber);
                foreach(float z in new[]{-depth/2,depth/2})
                {Beam(eave+new Vector3(-width/2,0,z),eave+new Vector3(0,rise,z),0.07f,0.07f,Timber);Beam(eave+new Vector3(0,rise,z),eave+new Vector3(width/2,0,z),0.07f,0.07f,Timber);}
                // Three small roof courses catch light without individual renderer objects.
                for(int i=1;i<=3;i++)foreach(int side in new[]{-1,1})
                {
                    float f=i/4f;Vector3 p=eave+new Vector3(side*width*0.5f*f,rise*(1-f)+0.01f,0);
                    Box(p,new Vector3(0.028f,0.028f,depth),i%2==0?left:right);
                }
            }
            public void Gable(Vector3 p,float width,float depth,float rise,Color color)
            {
                Triangle(p+new Vector3(-width/2,0,depth/2),p+new Vector3(width/2,0,depth/2),p+new Vector3(0,rise,depth/2),color);
                Triangle(p+new Vector3(width/2,0,-depth/2),p+new Vector3(-width/2,0,-depth/2),p+new Vector3(0,rise,-depth/2),color);
            }
            public void Cylinder(Vector3 p,float radius,float height,Color color,int sides=8,Quaternion? rotation=null)
            {
                Quaternion r=rotation??Quaternion.identity;Vector3 up=r*Vector3.up*(height/2);
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                    Vector3 va=r*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),vb=r*new Vector3(Mathf.Cos(c)*radius,0,Mathf.Sin(c)*radius);
                    Quad(p+va-up,p+va+up,p+vb+up,p+vb-up,color);
                    Triangle(p+up,p+vb+up,p+va+up,color);Triangle(p-up,p+va-up,p+vb-up,color);
                }
            }
            public void Cone(Vector3 bottom,float height,float radius,Color color,int sides)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                    Vector3 va=bottom+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),vb=bottom+new Vector3(Mathf.Cos(c)*radius,0,Mathf.Sin(c)*radius);
                    Triangle(va,bottom+Vector3.up*height,vb,color);Triangle(bottom,va,vb,color);
                }
            }
            public void Ellipsoid(Vector3 center,Vector3 radius,Color color,int slices,int rings)
            {
                for(int y=0;y<rings;y++)for(int x=0;x<slices;x++)
                {
                    float v0=Mathf.PI*y/rings,v1=Mathf.PI*(y+1)/rings,u0=Mathf.PI*2*x/slices,u1=Mathf.PI*2*(x+1)/slices;
                    Vector3 a=Sphere(center,radius,u0,v0),b=Sphere(center,radius,u1,v0),c=Sphere(center,radius,u1,v1),d=Sphere(center,radius,u0,v1);
                    if(y==0)Triangle(a,c,d,color);else if(y==rings-1)Triangle(a,b,c,color);else Quad(a,b,c,d,color);
                }
            }
            private static Vector3 Sphere(Vector3 c,Vector3 r,float u,float v)
            {return c+Vector3.Scale(r,new Vector3(Mathf.Cos(u)*Mathf.Sin(v),Mathf.Cos(v),Mathf.Sin(u)*Mathf.Sin(v)));}
            public void Arch(Vector3 spring,float halfSpan,float height,float thick,float depth,Color color,int steps)
            {
                for(int i=0;i<steps;i++)
                {
                    float a=Mathf.PI*i/steps,b=Mathf.PI*(i+1)/steps;
                    Vector3 ia=spring+new Vector3(Mathf.Cos(a)*halfSpan,Mathf.Sin(a)*height,0),ib=spring+new Vector3(Mathf.Cos(b)*halfSpan,Mathf.Sin(b)*height,0);
                    Vector3 oa=spring+new Vector3(Mathf.Cos(a)*(halfSpan+thick),Mathf.Sin(a)*(height+thick),0),ob=spring+new Vector3(Mathf.Cos(b)*(halfSpan+thick),Mathf.Sin(b)*(height+thick),0);
                    Vector3 z=Vector3.forward*(depth/2);Color c=i%3==0?StoneDark:color;
                    Quad(ia+z,oa+z,ob+z,ib+z,c);Quad(ib-z,ob-z,oa-z,ia-z,c);
                    Quad(oa+z,oa-z,ob-z,ob+z,c);Quad(ib+z,ib-z,ia-z,ia+z,c);
                }
            }
            public void Hull(Vector3 p,float length,float width,float height,Color color)
            {
                Vector3[] rim={new Vector3(-length/2,0,0),new Vector3(-length*0.35f,0,-width/2),new Vector3(length*0.35f,0,-width/2),new Vector3(length/2,0,0),new Vector3(length*0.35f,0,width/2),new Vector3(-length*0.35f,0,width/2)};
                for(int i=0;i<6;i++)
                {
                    Vector3 a=rim[i],b=rim[(i+1)%6];Vector3 la=new Vector3(a.x*0.8f,-height,a.z*0.72f),lb=new Vector3(b.x*0.8f,-height,b.z*0.72f);
                    Quad(p+a,p+b,p+lb,p+la,color);Triangle(p,p+b,p+a,TimberLight);
                }
            }
            public Mesh ToMesh(string name,bool skin=false)
            {
                var mesh=new Mesh{name=name};if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                if(skin)mesh.boneWeights=weights.ToArray();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
