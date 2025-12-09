//using UnityEngine;
//using UnityEngine.InputSystem;

//[RequireComponent(typeof(PlayerMovement))]
//public class PlayerShooting : MonoBehaviour
//{
//    public Transform firePoint;
//    public float rotationOffset = -90f;
//    public float fireRate = 0.25f;

//    private float nextFireTime = 0f;
//    private Camera mainCam;
//    private PlayerMovement pm;

//    void Start()
//    {
//        mainCam = Camera.main;
//        pm = GetComponent<PlayerMovement>();

//        if (!mainCam)
//            Debug.LogError("[PlayerShooting] No Camera found");
//    }

//    void Update()
//    {
//        if (Mouse.current == null) return;

//        // Rotate to mouse
//        Vector3 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
//        mousePos.z = 0;

//        Vector3 dir = (mousePos - transform.position).normalized;
//        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
//        transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);

//        // Fire
//        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
//        {
//            nextFireTime = Time.time + fireRate;
//            Shoot();
//        }
//    }

//    void Shoot()
//    {
//        if (firePoint == null) return;

//        Vector2 pos = firePoint.position;
//        float rotZ = transform.eulerAngles.z;
//        Vector2 dir = transform.up;

//        if (pm.applyMovementLocally == true)
//        {

//            ServerUDP server = FindObjectOfType<ServerUDP>();
//            if (server != null)
//                server.EnqueueServerShot(pos, rotZ, dir);
//        }
//        else
//        {
//            // --- CLIENT SHOOTING (original) ---
//            if (NetworkShootingClient.Instance != null)
//                NetworkShootingClient.Instance.RequestSpawn(pos, rotZ, dir);
//        }
//    }
//}
