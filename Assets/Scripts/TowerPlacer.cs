using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class TowerPlacer : MonoBehaviour
{
    [Header("Setup")]
    public Tower[] towerPrefabs;
    public Transform pathRoot;
    public TMP_Text infoText;

    [Header("Limits")]
    public int maxTowers = 12;
    public float pathClearance = 2.5f;
    public float towerSpacing = 3f;

    int selected = 0;
    int placed = 0;
    int lastCoins = -1;
    bool shownGameOver = false;
    string message = "";

    readonly List<Vector3> placedPositions = new List<Vector3>();
    Image[] buttonImages;
    TextMeshProUGUI[] buttonLabels;
    Plane ground = new Plane(Vector3.up, Vector3.zero);

    static readonly Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    static readonly Color selectedColor = new Color(0.9f, 0.7f, 0.1f, 0.95f);
    static readonly Color lockedColor = new Color(0.35f, 0.12f, 0.12f, 0.85f);

    void Start()
    {
        if (infoText == null)
            Debug.LogWarning("TowerPlacer: Info Text is not assigned!");
        else
            BuildMenu(infoText.canvas.transform);

        Select(0);
    }
    public void ResetForLevel()
    {
        placed = 0;
        placedPositions.Clear();
        message = "";
        RefreshText();
    }
    int Coins { get { return GameManager.Instance != null ? GameManager.Instance.Coins : 0; } }

    // ---------- Menu ----------
    void BuildMenu(Transform canvas)
    {
        const float w = 230f, h = 90f, gap = 10f;
        int n = towerPrefabs.Length;
        buttonImages = new Image[n];
        buttonLabels = new TextMeshProUGUI[n];

        GameObject panel = new GameObject("TowerMenu", typeof(RectTransform));
        panel.transform.SetParent(canvas, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0, 20);
        pr.sizeDelta = new Vector2(n * w + (n - 1) * gap, h);

        for (int i = 0; i < n; i++)
        {
            Tower t = towerPrefabs[i];
            if (t == null) continue;
            int index = i;

            GameObject btn = new GameObject("Btn_" + t.towerName, typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(panel.transform, false);
            RectTransform br = btn.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0, 0);
            br.anchoredPosition = new Vector2(i * (w + gap), 0);
            br.sizeDelta = new Vector2(w, h);

            buttonImages[i] = btn.GetComponent<Image>();
            btn.GetComponent<Button>().onClick.AddListener(() => Select(index));

            GameObject txt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txt.transform.SetParent(btn.transform, false);
            RectTransform tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 4); tr.offsetMax = new Vector2(-8, -4);

            TextMeshProUGUI label = txt.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22;
            label.color = Color.white;
            label.raycastTarget = false;
            buttonLabels[i] = label;
        }
    }

    // ---------- Input ----------
    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            if (!shownGameOver)
            {
                shownGameOver = true;
                message = "GAME OVER - an enemy got through!";
                RefreshText();
            }
            return;
        }

        if (Coins != lastCoins) RefreshText();   // coins changed (kill reward / purchase)

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) Select(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) Select(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) Select(2);
        if (Keyboard.current.tabKey.wasPressedThisFrame) Select((selected + 1) % towerPrefabs.Length);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            TryPlace();
        }
    }

    public void Select(int index)
    {
        if (towerPrefabs == null || index < 0 || index >= towerPrefabs.Length || towerPrefabs[index] == null)
        {
            message = "No tower in slot " + (index + 1) + "!";
            RefreshText();
            return;
        }

        selected = index;
        message = "";
        RefreshText();
    }

    // ---------- Placing ----------
    void TryPlace()
    {
        if (towerPrefabs == null || selected >= towerPrefabs.Length || towerPrefabs[selected] == null) return;
        Tower prefab = towerPrefabs[selected];

        if (placed >= maxTowers)
        {
            message = "No towers left!";
            RefreshText();
            return;
        }

        if (Coins < prefab.cost)
        {
            message = "Not enough coins! " + prefab.towerName + " costs " + prefab.cost + ".";
            RefreshText();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!ground.Raycast(ray, out float dist)) return;
        Vector3 point = ray.GetPoint(dist);

        GameObject g = GameObject.Find("Ground");
        if (g != null)
        {
            Bounds gb = g.GetComponent<Renderer>().bounds;
            if (point.x < gb.min.x + 1f || point.x > gb.max.x - 1f ||
                point.z < gb.min.z + 1f || point.z > gb.max.z - 1f)
            {
                message = "Can't build outside the walls!";
                RefreshText();
                return;
            }
        }

        if (IsNearPath(point))
        {
            message = "Too close to the road!";
            RefreshText();
            return;
        }

        foreach (Vector3 p in placedPositions)
        {
            Vector3 a = new Vector3(p.x, 0, p.z);
            Vector3 b = new Vector3(point.x, 0, point.z);
            if (Vector3.Distance(a, b) < towerSpacing)
            {
                message = "Too close to another tower!";
                RefreshText();
                return;
            }
        }

        GameManager.Instance.TrySpend(prefab.cost);

        Vector3 spawnPos = new Vector3(point.x, 0f, point.z);
        Instantiate(prefab, spawnPos, Quaternion.identity);
        placedPositions.Add(spawnPos);
        placed++;

        message = placed >= maxTowers ? "All towers placed!" : "Placed " + prefab.towerName + "!";
        RefreshText();
    }

    bool IsNearPath(Vector3 point)
    {
        if (pathRoot == null || pathRoot.childCount < 2) return false;

        for (int i = 0; i < pathRoot.childCount - 1; i++)
        {
            Vector3 a = pathRoot.GetChild(i).position;
            Vector3 b = pathRoot.GetChild(i + 1).position;
            if (DistanceToSegment(point, a, b) < pathClearance) return true;
        }
        return false;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = 0; a.y = 0; b.y = 0;
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return Vector3.Distance(p, a + ab * t);
    }

    // ---------- UI refresh ----------
    void RefreshText()
    {
        lastCoins = Coins;

        if (buttonImages != null)
        {
            for (int i = 0; i < towerPrefabs.Length; i++)
            {
                if (towerPrefabs[i] == null || buttonImages[i] == null) continue;
                Tower t = towerPrefabs[i];
                bool affordable = Coins >= t.cost;

                buttonImages[i].color = (i == selected) ? selectedColor : (affordable ? normalColor : lockedColor);

                buttonLabels[i].text = (i + 1) + ": " + t.towerName + "  -  $" + t.cost +
                    "\n<size=16>" + (affordable ? "" : "[LOCKED] ") +
                    "Rng " + t.range + " | " + t.shotsPerSecond + "/s | Dmg " + t.damage + "</size>";
            }
        }

        if (infoText == null) return;

        string s = "Click a tower below (or press 1 / 2 / 3 / Tab), then click the ground to place it\n";
        s += "Coins: " + Coins + "     Towers left: " + (maxTowers - placed);
        if (!string.IsNullOrEmpty(message)) s += "\n" + message;
        infoText.text = s;
    }
}