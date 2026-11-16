using UnityEngine;

public class Tower : MonoBehaviour
{
    public string towerName = "Tower";
    public float range = 12f;
    public float shotsPerSecond = 1f;
    public float projectileSpeed = 20f;
    public int damage = 1;
    public Projectile projectilePrefab;

    float cooldown;

    void Update()
    {
        if (GameManager.Instance.IsGameOver) return;

        cooldown -= Time.deltaTime;
        if (cooldown > 0f) return;

        Enemy target = FindTarget();
        if (target == null) return;

        // Spawn the shot just above the top of the tower
        Vector3 muzzle = transform.position + Vector3.up * (transform.localScale.y * 0.5f + 0.3f);

        // Aim where the enemy WILL be, not where it is now
        Vector3 aimPoint = PredictIntercept(muzzle, target.transform.position, target.Velocity, projectileSpeed);
        Vector3 direction = (aimPoint - muzzle).normalized;

        Projectile shot = Instantiate(projectilePrefab, muzzle, Quaternion.identity);
        shot.Launch(direction * projectileSpeed, damage);

        cooldown = 1f / shotsPerSecond;
    }

    Enemy FindTarget()
    {
        Enemy best = null;
        float bestDistance = range * range;

        foreach (Enemy enemy in Enemy.All)
        {
            Vector3 offset = enemy.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }
        return best;
    }

    // Solves |targetPos + targetVel * t - shooter| = speed * t for the earliest t > 0
    static Vector3 PredictIntercept(Vector3 shooter, Vector3 targetPos, Vector3 targetVel, float speed)
    {
        Vector3 d = targetPos - shooter;
        float a = Vector3.Dot(targetVel, targetVel) - speed * speed;
        float b = 2f * Vector3.Dot(d, targetVel);
        float c = Vector3.Dot(d, d);

        float t;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f) return targetPos;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return targetPos;

            float root = Mathf.Sqrt(disc);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);
            float low = Mathf.Min(t1, t2);
            t = low > 0f ? low : Mathf.Max(t1, t2);
        }

        if (t <= 0f) return targetPos;
        return targetPos + targetVel * t;
    }
}