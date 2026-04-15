using UnityEngine;

namespace AeternalEverwatcher;

public class ProjectileMover : MonoBehaviour
{
    private Vector3 moveDirection;

    void Start()
    {
        moveDirection = transform.position.x < HeroController.instance.transform.position.x ? Vector3.right : Vector3.left;
    }
    private void Update()
    {
        transform.position += moveDirection * (Time.deltaTime * 100);
    }
}