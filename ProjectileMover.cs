using UnityEngine;

namespace AeternalEverwatcher;

public class ProjectileMover : MonoBehaviour
{
    private Vector3 moveDirection;

    private void OnEnable()
    {
        moveDirection = transform.position.x < HeroController.instance.transform.position.x ? Vector3.right : Vector3.left;
        transform.localScale = new Vector3(moveDirection.x * -1.75f, Random.Range(1.8f, 2.2f), 1);
        transform.SetLocalRotation2D(Random.Range(-10, 10));
    }
    private void Update()
    {
        transform.position += moveDirection * (Time.deltaTime * 100);
    }
}