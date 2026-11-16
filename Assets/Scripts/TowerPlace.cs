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
    public int maxTowers = 6;
    public float pathClearance = 2.5f;
    public float towerSpacing = 3f;

    int selected = 0;
    int placed = 0;
    string message = "";
    readonly List<Vector3> placedPositions = new List<Vector3>();
    readonly List<Image> buttonImages = new List<Image>();
    Plane ground = new Plane(Vector3.up, Vector3.zero);

    static readonly Color normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    static readonly Color selectedColor = new Color(0.9f, 0.7f, 0.1f, 0.95f);

    void Start()
    {
        if (infoText == null)
            Debug.LogWarning("TowerPlacer: Info Text is not assigned!");
        else
            BuildMenu(infoText.canvas.transform);

        Select(0);
    }

    // ---------- Menu (bottom of the screen, always visible) ----------
    void BuildMenu(Transform canvas)
    {
        const float w = 230f, h = 90f, gap = 10f;
        int n = towerPrefabs.Length;

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
            int index = i; // copy for the click handler

            GameObject btn = new GameObject("Btn_" + t.towerName, typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(panel.transform, false);
            RectTransform br = btn.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0, 0);
            br.anchoredPosition = new Vector2(i * (w + gap), 0);
            br.sizeDelta = new Vector2(w, h);

            Image img = btn.GetComponent<Image>();
            img.color = normalColor;
            buttonImages.Add(img);
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
            label.text = (i + 1) + ": " + t.towerName +
                         "\n<size=16>Range " + t.range + "  |  " + t.shotsPerSecond + "/s  |  Dmg " + t.damage + "</size>";
        }
    }

    // ---------- Input ----------
    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) Select(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) Select(1);
        if (Keyboard.current.tabKey.wasPressedThisFrame) Select((selected + 1) % towerPrefabs.Length);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // don't place a tower when clicking a menu button
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            TryPlace();
        }
    }

    public void Select(int index)
    {
        if (towerPrefabs == null || index < 0 || index >= towerPrefabs.Length || towerPrefabs[index] == null)
        {
            Debug.LogWarning("TowerPlacer: no tower assigned in slot " + index);
            message = "No tower in slot " + (index + 1) + "!";
            RefreshText();
            return;
        }

        selected = index;
        message = "";
        Debug.Log("Selected tower: " + towerPrefabs[selected].towerName);
        RefreshText();
    }

    // ---------- Placing ----------
    void TryPlace()
    {
        if (towerPrefabs == null || selected >= towerPrefabs.Length || towerPrefabs[selected] == null) return;

        if (placed >= maxTowers)
        {
            message = "No towers left!";
            RefreshText();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!ground.Raycast(ray, out float dist)) return;
        Vector3 point = ray.GetPoint(dist);

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

        Tower prefab = towerPrefabs[selected];
        Vector3 spawnPos = new Vector3(point.x, prefab.transform.localScale.y * 0.5f, point.z);
        Instantiate(prefab, spawnPos, Quaternion.identity);
        placedPositions.Add(spawnPos);
        placed++;

        message = placed >= maxTowers ? "All towers placed!" : "Placed " + prefab.towerName + "!";
        Debug.Log("Placed " + prefab.towerName + " (" + placed + "/" + maxTowers + ")");
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

    // ---------- Overlay + menu highlight ----------
    void RefreshText()
    {
        // highlight the selected button
        for (int i = 0; i < buttonImages.Count; i++)
            buttonImages[i].color = (i == selected) ? selectedColor : normalColor;

        if (infoText == null) return;

        string s = "Click a tower below (or press 1 / 2 / Tab), then click the ground to place it\n";
        s += "Towers left: " + (maxTowers - placed);
        if (!string.IsNullOrEmpty(message)) s += "\n" + message;
        infoText.text = s;
    }
}