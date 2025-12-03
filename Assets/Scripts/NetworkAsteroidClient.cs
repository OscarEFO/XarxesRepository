using System.Collections.Concurrent;
using UnityEngine;

public class NetworkAsteroidClient : MonoBehaviour
{
    public static NetworkAsteroidClient Instance { get; private set; }

    public GameObject asteroidPrefab;

    private ConcurrentQueue<SpawnInfo> asteroidQueue = new ConcurrentQueue<SpawnInfo>();

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        while (asteroidQueue.TryDequeue(out SpawnInfo info))
        {
            SpawnAsteroid(info);
        }
    }

    public void EnqueueSpawn(SpawnInfo info)
    {
        asteroidQueue.Enqueue(info);
    }

    private void SpawnAsteroid(SpawnInfo info)
    {
        GameObject a = Instantiate(
            asteroidPrefab,
            new Vector3(info.x, info.y, 0f),
            Quaternion.identity
        );

        Rigidbody2D rb = a.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = new Vector2(info.dirX, info.dirY) * info.speed;
    }

    public struct SpawnInfo
    {
        public float x, y;
        public float dirX, dirY;
        public float speed;
    }
}
