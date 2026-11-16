using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathLine : MonoBehaviour
{
    public float roadWidth = 0f;             // 0 = use the width from the LineRenderer on this object
    public float tileLengthInWidths = 2f;    // texture tile length, in road widths
    public float stoneSpacing = 2.2f;
    public int seed = 3;

    LineRenderer road;     // the flat ribbon actually drawn
    Transform stones;
    float width = 2f;

    void Start()
    {
        BuildRoad();
        Refresh();
    }

    // ---------- road look ----------
    void BuildRoad()
    {
        LineRenderer src = GetComponent<LineRenderer>();
        width = roadWidth > 0f ? roadWidth : Mathf.Max(0.5f, src.startWidth);
        src.enabled = false;   // old billboard line, no longer drawn

        GameObject go = new GameObject("RoadLine");
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // Z axis points up, so the ribbon lies flat
        road = go.AddComponent<LineRenderer>();
        road.useWorldSpace = true;
        road.alignment = LineAlignment.TransformZ;
        road.textureMode = LineTextureMode.Tile;
        road.startWidth = width;
        road.endWidth = width;
        road.numCornerVertices = 8;
        road.numCapVertices = 4;
        road.startColor = Color.white;
        road.endColor = Color.white;
        road.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        road.receiveShadows = false;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = sh != null ? new Material(sh) : new Material(src.sharedMaterial);
        m.mainTexture = MakeDirtTexture();
        m.color = Color.white;
        m.mainTextureScale = new Vector2(1f / (width * tileLengthInWidths), 1f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // draw both sides of the ribbon
        road.material = m;
    }

    Texture2D MakeDirtTexture()
    {
        const int w = 128;   // along the road
        const int h = 64;    // across the road
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        System.Random rng = new System.Random(seed);

        Color dirt = new Color(0.50f, 0.34f, 0.18f);
        Color[] px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float edge = Mathf.Abs(v - 0.5f) * 2f;               // 0 in the middle, 1 at the edges
            float edgeDark = 1f - 0.45f * edge * edge;

            for (int x = 0; x < w; x++)
            {
                float patch = 0.5f + 0.5f * Mathf.Sin(x * Mathf.PI * 4f / w) * Mathf.Sin(y * Mathf.PI * 2f / h);
                float noise = (float)rng.NextDouble();
                float k = (0.78f + 0.12f * patch + 0.18f * noise) * edgeDark;
                px[y * w + x] = new Color(dirt.r * k, dirt.g * k, dirt.b * k);
            }
        }

        // pebbles (wrap around so the tile repeats cleanly)
        for (int i = 0; i < 55; i++)
        {
            int cx = rng.Next(w);
            int cy = rng.Next(4, h - 4);
            int r = rng.Next(1, 4);
            float g = 0.45f + (float)rng.NextDouble() * 0.25f;

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int x = (cx + dx + w) % w;
                    int y = Mathf.Clamp(cy + dy, 0, h - 1);
                    float shade = (dy < 0) ? g * 0.75f : g;
                    px[y * w + x] = new Color(shade, shade * 0.97f, shade * 0.93f);
                }
            }
        }

        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    // ---------- line + stones ----------
    public void Refresh()
    {
        if (road == null) return;

        int n = transform.childCount;
        road.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = transform.GetChild(i).position;
            p.y = 0.05f;
            road.SetPosition(i, p);
        }

        ScatterStones();
    }

    void ScatterStones()
    {
        if (stones != null) Destroy(stones.gameObject);
        stones = new GameObject("RoadStones").transform;   // not a child of Path (children are waypoints)

        int n = transform.childCount;
        if (n < 2) return;

        System.Random rng = new System.Random(seed + 11);
        float half = width * 0.5f;

        Material baseMat;
        GameObject ground = GameObject.Find("Ground");
        Renderer gr = ground != null ? ground.GetComponent<Renderer>() : null;
        if (gr != null) baseMat = gr.sharedMaterial;
        else baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a = transform.GetChild(i).position;
            Vector3 b = transform.GetChild(i + 1).position;
            a.y = 0; b.y = 0;

            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) continue;
            dir /= len;
            Vector3 side = new Vector3(-dir.z, 0, dir.x);

            for (float d = 0.5f; d < len; d += stoneSpacing)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    if (rng.NextDouble() < 0.35) continue;   // leave gaps so it looks natural

                    float along = d + ((float)rng.NextDouble() - 0.5f) * stoneSpacing * 0.8f;
                    float offset = half + 0.1f + (float)rng.NextDouble() * 0.5f;
                    Vector3 pos = a + dir * along + side * (s * offset);
                    pos.y = 0.12f;

                    float size = 0.25f + (float)rng.NextDouble() * 0.4f;
                    float g = 0.42f + (float)rng.NextDouble() * 0.25f;

                    GameObject st = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(st.GetComponent<Collider>());
                    st.transform.SetParent(stones, false);
                    st.transform.position = pos;
                    st.transform.localScale = new Vector3(size * 1.3f, size * 0.7f, size);

                    Material m = new Material(baseMat);
                    m.mainTexture = null;
                    m.color = new Color(g, g * 0.98f, g * 0.94f);
                    st.GetComponent<Renderer>().material = m;
                }
            }
        }
    }
}