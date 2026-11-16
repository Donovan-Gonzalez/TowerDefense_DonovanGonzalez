using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyType
{
    public string name = "Normal";
    public float speed = 3f;
    public int health = 3;
    public Color color = Color.red;
    public float scale = 1f;
    public int weight = 1;   // higher = spawns more often
}

public class EnemySpawner : MonoBehaviour
{
    public Enemy enemyPrefab;
    public Transform pathRoot;
    public float spawnInterval = 2f;

    public EnemyType[] types =
    {
        new EnemyType { name = "Normal", speed = 3f,   health = 3,  color = new Color(0.85f, 0.2f, 0.2f), scale = 1f,   weight = 4 },
        new EnemyType { name = "Runner", speed = 6f,   health = 2,  color = new Color(1f, 0.9f, 0.2f),    scale = 0.7f, weight = 3 },
        new EnemyType { name = "Tank",   speed = 1.5f, health = 10, color = new Color(0.2f, 0.3f, 0.9f),  scale = 1.4f, weight = 2 },
    };

    Transform[] waypoints;
    float timer;

    void Start()
    {
        List<Transform> list = new List<Transform>();
        foreach (Transform child in pathRoot) list.Add(child);
        waypoints = list.ToArray();
        timer = spawnInterval;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Spawn();
            timer = spawnInterval;
        }
    }

    void Spawn()
    {
        EnemyType t = PickType();
        Enemy e = Instantiate(enemyPrefab, waypoints[0].position, Quaternion.identity);
        e.Configure(t.speed, t.health, t.color, t.scale);
        e.Init(waypoints);
    }

    EnemyType PickType()
    {
        int total = 0;
        foreach (EnemyType t in types) total += t.weight;

        int roll = Random.Range(0, total);
        foreach (EnemyType t in types)
        {
            if (roll < t.weight) return t;
            roll -= t.weight;
        }
        return types[0];
    }
}