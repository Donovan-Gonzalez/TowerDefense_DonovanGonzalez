using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Ground, Flyer, Boss }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    // fired when a boss crosses 50% (phase 1) and 25% (phase 2) health
    public static event System.Action<int> BossPhase;

    public float speed = 3f;
    public int maxHealth = 3;
    public int reward = 10;

    const float BarWidth = 1.3f;

    Vector3[] path;
    int nextIndex = 1;
    float health;
    float damageMult = 1f;     // 1 = normal, 0.5 = armored (takes half damage)
    EnemyKind kind;
    int phase;
    bool dead;

    Renderer bodyRenderer;
    Material baseMat;
    Transform bar;
    Transform barFill;

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

    public void Configure(float newSpeed, int newHealth, Color color, float scale, int newReward,
                          EnemyKind newKind, float newDamageMult)
    {
        speed = newSpeed;
        maxHealth = newHealth;
        health = newHealth;
        reward = newReward;
        kind = newKind;
        damageMult = newDamageMult;
        transform.localScale *= scale;

        bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
            baseMat = bodyRenderer.material;
        }

        if (kind == EnemyKind.Flyer) AddWings();
        if (kind == EnemyKind.Boss) AddHorns();
        if (damageMult < 1f && kind == EnemyKind.Ground) AddArmor();
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

    // ---------- looks ----------
    GameObject Part(PrimitiveType type, Vector3 pos, Vector3 size, Color c, Vector3 euler)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = size;
        if (baseMat != null)
        {
            Material m = new Material(baseMat);
            m.color = c;
            go.GetComponent<Renderer>().material = m;
        }
        return go;
    }

    void AddWings()
    {
        Color wing = new Color(0.95f, 0.98f, 1f);
        Part(PrimitiveType.Cube, new Vector3(-0.9f, 0.25f, 0), new Vector3(1.3f, 0.08f, 0.7f), wing, new Vector3(0, 0, 15));
        Part(PrimitiveType.Cube, new Vector3(0.9f, 0.25f, 0), new Vector3(1.3f, 0.08f, 0.7f), wing, new Vector3(0, 0, -15));
    }

    void AddHorns()
    {
        Color gold = new Color(1f, 0.8f, 0.2f);
        Part(PrimitiveType.Cube, new Vector3(-0.3f, 1.05f, 0), new Vector3(0.15f, 0.6f, 0.15f), gold, new Vector3(0, 0, 25));
        Part(PrimitiveType.Cube, new Vector3(0.3f, 1.05f, 0), new Vector3(0.15f, 0.6f, 0.15f), gold, new Vector3(0, 0, -25));
    }

    // armored enemies get a steel belt so you can tell them apart
    void AddArmor()
    {
        Color steel = new Color(0.75f, 0.78f, 0.82f);
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(1.15f, 0.12f, 1.15f), steel, Vector3.zero);
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.55f, 0), new Vector3(1.05f, 0.1f, 1.05f), steel, Vector3.zero);
    }

    void SetBodyColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }

    Transform BarPiece(Vector3 size, Color c)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(bar, false);
        go.transform.localScale = size;
        if (baseMat != null)
        {
            Material m = new Material(baseMat);
            m.color = c;
            go.GetComponent<Renderer>().material = m;
        }
        return go.transform;
    }

    void BuildHealthBar()
    {
        bar = new GameObject("HealthBar").transform;
        bar.SetParent(transform, false);
        bar.localScale = Vector3.one / transform.lossyScale.x;

        BarPiece(new Vector3(BarWidth, 0.2f, 0.05f), new Color(0.1f, 0.1f, 0.1f));
        barFill = BarPiece(new Vector3(BarWidth * 0.94f, 0.14f, 0.05f), Color.green);
        UpdateBar();
    }

    void UpdateBar()
    {
        if (barFill == null) return;
        float ratio = Mathf.Clamp01(health / maxHealth);
        barFill.localScale = new Vector3(BarWidth * 0.94f * ratio, 0.14f, 0.05f);
        barFill.localPosition = new Vector3(-BarWidth * 0.47f * (1f - ratio), 0f, -0.03f);
        Renderer r = barFill.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    void LateUpdate()
    {
        if (bar == null) return;
        bar.position = transform.position + Vector3.up * (transform.lossyScale.y + 0.8f);
        if (Camera.main != null) bar.rotation = Camera.main.transform.rotation;
    }

    // ---------- movement ----------
    void Update()
    {
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
                SetBodyColor(new Color(0.55f, 0.05f, 0.1f));
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