using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerShooting : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float fireRate = 0.25f; 

    [Header("Spawn Settings")]
    public Transform firePoint; 

    private float nextFireTime = 0f;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null)
            Debug.LogError("Main Camera not found");
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0f;

        Vector3 direction = (mousePos - spawnPos).normalized;

        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = direction * projectileSpeed;
    }
}
