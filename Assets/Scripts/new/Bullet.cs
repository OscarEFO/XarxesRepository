using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int ownerId; // Set by shooter

    void Start()
    {
        Destroy(gameObject, 3f);
    }
}
