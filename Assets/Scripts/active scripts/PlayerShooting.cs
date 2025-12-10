using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting")]
    public Transform firePoint;
    public float fireRate = 0.25f;

    private float nextFireTime = 0f;
    private Camera mainCam;
    private Player player;

    void Start()
    {
        mainCam = Camera.main;
        player = GetComponent<Player>();

        if (firePoint == null)
            Debug.LogError("[PlayerShooting] firePoint not assigned!");
    }

    void Update()
    {
        if (!player.isLocalPlayer) return;
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    private void Shoot()
    {
        if (player.clientManager == null) return;

        Vector2 origin = firePoint.position;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mousePosition);
        Vector2 dir = (mouseWorld - transform.position).normalized;

        // Send shoot packet ONLY — no bullet instantiation here
        player.clientManager.SendShoot(player.networkId, origin, dir);

        Debug.Log($"LOCAL SHOOT SENT → id={player.networkId}, origin={origin}, dir={dir}");
    }
}

