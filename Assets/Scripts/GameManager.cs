using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int startingCoins = 100;
    public bool IsGameOver { get; private set; }
    public int Coins { get; private set; }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Coins = startingCoins;
    }

    public void AddCoins(int amount) { Coins += amount; }
    public void SetCoins(int amount) { Coins = amount; }
    public bool TrySpend(int amount)
    {
        if (Coins < amount) return false;
        Coins -= amount;
        return true;
    }

    public void EnemyReachedEnd()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Debug.Log("GAME OVER");
        Time.timeScale = 0f;
    }
}