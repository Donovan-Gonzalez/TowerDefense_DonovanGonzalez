using UnityEngine;

public class Explosion : MonoBehaviour
{
    float t;
    float duration = 0.35f;
    float maxScale = 4f;

    public static void Spawn(Vector3 pos, float radius, Material template)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        if (template != null)
        {
            Material m = new Material(template);
            m.color = new Color(1f, 0.55f, 0.1f);
            go.GetComponent<Renderer>().material = m;
        }

        Explosion e = go.AddComponent<Explosion>();
        e.maxScale = radius * 2f;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / duration;
        transform.localScale = Vector3.one * Mathf.Lerp(0.3f, maxScale, k);
        if (k >= 1f) Destroy(gameObject);
    }
}