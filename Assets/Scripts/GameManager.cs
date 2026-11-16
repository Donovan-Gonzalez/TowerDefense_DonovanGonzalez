using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool IsGameOver { get; private set; }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    public void EnemyReachedEnd()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Debug.Log("GAME OVER: an enemy reached the end of its path!");
        Time.timeScale = 0f;
    }
}