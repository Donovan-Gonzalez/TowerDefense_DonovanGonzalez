using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    enum State { Preparing, Fighting, LevelDone, Won, Lost }

    public EnemySpawner spawner;
    public TowerPlacer placer;
    public Transform pathRoot;
    public float prepTime = 15f;

    State state;
    int level = -1;
    float timer;
    TextMeshProUGUI centerText;
    TextMeshProUGUI hudText;
    readonly List<GameObject> markers = new List<GameObject>();

    void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (placer == null) placer = FindAnyObjectByType<TowerPlacer>();
        if (pathRoot == null && spawner != null) pathRoot = spawner.pathRoot;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        centerText = MakeText(canvas.transform, "CenterText", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                              Vector2.zero, new Vector2(1100, 300), 44, TextAlignmentOptions.Center);
        hudText = MakeText(canvas.transform, "LevelHud", new Vector2(1, 1), new Vector2(1, 1),
                           new Vector2(-20, -20), new Vector2(500, 100), 28, TextAlignmentOptions.TopRight);

        StartLevel(0);
    }

    TextMeshProUGUI MakeText(Transform canvas, string objName, Vector2 anchor, Vector2 pivot,
                             Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.outlineColor = Color.black;
        t.outlineWidth = 0.25f;
        return t;
    }

    // ---------- flow ----------
    void StartLevel(int i)
    {
        level = i;
        LevelConfig cfg = spawner.levels[i];

        foreach (Enemy e in new List<Enemy>(Enemy.All)) Destroy(e.gameObject);
        foreach (Tower t in FindObjectsByType<Tower>()) Destroy(t.gameObject);
        foreach (Projectile p in FindObjectsByType<Projectile>()) Destroy(p.gameObject);

        BuildPath(cfg.path);
        spawner.SetLevel(cfg);
        GameManager.Instance.SetCoins(cfg.startCoins);
        if (placer != null) placer.ResetForLevel();

        timer = prepTime;
        state = State.Preparing;
    }

    void Update()
    {
        if (Keyboard.current == null || spawner == null) return;
        GameManager gm = GameManager.Instance;

        if (state == State.Lost || state == State.Won)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame) Restart();
            return;
        }

        if (gm != null && gm.IsGameOver)
        {
            state = State.Lost;
            centerText.text = "GAME OVER\nAn enemy reached the finish!\n<size=28>Press R to restart</size>";
            return;
        }

        UpdateHud();
        LevelConfig cfg = spawner.levels[level];

        switch (state)
        {
            case State.Preparing:
                timer -= Time.deltaTime;
                centerText.text = "LEVEL " + (level + 1) + " - " + cfg.name +
                                  "\n<size=28>Place your towers! Press SPACE to start (" + Mathf.CeilToInt(Mathf.Max(0f, timer)) + "s)</size>";
                if (Keyboard.current.spaceKey.wasPressedThisFrame || timer <= 0f)
                {
                    centerText.text = "";
                    spawner.Begin();
                    state = State.Fighting;
                }
                break;

            case State.Fighting:
                if (spawner.Finished && Enemy.All.Count == 0)
                {
                    if (level >= spawner.levels.Count - 1)
                    {
                        state = State.Won;
                        centerText.text = "YOU WIN!\nAll " + spawner.levels.Count + " levels cleared!\n<size=28>Press R to play again</size>";
                    }
                    else
                    {
                        state = State.LevelDone;
                        centerText.text = "LEVEL " + (level + 1) + " COMPLETE!\n<size=28>Press SPACE for the next level</size>";
                    }
                }
                break;

            case State.LevelDone:
                if (Keyboard.current.spaceKey.wasPressedThisFrame) StartLevel(level + 1);
                break;
        }
    }

    void UpdateHud()
    {
        int left = spawner.RemainingToSpawn + Enemy.All.Count;
        hudText.text = "Level " + (level + 1) + "/" + spawner.levels.Count + "\nEnemies left: " + left;
        if (spawner.BossFight) hudText.text += "\n<color=#ff5555>DEFEAT THE BOSS!</color>";
        string a = spawner.Alert;
        if (a != "") hudText.text += "\n<color=#ffaa00>" + a + "</color>";
    }
    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- path + markers ----------
    void BuildPath(Vector3[] pts)
    {
        List<Transform> old = new List<Transform>();
        foreach (Transform c in pathRoot) old.Add(c);
        foreach (Transform c in old) { c.SetParent(null); Destroy(c.gameObject); }

        for (int i = 0; i < pts.Length; i++)
        {
            GameObject wp = new GameObject("WP" + i);
            wp.transform.SetParent(pathRoot, false);
            wp.transform.position = pts[i];
        }

        PathLine line = pathRoot.GetComponent<PathLine>();
        if (line != null) line.Refresh();

        BuildMarkers(pts[0], pts[pts.Length - 1]);
    }

    Material MakeMat(Color c)
    {
        Material m;
        GameObject g = GameObject.Find("Ground");
        Renderer gr = g != null ? g.GetComponent<Renderer>() : null;
        if (gr != null) m = new Material(gr.sharedMaterial);
        else m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.mainTexture = null;
        m.color = c;
        return m;
    }

    GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color c)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = MakeMat(c);
        markers.Add(go);
        return go;
    }

    void Label(string text, Vector3 pos, Color c)
    {
        GameObject go = new GameObject("Label_" + text);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 9;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = c;
        tmp.rectTransform.sizeDelta = new Vector2(10, 2);
        go.transform.position = pos;
        if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
        markers.Add(go);
    }

    void BuildMarkers(Vector3 start, Vector3 end)
    {
        foreach (GameObject g in markers) if (g != null) Destroy(g);
        markers.Clear();

        // START: green pad
        Prim(PrimitiveType.Cylinder, new Vector3(start.x, 0.06f, start.z), new Vector3(4.4f, 0.06f, 4.4f), new Color(0.2f, 0.85f, 0.3f));
        Label("START", new Vector3(start.x, 3f, start.z), new Color(0.75f, 1f, 0.75f));

        // FINISH: red pad + flag
        Prim(PrimitiveType.Cylinder, new Vector3(end.x, 0.06f, end.z), new Vector3(4.4f, 0.06f, 4.4f), new Color(0.9f, 0.2f, 0.2f));
        Prim(PrimitiveType.Cylinder, new Vector3(end.x, 2f, end.z), new Vector3(0.2f, 2f, 0.2f), new Color(0.9f, 0.9f, 0.9f));
        Prim(PrimitiveType.Cube, new Vector3(end.x + 0.9f, 3.4f, end.z), new Vector3(1.6f, 0.9f, 0.1f), new Color(0.95f, 0.15f, 0.15f));
        Label("FINISH", new Vector3(end.x, 5f, end.z), new Color(1f, 0.8f, 0.8f));
    }
}