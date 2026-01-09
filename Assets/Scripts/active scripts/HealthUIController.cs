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

    void Start()
    {
        if (clientManager == null)
            clientManager = FindObjectOfType<ClientManagerUDP>();

    }


    public void UpdateHealth(bool isLocalPlayer, int hp)
    {
        if (hp <= 0)
            return;

        if (isLocalPlayer)
            UpdateLocalHealth(hp);
        else
            UpdateEnemyHealth(hp);
    }


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
