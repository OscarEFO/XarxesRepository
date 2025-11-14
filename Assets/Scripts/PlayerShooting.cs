using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform firePoint;
    public float rotationOffset = -90f;
    public float fireRate = 0.25f;
    private float nextFireTime = 0f;

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null) Debug.LogError("[PlayerShooting] Main Camera not found");
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0f;

        Vector3 direction = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            RequestShoot();
        }
    }

    void RequestShoot()
    {
        if (NetworkShootingClient.Instance == null)
            return;

        if (firePoint == null)
            return;

        Vector2 pos = firePoint.position;
        float rotZ = transform.eulerAngles.z;
        Vector2 dir = transform.up; // direction vector

        NetworkShootingClient.Instance.RequestSpawn(pos, rotZ, dir);
    }
}
