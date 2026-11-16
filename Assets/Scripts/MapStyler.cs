using UnityEngine;

public class MapStyler : MonoBehaviour
{
    [Header("Scenery")]
    public Transform pathRoot;
    public int trees = 45;
    public int rocks = 25;
    public float pathMargin = 5f;
    public int seed = 7;

    [Header("Border wall")]
    public bool buildWall = true;
    public float wallHeight = 3f;
    public float wallThickness = 1.5f;
    public Color skyColor = new Color(0.55f, 0.78f, 0.95f);

    void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) return;

        Material grass = r.material;
        grass.mainTexture = MakeGrassTexture();
        grass.color = Color.white;
        grass.mainTextureScale = new Vector2(10, 10);
        if (grass.HasProperty("_Smoothness")) grass.SetFloat("_Smoothness", 0f);

        // sky-blue background instead of grey
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
        }

        if (pathRoot == null)
        {
            GameObject p = GameObject.Find("Path");
            if (p != null) pathRoot = p.transform;
        }

        Transform root = new GameObject("Scenery").transform;
        Scatter(r.bounds, grass, root);
        if (buildWall) BuildWall(r.bounds, grass, root);
    }

    // ---------- textures ----------
    Texture2D MakeGrassTexture()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        System.Random rng = new System.Random(seed);
        Color dark = new Color(0.16f, 0.42f, 0.14f);
        Color light = new Color(0.36f, 0.66f, 0.20f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float wave = 0.5f + 0.25f * Mathf.Sin(x * Mathf.PI * 6f / size) * Mathf.Sin(y * Mathf.PI * 6f / size);
                float speck = (float)rng.NextDouble() * 0.35f;
                tex.SetPixel(x, y, Color.Lerp(dark, light, Mathf.Clamp01(wave + speck - 0.15f)));
            }
        }

        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    Texture2D MakeBrickTexture()
    {
        const int size = 64, brickW = 16, brickH = 8;
        int rows = size / brickH;
        int cols = size / brickW;

        System.Random rng = new System.Random(seed + 1);
        float[,] tint = new float[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                tint[r, c] = 0.8f + (float)rng.NextDouble() * 0.35f;

        Color brick = new Color(0.62f, 0.30f, 0.22f);
        Color mortar = new Color(0.74f, 0.72f, 0.68f);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int y = 0; y < size; y++)
        {
            int row = y / brickH;
            int offset = (row % 2 == 0) ? 0 : brickW / 2;
            for (int x = 0; x < size; x++)
            {
                int xx = (x + offset) % size;
                bool isMortar = (xx % brickW == 0) || (y % brickH == 0);
                if (isMortar)
                {
                    tex.SetPixel(x, y, mortar);
                }
                else
                {
                    float noise = ((float)rng.NextDouble() - 0.5f) * 0.08f;
                    float t = tint[row, xx / brickW] + noise;
                    tex.SetPixel(x, y, new Color(brick.r * t, brick.g * t, brick.b * t));
                }
            }
        }

        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    // ---------- helpers ----------
    Material Mat(Material source, Color c)
    {
        Material m = new Material(source);
        m.mainTexture = null;
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        return m;
    }

    Material BrickMat(Material source, Texture2D tex, float tileU, float tileV)
    {
        Material m = new Material(source);
        m.mainTexture = tex;
        m.color = Color.white;
        m.mainTextureScale = new Vector2(Mathf.Max(0.5f, tileU), Mathf.Max(0.5f, tileV));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        return m;
    }

    GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = m;
        return go;
    }

    // ---------- border wall ----------
    void BuildWall(Bounds b, Material grass, Transform root)
    {
        float h = wallHeight;
        float t = wallThickness;
        float baseY = b.max.y;
        float cy = baseY + h * 0.5f;

        float width = b.size.x;
        float depth = b.size.z;
        float fullW = width + 2f * t;
        float fullD = depth + 2f * t;

        Texture2D brickTex = MakeBrickTexture();   // one bricks texture for the whole wall
        float tileH = h / 6f;                      // brick tile is about 8 wide x 6 tall (world units)

        Material northSouth = BrickMat(grass, brickTex, fullW / 8f, tileH);
        Material eastWest = BrickMat(grass, brickTex, fullD / 8f, tileH);

        // four wall segments (just outside the ground so the whole ground stays visible)
        Prim(PrimitiveType.Cube, root, new Vector3(b.center.x, cy, b.max.z + t * 0.5f), new Vector3(fullW, h, t), northSouth);
        Prim(PrimitiveType.Cube, root, new Vector3(b.center.x, cy, b.min.z - t * 0.5f), new Vector3(fullW, h, t), northSouth);
        Prim(PrimitiveType.Cube, root, new Vector3(b.max.x + t * 0.5f, cy, b.center.z), new Vector3(t, h, depth), eastWest);
        Prim(PrimitiveType.Cube, root, new Vector3(b.min.x - t * 0.5f, cy, b.center.z), new Vector3(t, h, depth), eastWest);

        // battlements along the top
        Material merlon = BrickMat(grass, brickTex, 0.3f, 0.15f);
        float step = 2.4f;
        float my = baseY + h + 0.45f;

        for (float x = b.min.x - t; x <= b.max.x + t; x += step)
        {
            Prim(PrimitiveType.Cube, root, new Vector3(x, my, b.max.z + t * 0.5f), new Vector3(1.2f, 0.9f, t), merlon);
            Prim(PrimitiveType.Cube, root, new Vector3(x, my, b.min.z - t * 0.5f), new Vector3(1.2f, 0.9f, t), merlon);
        }
        for (float z = b.min.z; z <= b.max.z; z += step)
        {
            Prim(PrimitiveType.Cube, root, new Vector3(b.max.x + t * 0.5f, my, z), new Vector3(t, 0.9f, 1.2f), merlon);
            Prim(PrimitiveType.Cube, root, new Vector3(b.min.x - t * 0.5f, my, z), new Vector3(t, 0.9f, 1.2f), merlon);
        }

        // corner towers with red roofs
        float towerH = h + 2.5f;
        Material towerMat = BrickMat(grass, brickTex, 1.4f, towerH / 6f);
        Material roofMat = Mat(grass, new Color(0.55f, 0.12f, 0.12f));

        float[] xs = { b.min.x - t * 0.5f, b.max.x + t * 0.5f };
        float[] zs = { b.min.z - t * 0.5f, b.max.z + t * 0.5f };
        foreach (float x in xs)
        {
            foreach (float z in zs)
            {
                Prim(PrimitiveType.Cylinder, root, new Vector3(x, baseY + towerH * 0.5f, z), new Vector3(3.6f, towerH * 0.5f, 3.6f), towerMat);
                Prim(PrimitiveType.Sphere, root, new Vector3(x, baseY + towerH, z), new Vector3(3.8f, 2.6f, 3.8f), roofMat);
            }
        }
    }

    // ---------- trees and rocks ----------
    void Scatter(Bounds b, Material grass, Transform root)
    {
        // keep scenery out of the play area (path bounding box + margin)
        Vector2 min = new Vector2(-10, -10), max = new Vector2(10, 10);
        if (pathRoot != null && pathRoot.childCount > 0)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Transform c in pathRoot)
            {
                min.x = Mathf.Min(min.x, c.position.x);
                min.y = Mathf.Min(min.y, c.position.z);
                max.x = Mathf.Max(max.x, c.position.x);
                max.y = Mathf.Max(max.y, c.position.z);
            }
        }
        min -= new Vector2(pathMargin, pathMargin);
        max += new Vector2(pathMargin, pathMargin);

        System.Random rng = new System.Random(seed);
        float baseY = b.max.y;

        Material trunk = Mat(grass, new Color(0.38f, 0.24f, 0.12f));
        Material leafA = Mat(grass, new Color(0.10f, 0.40f, 0.12f));
        Material leafB = Mat(grass, new Color(0.16f, 0.52f, 0.16f));
        Material rock = Mat(grass, new Color(0.5f, 0.5f, 0.52f));

        for (int i = 0; i < trees; i++)
        {
            if (!RandomSpot(rng, b, min, max, out Vector3 p)) continue;
            float s = 0.8f + (float)rng.NextDouble() * 0.9f;

            Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, baseY + 0.9f * s, p.z), new Vector3(0.5f * s, 0.9f * s, 0.5f * s), trunk);
            Prim(PrimitiveType.Sphere, root, new Vector3(p.x, baseY + 2.4f * s, p.z), Vector3.one * 2.4f * s, leafA);
            Prim(PrimitiveType.Sphere, root, new Vector3(p.x, baseY + 3.5f * s, p.z), Vector3.one * 1.7f * s, leafB);
        }

        for (int i = 0; i < rocks; i++)
        {
            if (!RandomSpot(rng, b, min, max, out Vector3 p)) continue;
            Vector3 sc = new Vector3(0.8f + (float)rng.NextDouble() * 1.0f,
                                     0.5f + (float)rng.NextDouble() * 0.5f,
                                     0.8f + (float)rng.NextDouble() * 1.0f);
            Prim(PrimitiveType.Sphere, root, new Vector3(p.x, baseY + sc.y * 0.3f, p.z), sc, rock);
        }
    }

    bool RandomSpot(System.Random rng, Bounds b, Vector2 min, Vector2 max, out Vector3 pos)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float x = Mathf.Lerp(b.min.x + 1f, b.max.x - 1f, (float)rng.NextDouble());
            float z = Mathf.Lerp(b.min.z + 1f, b.max.z - 1f, (float)rng.NextDouble());
            bool inside = x > min.x && x < max.x && z > min.y && z < max.y;
            if (!inside) { pos = new Vector3(x, 0, z); return true; }
        }
        pos = Vector3.zero;
        return false;
    }
}