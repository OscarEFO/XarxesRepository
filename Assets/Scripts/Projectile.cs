using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int ownerId;         // Assigned when spawned
    public int damage = 1;
    public float lifetime = 3f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }
}
