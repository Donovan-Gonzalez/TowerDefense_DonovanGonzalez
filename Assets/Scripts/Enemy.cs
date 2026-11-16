using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public float speed = 3f;
    public int maxHealth = 3;

    Transform[] path;
    int nextIndex = 1;
    int health;

    // Used by towers for predictive aiming
    public Vector3 Velocity
    {
        get
        {
            if (path == null || nextIndex >= path.Length) return Vector3.zero;
            Vector3 d = path[nextIndex].position - transform.position;
            d.y = 0;
            return d.normalized * speed;
        }
    }

    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Called by the spawner right after Instantiate (before Init)
    public void Configure(float newSpeed, int newHealth, Color color, float scale)
    {
        speed = newSpeed;
        maxHealth = newHealth;
        transform.localScale *= scale;

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = color;
    }

    public void Init(Transform[] waypoints)
    {
        path = waypoints;
        transform.position = path[0].position;
        nextIndex = 1;
        health = maxHealth;
    }

    void Update()
    {
        if (path == null || nextIndex >= path.Length) return;

        Vector3 target = path[nextIndex].position;
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

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0) Destroy(gameObject);
    }
}