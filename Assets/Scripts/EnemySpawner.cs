using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyType
{
    public string name;
    public float speed;
    public int health;
    public Color color;
    public float scale;
    public int reward;
    public EnemyKind kind;
    public float damageMultiplier;   // 1 = normal, 0.5 = takes half damage

    public EnemyType(string n, float sp, int hp, Color c, float sc, int rw, EnemyKind k, float dmgMult)
    {
        name = n; speed = sp; health = hp; color = c; scale = sc; reward = rw; kind = k; damageMultiplier = dmgMult;
    }
}

public class LevelConfig
{
    public string name;
    public Vector3[] path;
    public int count;
    public int groupMin = 2;       // enemies come in clumps of groupMin..groupMax
    public int groupMax = 4;
    public float groupGap = 3f;    // pause between clumps
    public float hpMultiplier = 1f;
    public int startCoins;
    public EnemyType[] types;
    public int[] weights;
    public bool boss;
}

public static class LevelData
{
    //                                                           speed  hp   color                                scale  $    kind              dmg taken
    static readonly EnemyType Normal = new EnemyType("Normal", 3f, 3, new Color(0.85f, 0.2f, 0.2f), 1f, 7, EnemyKind.Ground, 1f);
    public static readonly EnemyType Runner = new EnemyType("Runner", 6f, 2, new Color(1f, 0.9f, 0.2f), 0.7f, 10, EnemyKind.Ground, 1f);
    public static readonly EnemyType Tank = new EnemyType("Tank", 1.5f, 10, new Color(0.2f, 0.3f, 0.9f), 1.4f, 25, EnemyKind.Ground, 1f);
    static readonly EnemyType Armored = new EnemyType("Armored", 2.5f, 7, new Color(0.6f, 0.3f, 0.8f), 1.15f, 15, EnemyKind.Ground, 0.5f);
    static readonly EnemyType Flyer = new EnemyType("Flyer", 2.5f, 5, new Color(0.8f, 0.95f, 1f), 0.9f, 20, EnemyKind.Flyer, 1f);
    public static readonly EnemyType Boss = new EnemyType("Boss", 1.2f, 50, new Color(0.15f, 0.05f, 0.2f), 2.4f, 200, EnemyKind.Boss, 0.8f);

    public static List<LevelConfig> Build()
    {
        List<LevelConfig> list = new List<LevelConfig>();

        list.Add(new LevelConfig
        {
            name = "Grassy Start",
            path = new[]
            {
                new Vector3(-20, 1, -15), new Vector3(-20, 1, 5), new Vector3(0, 1, 5),
                new Vector3(0, 1, -10), new Vector3(20, 1, -10), new Vector3(20, 1, 15)
            },
            count = 20,
            groupMin = 2,
            groupMax = 4,
            groupGap = 3.2f,
            hpMultiplier = 1f,
            startCoins = 75,
            types = new[] { Normal, Runner, Tank },
            weights = new[] { 5, 3, 2 },
            boss = false
        });

        list.Add(new LevelConfig
        {
            name = "Winding Valley",
            path = new[]
            {
                new Vector3(-20, 1, 14), new Vector3(-10, 1, 14), new Vector3(-10, 1, 0), new Vector3(0, 1, 0),
                new Vector3(0, 1, -12), new Vector3(12, 1, -12), new Vector3(12, 1, 4), new Vector3(20, 1, 4)
            },
            count = 30,
            groupMin = 3,
            groupMax = 5,
            groupGap = 2.6f,
            hpMultiplier = 1.5f,
            startCoins = 125,
            types = new[] { Normal, Runner, Tank, Armored },
            weights = new[] { 3, 3, 2, 3 },
            boss = false
        });

        list.Add(new LevelConfig
        {
            name = "Boss Gauntlet",
            path = new[]
            {
                new Vector3(-18, 1, -14), new Vector3(-18, 1, 12), new Vector3(-8, 1, 12), new Vector3(-8, 1, -8),
                new Vector3(2, 1, -8), new Vector3(2, 1, 12), new Vector3(12, 1, 12), new Vector3(12, 1, -8),
                new Vector3(20, 1, -8)
            },
            count = 36,
            groupMin = 3,
            groupMax = 6,
            groupGap = 2.2f,
            hpMultiplier = 2f,
            startCoins = 175,
            types = new[] { Normal, Runner, Tank, Armored, Flyer },
            weights = new[] { 2, 2, 3, 3, 3 },
            boss = true
        });

        return list;
    }
}

public class EnemySpawner : MonoBehaviour
{
    public Enemy enemyPrefab;
    public Transform pathRoot;

    const float IntraGroupGap = 0.35f;   // spacing inside a clump
    const float BossAddInterval = 0.6f;  // Runner flood while the boss is alive

    [System.NonSerialized] public List<LevelConfig> levels;

    LevelConfig cfg;
    bool running;
    bool bossSpawned;
    Enemy bossEnemy;
    int spawned;
    int groupLeft;
    float timer;
    string alert = "";
    float alertUntil;

    public bool Finished { get; private set; }

    public bool BossFight
    {
        get { return cfg != null && cfg.boss && bossSpawned && bossEnemy != null; }
    }

    public string Alert
    {
        get { return Time.time < alertUntil ? alert : ""; }
    }

    public int RemainingToSpawn
    {
        get
        {
            if (cfg == null) return 0;
            return (cfg.count - spawned) + ((cfg.boss && !bossSpawned) ? 1 : 0);
        }
    }

    void Awake()
    {
        levels = LevelData.Build();
    }

    void OnEnable() { Enemy.BossPhase += OnBossPhase; }
    void OnDisable() { Enemy.BossPhase -= OnBossPhase; }

    public void SetLevel(LevelConfig c)
    {
        StopAllCoroutines();
        cfg = c;
        running = false;
        Finished = false;
        spawned = 0;
        groupLeft = 0;
        bossSpawned = false;
        bossEnemy = null;
        alert = "";
    }

    public void Begin()
    {
        running = true;
        timer = 0.5f;
    }

    void Update()
    {
        if (!running || cfg == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        if (spawned < cfg.count)
        {
            if (groupLeft <= 0)
                groupLeft = Random.Range(cfg.groupMin, cfg.groupMax + 1);

            SpawnOne(PickType());
            spawned++;
            groupLeft--;

            if (spawned >= cfg.count && cfg.boss) timer = 4f;
            else timer = (groupLeft > 0) ? IntraGroupGap : cfg.groupGap;
        }
        else if (cfg.boss && !bossSpawned)
        {
            bossEnemy = SpawnOne(LevelData.Boss);
            bossSpawned = true;
            timer = 1.5f;
            SetAlert("THE BOSS HAS ARRIVED!");
        }
        else if (cfg.boss && bossEnemy != null)
        {
            // boss still alive: keep sending fast Runners
            SpawnOne(LevelData.Runner);
            timer = BossAddInterval;
        }
        else
        {
            running = false;
            Finished = true;
        }
    }

    // ---------- boss phases ----------
    void OnBossPhase(int phase)
    {
        if (cfg == null || !cfg.boss) return;

        if (phase == 1)
        {
            SetAlert("THE BOSS IS ANGRY! Tanks incoming!");
            StartCoroutine(Burst(LevelData.Tank, 3, 0.8f));
        }
        else
        {
            SetAlert("BOSS ENRAGED! Runner swarm!");
            StartCoroutine(Burst(LevelData.Runner, 8, 0.3f));
        }
    }

    IEnumerator Burst(EnemyType t, int n, float gap)
    {
        for (int i = 0; i < n; i++)
        {
            SpawnOne(t);
            yield return new WaitForSeconds(gap);
        }
    }

    void SetAlert(string text)
    {
        alert = text;
        alertUntil = Time.time + 4f;
    }

    // ---------- spawning ----------
    EnemyType PickType()
    {
        int total = 0;
        foreach (int w in cfg.weights) total += w;

        int roll = Random.Range(0, total);
        for (int i = 0; i < cfg.types.Length; i++)
        {
            if (roll < cfg.weights[i]) return cfg.types[i];
            roll -= cfg.weights[i];
        }
        return cfg.types[0];
    }

    Enemy SpawnOne(EnemyType t)
    {
        Vector3[] pts;
        if (t.kind == EnemyKind.Flyer)
        {
            // flyers go in a straight line, high above the map
            pts = new Vector3[]
            {
                new Vector3(cfg.path[0].x, 3.5f, cfg.path[0].z),
                new Vector3(cfg.path[cfg.path.Length - 1].x, 3.5f, cfg.path[cfg.path.Length - 1].z)
            };
        }
        else
        {
            // ground enemies stand on the ground: center height = scale
            pts = new Vector3[cfg.path.Length];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vector3(cfg.path[i].x, t.scale, cfg.path[i].z);
        }

        int hp = Mathf.Max(1, Mathf.RoundToInt(t.health * cfg.hpMultiplier));

        Enemy e = Instantiate(enemyPrefab, pts[0], Quaternion.identity);
        e.Configure(t.speed, hp, t.color, t.scale, t.reward, t.kind, t.damageMultiplier);
        e.Init(pts);
        return e;
    }
}