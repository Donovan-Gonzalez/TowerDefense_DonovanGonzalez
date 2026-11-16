using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathLine : MonoBehaviour
{
    void Start()
    {
        LineRenderer line = GetComponent<LineRenderer>();
        line.positionCount = transform.childCount;

        for (int i = 0; i < transform.childCount; i++)
        {
            Vector3 p = transform.GetChild(i).position;
            p.y = 0.05f;                 // just above the ground
            line.SetPosition(i, p);
        }
    }
}