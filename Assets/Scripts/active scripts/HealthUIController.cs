using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : MonoBehaviour
{
    [Header("References")]
    public ClientManagerUDP clientManager;

    [Header("UI Images")]
    public Image localHealthImage;
    public Image enemyHealthImage;

    [Header("Sprites")]
    public Sprite[] localHealthSprites;
    public Sprite[] enemyHealthSprites;

    private Player localPlayer;
    private Player enemyPlayer;

    void Start()
    {
        if (clientManager == null)
            clientManager = FindObjectOfType<ClientManagerUDP>();

        // Cache players
        localPlayer = clientManager != null ? clientManager.localPlayer : null;
        enemyPlayer = FindEnemyPlayer();

        // Inicializar UI SOLO UNA VEZ
        if (localPlayer != null)
            UpdateLocalHealth(localPlayer.currentHealth);

        if (enemyPlayer != null)
            UpdateEnemyHealth(enemyPlayer.currentHealth);
    }

    private Player FindEnemyPlayer()
    {
        foreach (var p in FindObjectsOfType<Player>())
        {
            if (!p.isLocalPlayer)
                return p;
        }
        return null;
    }

    // -------------------------
    // PUBLIC API (called by Player)
    // -------------------------
    public void UpdateHealth(bool isLocalPlayer, int hp)
    {
        if (hp <= 0)
            return; // muerto → no hace falta UI

        if (isLocalPlayer)
            UpdateLocalHealth(hp);
        else
            UpdateEnemyHealth(hp);
    }

    // -------------------------
    // INTERNAL UI UPDATE
    // -------------------------
    private void UpdateLocalHealth(int hp)
    {
        hp = Mathf.Clamp(hp, 1, localHealthSprites.Length - 1);
        localHealthImage.sprite = localHealthSprites[hp];
    }

    private void UpdateEnemyHealth(int hp)
    {
        hp = Mathf.Clamp(hp, 1, enemyHealthSprites.Length - 1);
        enemyHealthImage.sprite = enemyHealthSprites[hp];
    }
}
