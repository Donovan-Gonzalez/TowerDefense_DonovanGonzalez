using UnityEngine;

public enum TowerStyle { Rapid, Sniper, Bomber }

public class Tower : MonoBehaviour
{
    public string towerName = "Tower";
    public TowerStyle style = TowerStyle.Rapid;
    public int cost = 50;
    public float range = 12f;
    public float shotsPerSecond = 1f;
    public float projectileSpeed = 20f;
    public int damage = 1;
    public float splashRadius = 0f;      // 0 = single target
    public Projectile projectilePrefab;

    Transform turret;
    float muzzleForward = 1f;
    Color tint = Color.white;
    float cooldown;
    Material baseMat;

    void Awake()
    {
        // hide the old placeholder cube and build a nicer model
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) { baseMat = mr.sharedMaterial; mr.enabled = false; }
        if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        transform.localScale = Vector3.one;
        BuildVisuals();
    }

    GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Color color)
    {
        return Part(parent, type, pos, size, color, Vector3.zero);
    }

    GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Color color, Vector3 euler)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        if (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule) size.y *= 0.5f;
        go.transform.localScale = size;

        Material m = new Material(baseMat);
        m.color = color;
        go.GetComponent<Renderer>().material = m;
        return go;
    }

    Transform MakeTurret(float height)
    {
        GameObject t = new GameObject("Turret");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = new Vector3(0, height, 0);
        return t.transform;
    }

    void BuildVisuals()
    {
        Color grey = new Color(0.35f, 0.35f, 0.38f);
        Color dark = new Color(0.12f, 0.12f, 0.14f);

        switch (style)
        {
            case TowerStyle.Rapid:
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.4f, 1.8f), grey);
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 1.0f, 0), new Vector3(1.1f, 1.2f, 1.1f), new Color(0.1f, 0.7f, 0.75f));
                turret = MakeTurret(1.8f);
                Part(turret, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.05f, 0.5f, 0.6f));
                Part(turret, PrimitiveType.Cube, new Vector3(-0.22f, 0, 0.65f), new Vector3(0.14f, 0.14f, 1.0f), dark);
                Part(turret, PrimitiveType.Cube, new Vector3(0.22f, 0, 0.65f), new Vector3(0.14f, 0.14f, 1.0f), dark);
                muzzleForward = 1.2f;
                tint = new Color(0.3f, 1f, 1f);
                break;

            case TowerStyle.Sniper:
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(1.6f, 0.4f, 1.6f), grey);
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 1.9f, 0), new Vector3(0.8f, 3.0f, 0.8f), new Color(0.4f, 0.2f, 0.6f));
                Part(transform, PrimitiveType.Cube, new Vector3(0, 3.55f, 0), new Vector3(1.3f, 0.3f, 1.3f), new Color(0.25f, 0.12f, 0.4f));
                turret = MakeTurret(3.95f);
                Part(turret, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.5f, 1.0f), dark);
                Part(turret, PrimitiveType.Cube, new Vector3(0, 0, 1.4f), new Vector3(0.14f, 0.14f, 2.2f), new Color(0.8f, 0.8f, 0.85f));
                Part(turret, PrimitiveType.Cube, new Vector3(0, 0.35f, 0.2f), new Vector3(0.2f, 0.2f, 0.5f), Color.black);
                muzzleForward = 2.5f;
                tint = new Color(1f, 0.3f, 1f);
                break;

            case TowerStyle.Bomber:
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0), new Vector3(2.2f, 0.5f, 2.2f), grey);
                Part(transform, PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(1.7f, 1.2f, 1.7f), new Color(0.85f, 0.3f, 0.1f));
                turret = MakeTurret(2.0f);
                Part(turret, PrimitiveType.Sphere, Vector3.zero, new Vector3(1.3f, 1.3f, 1.3f), new Color(0.2f, 0.2f, 0.22f));
                Part(turret, PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0.7f), new Vector3(0.6f, 1.2f, 0.6f), dark, new Vector3(90, 0, 0));
                Part(turret, PrimitiveType.Cylinder, new Vector3(0, 0.1f, 1.3f), new Vector3(0.8f, 0.15f, 0.8f), new Color(1f, 0.5f, 0.1f), new Vector3(90, 0, 0));
                muzzleForward = 1.5f;
                tint = new Color(1f, 0.5f, 0f);
                break;
        }
    }

    void Update()
    {
        cooldown -= Time.deltaTime;

        Enemy target = FindTarget();
        if (target == null) return;

        Vector3 from = turret.position;
        Vector3 targetPos = target.transform.position;

        Vector3 aimPoint;
        if (!PredictIntercept(from, targetPos, target.Velocity, projectileSpeed, out aimPoint))
            aimPoint = targetPos;

        Vector3 dir = aimPoint - from;

        Vector3 flat = dir; flat.y = 0;
        if (flat.sqrMagnitude > 0.01f)
            turret.rotation = Quaternion.LookRotation(flat);

        if (cooldown <= 0f)
        {
            Fire(from, dir.normalized);
            cooldown = 1f / shotsPerSecond;
        }
    }

    void Fire(Vector3 from, Vector3 dir)
    {
        Projectile p = Instantiate(projectilePrefab, from + dir * muzzleForward, Quaternion.identity);
        p.Launch(dir * projectileSpeed, damage, splashRadius, tint);
    }

    Enemy FindTarget()
    {
        Enemy best = null;
        float bestDist = range;
        foreach (Enemy e in Enemy.All)
        {
            Vector3 d = e.transform.position - transform.position;
            d.y = 0;
            float dist = d.magnitude;
            if (dist <= bestDist) { bestDist = dist; best = e; }
        }
        return best;
    }

    // Solve |targetPos + vel*t - shooter| = speed*t for the smallest t > 0
    static bool PredictIntercept(Vector3 shooter, Vector3 targetPos, Vector3 vel, float speed, out Vector3 aim)
    {
        aim = targetPos;
        Vector3 d = targetPos - shooter;
        float a = Vector3.Dot(vel, vel) - speed * speed;
        float b = 2f * Vector3.Dot(d, vel);
        float c = Vector3.Dot(d, d);

        float t;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f) return false;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a);
            float t2 = (-b + sq) / (2f * a);
            t = float.MaxValue;
            if (t1 > 0f) t = Mathf.Min(t, t1);
            if (t2 > 0f) t = Mathf.Min(t, t2);
            if (t == float.MaxValue) return false;
        }

        if (t <= 0f) return false;
        aim = targetPos + vel * t;
        return true;
    }
}