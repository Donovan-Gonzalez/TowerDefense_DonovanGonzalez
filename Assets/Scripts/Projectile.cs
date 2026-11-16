using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    int damage = 1;
    bool used = false;

    public void Launch(Vector3 velocity, int damageAmount)
    {
        damage = damageAmount;
        GetComponent<Rigidbody>().linearVelocity = velocity;
        Destroy(gameObject, 5f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        used = true;                 // this shot can never deal damage twice
        enemy.TakeDamage(damage);
        Destroy(gameObject);
    }
}