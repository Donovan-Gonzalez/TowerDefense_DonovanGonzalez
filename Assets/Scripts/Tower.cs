using UnityEngine;

public enum TowerStyle { Rapid, Sniper, Bomber }

public class Tower : MonoBehaviour
{
    public string towerName = "Tower";
    public TowerStyle style = TowerStyle.Rapid;
    public int cost = 50;
    public float range = 10f;
    public float shotsPerSecond = 2f;
    public float projectileSpeed = 15f;
    public int damage = 1;
    public float splashRadius = 0f;
    public Projectile projectilePrefab;

    // If a Kenney weapon model faces backwards or sideways, change this (try 180, 90 or -90)
    const float KenneyYaw = 0f;

    Transform turret;      // rotates toward the target
    Transform muzzle;      // projectiles spawn here
    Material baseMat;
    Color tint = Color.yellow;
    float cooldown;

    void Awake()
    {
        // keep a material to clone, then hide the placeholder cube from the prefab
        Renderer first = GetComponentInChildren<Renderer>();
        if (first != null) baseMat = first.sharedMaterial;
        if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        transform.localScale = Vector3.one;

        Build();
    }

    // =====================================================================
    //  Model
    // =====================================================================
    Material Mat(Color c)
    {
        Material m = new Material(baseMat);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        return m;
    }

    GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mat(c);
        return go;
    }

    GameObject Box(Transform p, Vector3 pos, Vector3 size, Color c)
    { return Part(PrimitiveType.Cube, p, pos, size, c, Vector3.zero); }

    GameObject Ball(Transform p, Vector3 pos, float d, Color c)
    { return Part(PrimitiveType.Sphere, p, pos, new Vector3(d, d, d), c, Vector3.zero); }

    // vertical cylinder: d = diameter, h = full height
    GameObject Post(Transform p, Vector3 pos, float d, float h, Color c)
    { return Part(PrimitiveType.Cylinder, p, pos, new Vector3(d, h * 0.5f, d), c, Vector3.zero); }

    // barrel pointing along +Z: d = diameter, len = length, pos = its center
    GameObject Barrel(Transform p, Vector3 pos, float d, float len, Color c)
    { return Part(PrimitiveType.Cylinder, p, pos, new Vector3(d, len * 0.5f, d), c, new Vector3(90, 0, 0)); }

    void Build()
    {
        float baseH, baseW, kenneyScale, muzzleUp, muzzleFwd;
        Color body, accent;
        string kenneyName;

        switch (style)
        {
            case TowerStyle.Sniper:      // tall and slim
                baseH = 3.4f; baseW = 0.9f;
                body = new Color(0.45f, 0.30f, 0.65f); accent = new Color(0.30f, 0.18f, 0.45f);
                tint = new Color(0.4f, 0.9f, 1f);
                kenneyName = "weapon-ballista"; kenneyScale = 1.5f; muzzleUp = 0.6f; muzzleFwd = 1.3f;
                break;
            case TowerStyle.Bomber:      // medium height, wide and heavy
                baseH = 2.0f; baseW = 1.7f;
                body = new Color(0.85f, 0.45f, 0.15f); accent = new Color(0.45f, 0.22f, 0.08f);
                tint = new Color(1f, 0.5f, 0.1f);
                kenneyName = "weapon-cannon"; kenneyScale = 1.7f; muzzleUp = 0.7f; muzzleFwd = 1.2f;
                break;
            default:                     // Rapid: short
                baseH = 1.3f; baseW = 1.2f;
                body = new Color(0.20f, 0.70f, 0.80f); accent = new Color(0.10f, 0.40f, 0.50f);
                tint = new Color(1f, 0.9f, 0.2f);
                kenneyName = "weapon-turret"; kenneyScale = 1.5f; muzzleUp = 0.6f; muzzleFwd = 0.9f;
                break;
        }

        Color stone = new Color(0.55f, 0.55f, 0.58f);

        // plinth, shaft and platform (this is what makes each tower a different height)
        Box(transform, new Vector3(0, 0.15f, 0), new Vector3(baseW + 0.6f, 0.3f, baseW + 0.6f), stone);
        Post(transform, new Vector3(0, baseH * 0.5f, 0), baseW, baseH, body);
        Box(transform, new Vector3(0, baseH, 0), new Vector3(baseW + 0.5f, 0.2f, baseW + 0.5f), accent);

        // rotating top
        turret = new GameObject("Turret").transform;
        turret.SetParent(transform, false);
        turret.localPosition = new Vector3(0, baseH + 0.1f, 0);

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(turret, false);
        muzzle.localPosition = new Vector3(0, muzzleUp, muzzleFwd);

        // Kenney (CC0) weapon if it's in Assets/Resources/Kenney, otherwise a built-in model
        GameObject kenney = Resources.Load<GameObject>("Kenney/" + kenneyName);
        if (kenney != null)
        {
            GameObject m = Instantiate(kenney, turret);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(0, KenneyYaw, 0);
            m.transform.localScale = Vector3.one * kenneyScale;
        }
        else
        {
            BuildFallbackTop(accent);
        }
    }

    void BuildFallbackTop(Color accent)
    {
        Color dark = new Color(0.15f, 0.15f, 0.17f);

        if (style == TowerStyle.Sniper)
        {
            Box(turret, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.4f, 0.6f), accent);
            Barrel(turret, new Vector3(0, 0.6f, 0.9f), 0.12f, 1.6f, dark);
        }
        else if (style == TowerStyle.Bomber)
        {
            Ball(turret, new Vector3(0, 0.4f, 0), 1.0f, accent);
            Barrel(turret, new Vector3(0, 0.6f, 0.6f), 0.5f, 1.0f, dark);
        }
        else
        {
            Box(turret, new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.45f, 0.7f), accent);
            Barrel(turret, new Vector3(-0.15f, 0.6f, 0.6f), 0.14f, 0.9f, dark);
            Barrel(turret, new Vector3(0.15f, 0.6f, 0.6f), 0.14f, 0.9f, dark);
        }
    }

    // =====================================================================
    //  Targeting and shooting
    // =====================================================================
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (turret == null) return;

        cooldown -= Time.deltaTime;

        Enemy target = FindTarget();
        if (target == null) return;

        Vector3 aim = Intercept(muzzle.position, target);

        Vector3 flat = aim - transform.position;
        flat.y = 0;
        if (flat.sqrMagnitude > 0.01f)
            turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(flat), 15f * Time.deltaTime);

        if (cooldown <= 0f)
        {
            Fire(aim);
            cooldown = 1f / Mathf.Max(0.01f, shotsPerSecond);
        }
    }

    // closest enemy inside range (measured on the ground, so flyers count too)
    Enemy FindTarget()
    {
        Enemy best = null;
        float bestDist = range * range;
        Vector3 p = transform.position;

        for (int i = 0; i < Enemy.All.Count; i++)
        {
            Enemy e = Enemy.All[i];
            if (e == null) continue;
            Vector3 d = e.transform.position - p;
            d.y = 0;
            float sq = d.sqrMagnitude;
            if (sq <= bestDist) { bestDist = sq; best = e; }
        }
        return best;
    }

    // where the enemy will be when the projectile arrives (solves |D + V t| = s t)
    Vector3 Intercept(Vector3 from, Enemy e)
    {
        Vector3 pos = e.transform.position;
        Vector3 vel = e.Velocity;
        Vector3 d = pos - from;
        float s = projectileSpeed;

        float a = Vector3.Dot(vel, vel) - s * s;
        float b = 2f * Vector3.Dot(d, vel);
        float c = Vector3.Dot(d, d);
        float t = 0f;

        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) > 0.0001f) t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc >= 0f)
            {
                float sq = Mathf.Sqrt(disc);
                float t1 = (-b - sq) / (2f * a);
                float t2 = (-b + sq) / (2f * a);
                if (t1 > 0f && t2 > 0f) t = Mathf.Min(t1, t2);
                else t = Mathf.Max(t1, t2);
            }
        }

        if (t < 0f) t = 0f;
        return pos + vel * t;
    }

    void Fire(Vector3 aim)
    {
        if (projectilePrefab == null) return;

        Vector3 dir = aim - muzzle.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Projectile p = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
        p.Launch(dir * projectileSpeed, damage, splashRadius, tint);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}