using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerShooting : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float fireRate = 0.25f; 
    public float rotationOffset = -90f;
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
        if (Mouse.current == null) return;

    
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0f;

        Vector3 direction = (mousePos - transform.position).normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset); 
        if (Mouse.current != null && Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
        {
        if (projectilePrefab == null) return;

        // Use the firePoint’s transform if available
        Transform spawnTransform = firePoint != null ? firePoint : transform;

        // Instantiate projectile with the same rotation as the fire point
        GameObject projectile = Instantiate(projectilePrefab, spawnTransform.position, spawnTransform.rotation);

        // Add velocity in the fire point's "up" direction (assuming the sprite faces up)
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = spawnTransform.up * projectileSpeed;
        }
    }

}
