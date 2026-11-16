using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    int damage = 1;
    float splashRadius = 0f;
    bool used = false;

    public void Launch(Vector3 velocity, int damageAmount, float splash, Color tint)
    {
        damage = damageAmount;
        splashRadius = splash;
        GetComponent<Rigidbody>().linearVelocity = velocity;

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = tint;
        if (splash > 0f) transform.localScale *= 1.8f;

        Destroy(gameObject, 5f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        used = true;

        if (splashRadius > 0f) Explode();
        else enemy.TakeDamage(damage);

        Destroy(gameObject);
    }

    void Explode()
    {
        Vector3 center = transform.position;

        foreach (Enemy e in new List<Enemy>(Enemy.All))
        {
            if (e == null) continue;
            if (Vector3.Distance(e.transform.position, center) <= splashRadius)
                e.TakeDamage(damage, true);
        }

        Renderer r = GetComponentInChildren<Renderer>();
        Explosion.Spawn(center, splashRadius, r != null ? r.sharedMaterial : null);
    }
}