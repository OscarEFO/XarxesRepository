using System.Collections.Concurrent;
using UnityEngine;

public class NetworkShootingClient : MonoBehaviour
{
    public static NetworkShootingClient Instance { get; private set; }

    [Header("Projectile prefab (client-side replica)")]
    public GameObject projectilePrefab;

    // queue populated by ClientUDP.ReceiveLoop (thread) — consumed in Update on main thread
    private ConcurrentQueue<SpawnInfo> spawnQueue = new ConcurrentQueue<SpawnInfo>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        while (spawnQueue.TryDequeue(out SpawnInfo info))
        {
            InstantiateProjectileLocal(info);
        }
    }

    public void RequestSpawn(Vector2 pos, float rotZ, Vector2 dir)
    {
        ClientUDP.Instance?.SendShootRequest(pos, rotZ, dir);
    }

    // Called by ClientUDP when server sends spawn message
    public void EnqueueSpawn(SpawnInfo info)
    {
        spawnQueue.Enqueue(info);
    }

    void InstantiateProjectileLocal(SpawnInfo info)
    {
        if (projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab, new Vector3(info.x, info.y, 0f), Quaternion.Euler(0f, 0f, info.rotationZ));
        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = new Vector2(info.dirX, info.dirY).normalized * info.speed;
    }

    public struct SpawnInfo
    {
        public float x, y;
        public float rotationZ;
        public float dirX, dirY;
        public float speed;
    }
}
