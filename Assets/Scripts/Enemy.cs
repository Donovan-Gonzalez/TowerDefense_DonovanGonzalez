using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Ground, Flyer, Boss }
public enum EnemyModel { Goblin, Wolf, Ogre, Skeleton, Golem, Ufo, Demon }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    // fired when a boss crosses 50% (phase 1) and 25% (phase 2) health
    public static event System.Action<int> BossPhase;

    public float speed = 3f;
    public int maxHealth = 3;
    public int reward = 10;

    class Swing
    {
        public Transform t;
        public Vector3 axis;
        public float amp;
        public float offset;
        public float freq;
        public Quaternion baseRot;
    }

    const float BarWidth = 1.3f;
    static readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();

    Vector3[] path;
    int nextIndex = 1;
    float health;
    float damageMult = 1f;
    EnemyKind kind;
    int phase;
    bool dead;

    Material baseMat;                 // shared material of the (hidden) capsule, used as a template
    Transform visual;                 // every model part lives under this
    readonly List<Renderer> tinted = new List<Renderer>();
    readonly List<Swing> swings = new List<Swing>();
    float topY = 0.8f;                // top of the model, relative to the capsule center (local units)
    float walkPhase;
    float bobAmount = 0.04f;
    bool hover;
    Transform spinner;
    float spinSpeed;
    bool uniqueMats;

    Transform bar;
    Transform barFill;
    Material fillMat;

    public Vector3 Velocity
    {
        get
        {
            if (path == null || nextIndex >= path.Length) return Vector3.zero;
            Vector3 d = path[nextIndex] - transform.position;
            d.y = 0;
            return d.normalized * speed;
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void OnDestroy() { if (fillMat != null) Destroy(fillMat); }

    public void Configure(float newSpeed, int newHealth, Color color, float scale, int newReward,
                          EnemyKind newKind, float newDamageMult, EnemyModel model)
    {
        speed = newSpeed;
        maxHealth = newHealth;
        health = newHealth;
        reward = newReward;
        kind = newKind;
        damageMult = newDamageMult;
        uniqueMats = (kind == EnemyKind.Boss);   // the boss changes color in its phases
        transform.localScale *= scale;

        // keep the capsule's collider (that's what projectiles hit), hide its mesh
        Renderer capsule = GetComponentInChildren<Renderer>();
        if (capsule != null)
        {
            baseMat = capsule.sharedMaterial;
            capsule.enabled = false;
        }
        if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        switch (model)
        {
            case EnemyModel.Goblin: BuildGoblin(color); break;
            case EnemyModel.Wolf: BuildWolf(color); break;
            case EnemyModel.Ogre: BuildOgre(color); break;
            case EnemyModel.Skeleton: BuildSkeleton(color); break;
            case EnemyModel.Golem: BuildGolem(color); break;
            case EnemyModel.Ufo: BuildUfo(color); break;
            case EnemyModel.Demon: BuildDemon(color); break;
        }

        BuildHealthBar();
    }

    public void Init(Vector3[] points)
    {
        path = points;
        transform.position = path[0];
        nextIndex = 1;
        health = maxHealth;
        UpdateBar();
    }

    // =====================================================================
    //  Model-building helpers
    // =====================================================================
    static Color Dk(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k); }

    // y is measured up from the ground (the capsule's bottom is at local y = -1)
    static Vector3 G(float x, float y, float z) { return new Vector3(x, y - 1f, z); }

    Material GetMat(Color c)
    {
        Material m;
        if (!uniqueMats && matCache.TryGetValue(c, out m) && m != null) return m;

        m = new Material(baseMat);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (!uniqueMats) matCache[c] = m;
        return m;
    }

    GameObject MakePart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 size, Color c, Vector3 euler, bool tint)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        if (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule) size.y *= 0.5f;
        go.transform.localScale = size;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = GetMat(c);
        if (tint) tinted.Add(r);
        return go;
    }

    GameObject Cube(Transform p, Vector3 pos, Vector3 size, Color c, bool tint = false, Vector3 euler = default(Vector3))
    { return MakePart(PrimitiveType.Cube, p, pos, size, c, euler, tint); }

    GameObject Ball(Transform p, Vector3 pos, Vector3 size, Color c, bool tint = false)
    { return MakePart(PrimitiveType.Sphere, p, pos, size, c, Vector3.zero, tint); }

    GameObject Cyl(Transform p, Vector3 pos, Vector3 size, Color c, bool tint = false, Vector3 euler = default(Vector3))
    { return MakePart(PrimitiveType.Cylinder, p, pos, size, c, euler, tint); }

    Transform Pivot(Vector3 groundPos)
    {
        GameObject go = new GameObject("Pivot");
        go.transform.SetParent(visual, false);
        go.transform.localPosition = G(groundPos.x, groundPos.y, groundPos.z);
        return go.transform;
    }

    void AddSwing(Transform t, Vector3 axis, float amp, float offset, float freq)
    {
        Swing s = new Swing();
        s.t = t; s.axis = axis; s.amp = amp; s.offset = offset; s.freq = freq;
        s.baseRot = t.localRotation;
        swings.Add(s);
    }

    // a limb hangs down from a hip/shoulder pivot and swings while walking
    Transform Limb(Vector3 hip, Vector3 size, Color c, bool tint, float amp, float offset)
    {
        Transform pivot = Pivot(hip);
        Cube(pivot, new Vector3(0, -size.y * 0.5f, 0), size, c, tint);
        AddSwing(pivot, Vector3.right, amp, offset, 1f);
        return pivot;
    }

    // =====================================================================
    //  The models
    // =====================================================================
    void BuildGoblin(Color body)
    {
        Color dark = Dk(body, 0.55f);
        Color brown = new Color(0.35f, 0.22f, 0.12f);
        Color yellow = new Color(1f, 0.9f, 0.2f);

        Limb(new Vector3(-0.14f, 0.55f, 0), new Vector3(0.18f, 0.55f, 0.2f), dark, false, 35f, 0f);
        Limb(new Vector3(0.14f, 0.55f, 0), new Vector3(0.18f, 0.55f, 0.2f), dark, false, 35f, Mathf.PI);

        Ball(visual, G(0, 0.85f, 0), new Vector3(0.5f, 0.55f, 0.38f), body, true);        // body
        Ball(visual, G(0, 1.3f, 0.05f), new Vector3(0.52f, 0.46f, 0.46f), body, true);    // head
        Cube(visual, G(-0.36f, 1.35f, 0), new Vector3(0.3f, 0.09f, 0.07f), body, true, new Vector3(0, 0, 25));   // ears
        Cube(visual, G(0.36f, 1.35f, 0), new Vector3(0.3f, 0.09f, 0.07f), body, true, new Vector3(0, 0, -25));
        Cube(visual, G(-0.12f, 1.35f, 0.22f), new Vector3(0.1f, 0.1f, 0.06f), yellow);    // eyes
        Cube(visual, G(0.12f, 1.35f, 0.22f), new Vector3(0.1f, 0.1f, 0.06f), yellow);
        Cube(visual, G(0, 1.25f, 0.3f), new Vector3(0.1f, 0.14f, 0.12f), dark);           // nose

        Transform armL = Limb(new Vector3(-0.32f, 1.1f, 0), new Vector3(0.12f, 0.45f, 0.12f), body, true, 35f, Mathf.PI);
        Transform armR = Limb(new Vector3(0.32f, 1.1f, 0), new Vector3(0.12f, 0.45f, 0.12f), body, true, 35f, 0f);
        Cube(armR, new Vector3(0, -0.5f, 0.12f), new Vector3(0.12f, 0.5f, 0.12f), brown);   // club
        Ball(armR, new Vector3(0, -0.75f, 0.12f), new Vector3(0.22f, 0.22f, 0.22f), brown);

        topY = 1.55f - 1f;
        bobAmount = 0.05f;
    }

    void BuildWolf(Color body)
    {
        Color dark = Dk(body, 0.6f);
        Color red = new Color(1f, 0.15f, 0.1f);

        Cube(visual, G(0, 0.62f, 0), new Vector3(0.45f, 0.4f, 1.0f), body, true);          // body
        Cube(visual, G(0, 0.78f, 0.68f), new Vector3(0.38f, 0.34f, 0.42f), body, true);    // head
        Cube(visual, G(0, 0.7f, 0.98f), new Vector3(0.2f, 0.17f, 0.28f), dark);            // snout
        Cube(visual, G(0, 0.74f, 1.13f), new Vector3(0.08f, 0.07f, 0.06f), Color.black);   // nose
        Cube(visual, G(-0.13f, 1.0f, 0.6f), new Vector3(0.1f, 0.2f, 0.08f), body, true);   // ears
        Cube(visual, G(0.13f, 1.0f, 0.6f), new Vector3(0.1f, 0.2f, 0.08f), body, true);
        Cube(visual, G(-0.1f, 0.85f, 0.89f), new Vector3(0.07f, 0.07f, 0.05f), red);      // eyes
        Cube(visual, G(0.1f, 0.85f, 0.89f), new Vector3(0.07f, 0.07f, 0.05f), red);

        // four legs, diagonal pairs move together
        Limb(new Vector3(-0.15f, 0.45f, 0.35f), new Vector3(0.12f, 0.45f, 0.12f), dark, false, 40f, 0f);
        Limb(new Vector3(0.15f, 0.45f, -0.35f), new Vector3(0.12f, 0.45f, 0.12f), dark, false, 40f, 0f);
        Limb(new Vector3(0.15f, 0.45f, 0.35f), new Vector3(0.12f, 0.45f, 0.12f), dark, false, 40f, Mathf.PI);
        Limb(new Vector3(-0.15f, 0.45f, -0.35f), new Vector3(0.12f, 0.45f, 0.12f), dark, false, 40f, Mathf.PI);

        // wagging tail
        Transform tail = Pivot(new Vector3(0, 0.75f, -0.5f));
        tail.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        Cube(tail, new Vector3(0, 0, -0.25f), new Vector3(0.1f, 0.1f, 0.5f), body, true);
        AddSwing(tail, Vector3.up, 25f, 0f, 1.5f);

        topY = 1.12f - 1f;
        bobAmount = 0.04f;
    }

    void BuildOgre(Color body)
    {
        Color dark = Dk(body, 0.55f);
        Color brown = new Color(0.35f, 0.22f, 0.12f);
        Color bone = new Color(0.95f, 0.93f, 0.85f);
        Color yellow = new Color(1f, 0.85f, 0.2f);

        Limb(new Vector3(-0.22f, 0.75f, 0), new Vector3(0.32f, 0.75f, 0.34f), dark, false, 25f, 0f);
        Limb(new Vector3(0.22f, 0.75f, 0), new Vector3(0.32f, 0.75f, 0.34f), dark, false, 25f, Mathf.PI);

        Cube(visual, G(0, 0.82f, 0), new Vector3(0.78f, 0.32f, 0.58f), brown);              // loincloth
        Ball(visual, G(0, 1.1f, 0), new Vector3(1.0f, 0.95f, 0.75f), body, true);           // belly
        Ball(visual, G(0, 1.6f, 0.05f), new Vector3(0.95f, 0.6f, 0.6f), body, true);        // chest
        Ball(visual, G(0, 1.95f, 0.12f), new Vector3(0.5f, 0.46f, 0.46f), body, true);      // head
        Cube(visual, G(-0.14f, 1.8f, 0.36f), new Vector3(0.07f, 0.2f, 0.07f), bone);       // tusks
        Cube(visual, G(0.14f, 1.8f, 0.36f), new Vector3(0.07f, 0.2f, 0.07f), bone);
        Cube(visual, G(-0.11f, 2.0f, 0.34f), new Vector3(0.09f, 0.07f, 0.05f), yellow);    // eyes
        Cube(visual, G(0.11f, 2.0f, 0.34f), new Vector3(0.09f, 0.07f, 0.05f), yellow);

        Limb(new Vector3(-0.62f, 1.65f, 0), new Vector3(0.28f, 0.8f, 0.28f), body, true, 25f, Mathf.PI);
        Transform armR = Limb(new Vector3(0.62f, 1.65f, 0), new Vector3(0.28f, 0.8f, 0.28f), body, true, 25f, 0f);
        Cube(armR, new Vector3(0, -0.85f, 0.2f), new Vector3(0.2f, 0.8f, 0.2f), brown);     // spiked club
        Ball(armR, new Vector3(0, -1.2f, 0.2f), new Vector3(0.38f, 0.38f, 0.38f), dark);

        topY = 2.2f - 1f;
        bobAmount = 0.07f;
    }

    void BuildSkeleton(Color bone)
    {
        Color red = new Color(1f, 0.2f, 0.15f);
        Color metal = new Color(0.55f, 0.57f, 0.6f);

        Limb(new Vector3(-0.12f, 0.65f, 0), new Vector3(0.09f, 0.65f, 0.09f), bone, true, 35f, 0f);
        Limb(new Vector3(0.12f, 0.65f, 0), new Vector3(0.09f, 0.65f, 0.09f), bone, true, 35f, Mathf.PI);

        Cube(visual, G(0, 0.7f, 0), new Vector3(0.38f, 0.12f, 0.18f), bone, true);          // pelvis
        Cube(visual, G(0, 1.0f, 0), new Vector3(0.08f, 0.5f, 0.08f), bone, true);           // spine
        for (int i = 0; i < 4; i++)                                                         // ribs
            Cube(visual, G(0, 1.05f + i * 0.14f, 0.02f), new Vector3(0.46f - 0.04f * i, 0.05f, 0.2f), bone, true);

        Ball(visual, G(0, 1.7f, 0.02f), new Vector3(0.38f, 0.4f, 0.38f), bone, true);       // skull
        Cube(visual, G(0, 1.52f, 0.1f), new Vector3(0.2f, 0.07f, 0.18f), bone, true);       // jaw
        Cube(visual, G(-0.09f, 1.73f, 0.17f), new Vector3(0.1f, 0.1f, 0.05f), Color.black);// eye sockets
        Cube(visual, G(0.09f, 1.73f, 0.17f), new Vector3(0.1f, 0.1f, 0.05f), Color.black);
        Cube(visual, G(-0.09f, 1.73f, 0.2f), new Vector3(0.05f, 0.05f, 0.03f), red);       // glowing eyes
        Cube(visual, G(0.09f, 1.73f, 0.2f), new Vector3(0.05f, 0.05f, 0.03f), red);

        Limb(new Vector3(-0.27f, 1.45f, 0), new Vector3(0.07f, 0.6f, 0.07f), bone, true, 30f, Mathf.PI);
        Transform armR = Limb(new Vector3(0.27f, 1.45f, 0), new Vector3(0.07f, 0.6f, 0.07f), bone, true, 30f, 0f);
        Cube(armR, new Vector3(0, -0.6f, 0.3f), new Vector3(0.05f, 0.05f, 0.7f), metal);    // sword

        topY = 1.9f - 1f;
        bobAmount = 0.05f;
    }

    void BuildGolem(Color stone)
    {
        Color dark = Dk(stone, 0.7f);
        Color glow = new Color(1f, 0.55f, 0.1f);
        Color moss = new Color(0.25f, 0.5f, 0.2f);

        Limb(new Vector3(-0.28f, 0.6f, 0), new Vector3(0.4f, 0.6f, 0.42f), dark, false, 18f, 0f);
        Limb(new Vector3(0.28f, 0.6f, 0), new Vector3(0.4f, 0.6f, 0.42f), dark, false, 18f, Mathf.PI);

        Cube(visual, G(0, 0.72f, 0), new Vector3(0.7f, 0.25f, 0.5f), dark);                 // waist
        Cube(visual, G(0, 1.25f, 0), new Vector3(1.0f, 0.9f, 0.65f), stone, true);          // torso
        Cube(visual, G(-0.72f, 1.65f, 0), new Vector3(0.45f, 0.42f, 0.52f), stone, true);   // shoulders
        Cube(visual, G(0.72f, 1.65f, 0), new Vector3(0.45f, 0.42f, 0.52f), stone, true);
        Cube(visual, G(-0.72f, 1.89f, 0), new Vector3(0.34f, 0.08f, 0.4f), moss);           // moss
        Cube(visual, G(0.72f, 1.89f, 0), new Vector3(0.34f, 0.08f, 0.4f), moss);
        Cube(visual, G(0, 2.0f, 0.05f), new Vector3(0.5f, 0.45f, 0.46f), stone, true);      // head
        Cube(visual, G(-0.12f, 2.03f, 0.29f), new Vector3(0.12f, 0.08f, 0.05f), glow);      // glowing eyes
        Cube(visual, G(0.12f, 2.03f, 0.29f), new Vector3(0.12f, 0.08f, 0.05f), glow);
        Cube(visual, G(0, 1.3f, 0.34f), new Vector3(0.28f, 0.28f, 0.04f), glow);            // chest rune

        Transform armL = Limb(new Vector3(-0.72f, 1.5f, 0), new Vector3(0.34f, 0.85f, 0.34f), stone, true, 20f, Mathf.PI);
        Transform armR = Limb(new Vector3(0.72f, 1.5f, 0), new Vector3(0.34f, 0.85f, 0.34f), stone, true, 20f, 0f);
        Cube(armL, new Vector3(0, -0.9f, 0), new Vector3(0.42f, 0.34f, 0.42f), dark);       // fists
        Cube(armR, new Vector3(0, -0.9f, 0), new Vector3(0.42f, 0.34f, 0.42f), dark);

        topY = 2.2f - 1f;
        bobAmount = 0.03f;
    }

    // the UFO is modelled around the capsule's center (it hovers), so no G() here
    void BuildUfo(Color body)
    {
        // use the Kenney (CC0) UFO model if it's in Assets/Resources/Kenney
        GameObject kenney = Resources.Load<GameObject>("Kenney/enemy-ufo-a");
        if (kenney != null)
        {
            hover = true;
            bobAmount = 0.12f;
            GameObject m = Instantiate(kenney, visual);
            m.transform.localPosition = Vector3.zero;
            m.transform.localScale = Vector3.one * 2f;   // tweak if it looks too big or small
            topY = 1.4f;
            return;
        }

        Color glass = new Color(0.4f, 0.9f, 1f);
        Color metal = Dk(body, 0.45f);

        hover = true;
        bobAmount = 0.12f;

        Ball(visual, Vector3.zero, new Vector3(1.9f, 0.38f, 1.9f), body, true);                 // saucer
        Ball(visual, new Vector3(0, 0.28f, 0), new Vector3(0.85f, 0.75f, 0.85f), glass);        // dome
        Cyl(visual, new Vector3(0, -0.25f, 0), new Vector3(0.7f, 0.15f, 0.7f), metal);          // underside
        Cyl(visual, new Vector3(0, 0.78f, 0), new Vector3(0.05f, 0.35f, 0.05f), metal);         // antenna
        Ball(visual, new Vector3(0, 1.0f, 0), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.2f, 0.2f));

        spinner = new GameObject("Spinner").transform;
        spinner.SetParent(visual, false);
        spinSpeed = 140f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            Color c = (i % 2 == 0) ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.2f);
            Ball(spinner, new Vector3(Mathf.Cos(a) * 0.82f, 0f, Mathf.Sin(a) * 0.82f), new Vector3(0.15f, 0.15f, 0.15f), c);
        }

        topY = 1.1f;
    }

    void BuildDemon(Color body)
    {
        Color dark = Dk(body, 0.5f);
        Color gold = new Color(1f, 0.8f, 0.2f);
        Color red = new Color(1f, 0.2f, 0.1f);

        Limb(new Vector3(-0.25f, 0.8f, 0), new Vector3(0.34f, 0.8f, 0.36f), dark, false, 20f, 0f);
        Limb(new Vector3(0.25f, 0.8f, 0), new Vector3(0.34f, 0.8f, 0.36f), dark, false, 20f, Mathf.PI);

        Ball(visual, G(0, 1.3f, 0), new Vector3(1.1f, 1.0f, 0.75f), body, true);            // torso
        Ball(visual, G(-0.7f, 1.75f, 0), new Vector3(0.55f, 0.55f, 0.55f), body, true);     // shoulders
        Ball(visual, G(0.7f, 1.75f, 0), new Vector3(0.55f, 0.55f, 0.55f), body, true);
        Ball(visual, G(0, 2.15f, 0.08f), new Vector3(0.6f, 0.55f, 0.55f), body, true);      // head
        Cube(visual, G(-0.25f, 2.55f, 0), new Vector3(0.12f, 0.55f, 0.12f), gold, false, new Vector3(0, 0, 20));   // horns
        Cube(visual, G(0.25f, 2.55f, 0), new Vector3(0.12f, 0.55f, 0.12f), gold, false, new Vector3(0, 0, -20));
        Cube(visual, G(-0.14f, 2.2f, 0.3f), new Vector3(0.1f, 0.07f, 0.05f), red);          // eyes
        Cube(visual, G(0.14f, 2.2f, 0.3f), new Vector3(0.1f, 0.07f, 0.05f), red);

        Limb(new Vector3(-0.75f, 1.7f, 0), new Vector3(0.28f, 0.85f, 0.28f), body, true, 20f, Mathf.PI);
        Limb(new Vector3(0.75f, 1.7f, 0), new Vector3(0.28f, 0.85f, 0.28f), body, true, 20f, 0f);

        // wings
        Transform wingL = Pivot(new Vector3(-0.3f, 1.9f, -0.3f));
        Cube(wingL, new Vector3(-0.6f, 0.15f, 0), new Vector3(1.2f, 0.9f, 0.05f), dark);
        AddSwing(wingL, Vector3.forward, 10f, 0f, 1.2f);
        Transform wingR = Pivot(new Vector3(0.3f, 1.9f, -0.3f));
        Cube(wingR, new Vector3(0.6f, 0.15f, 0), new Vector3(1.2f, 0.9f, 0.05f), dark);
        AddSwing(wingR, Vector3.forward, 10f, Mathf.PI, 1.2f);

        // back spikes
        for (int i = 0; i < 3; i++)
            Cube(visual, G(0, 1.2f + i * 0.35f, -0.4f), new Vector3(0.1f, 0.2f, 0.1f), gold, false, new Vector3(-30, 0, 0));

        // tail
        Transform tail = Pivot(new Vector3(0, 0.95f, -0.35f));
        Cube(tail, new Vector3(0, 0, -0.4f), new Vector3(0.12f, 0.12f, 0.8f), dark);
        AddSwing(tail, Vector3.up, 20f, 0f, 1.3f);

        topY = 2.65f - 1f;
        bobAmount = 0.08f;
    }

    // =====================================================================
    //  Health bar
    // =====================================================================
    Transform BarPiece(Vector3 size, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(bar, false);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    void BuildHealthBar()
    {
        bar = new GameObject("HealthBar").transform;
        bar.SetParent(transform, false);
        bar.localScale = Vector3.one / transform.lossyScale.x;

        BarPiece(new Vector3(BarWidth, 0.2f, 0.05f), GetMat(new Color(0.1f, 0.1f, 0.1f)));

        fillMat = new Material(baseMat);
        fillMat.color = Color.green;
        barFill = BarPiece(new Vector3(BarWidth * 0.94f, 0.14f, 0.05f), fillMat);
        UpdateBar();
    }

    void UpdateBar()
    {
        if (barFill == null) return;
        float ratio = Mathf.Clamp01(health / maxHealth);
        barFill.localScale = new Vector3(BarWidth * 0.94f * ratio, 0.14f, 0.05f);
        barFill.localPosition = new Vector3(-BarWidth * 0.47f * (1f - ratio), 0f, -0.03f);
        if (fillMat != null) fillMat.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    void SetBodyColor(Color c)
    {
        foreach (Renderer r in tinted)
            if (r != null) r.sharedMaterial.color = c;   // boss only: it has its own materials
    }

    // =====================================================================
    //  Update
    // =====================================================================
    void LateUpdate()
    {
        if (bar == null) return;
        bar.position = transform.position + Vector3.up * (topY * transform.lossyScale.y + 0.6f);
        if (Camera.main != null) bar.rotation = Camera.main.transform.rotation;
    }

    void Update()
    {
        Animate();

        if (path == null || nextIndex >= path.Length) return;

        Vector3 target = path[nextIndex];
        Vector3 flat = target - transform.position;
        flat.y = 0;
        if (flat.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flat);

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            nextIndex++;
            if (nextIndex >= path.Length)
            {
                if (GameManager.Instance != null) GameManager.Instance.EnemyReachedEnd();
                Destroy(gameObject);
            }
        }
    }

    void Animate()
    {
        if (visual == null) return;

        walkPhase += Time.deltaTime * speed * 2.5f;

        foreach (Swing s in swings)
        {
            float a = Mathf.Sin(walkPhase * s.freq + s.offset) * s.amp;
            s.t.localRotation = s.baseRot * Quaternion.AngleAxis(a, s.axis);
        }

        float y = hover ? Mathf.Sin(walkPhase * 0.6f) * bobAmount
                        : Mathf.Abs(Mathf.Sin(walkPhase)) * bobAmount;
        visual.localPosition = new Vector3(0, y, 0);

        if (spinner != null) spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
    }

    // =====================================================================
    //  Damage
    // =====================================================================
    public void TakeDamage(int amount, bool ignoreArmor = false)
    {
        if (dead) return;

        health -= amount * (ignoreArmor ? 1f : damageMult);
        UpdateBar();

        if (health <= 0f)
        {
            dead = true;
            if (GameManager.Instance != null) GameManager.Instance.AddCoins(reward);
            Destroy(gameObject);
            return;
        }

        // boss phases
        if (kind == EnemyKind.Boss)
        {
            float ratio = health / maxHealth;

            if (phase < 1 && ratio <= 0.5f)
            {
                phase = 1;
                speed *= 1.5f;
                SetBodyColor(new Color(0.65f, 0.08f, 0.12f));
                if (BossPhase != null) BossPhase(1);
            }
            if (phase < 2 && ratio <= 0.25f)
            {
                phase = 2;
                speed *= 1.4f;
                SetBodyColor(new Color(1f, 0.1f, 0.1f));
                if (BossPhase != null) BossPhase(2);
            }
        }
    }
}