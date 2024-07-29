using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoMove : MonoBehaviour
{
    public float speed = 0.5f;
    private float signDistance = 0;
    public Vector3 moveDirection = Vector3.right;
    public float changeDirectionDistance = 5;
    private Vector3 origPosition;
    // Start is called before the first frame update
    void Start()
    {
        signDistance = 0;
        origPosition = gameObject.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        bool bChangeDirection = Mathf.Abs(signDistance) > changeDirectionDistance;
        if (bChangeDirection)
            moveDirection = -moveDirection;
        Vector3 position = gameObject.transform.position + moveDirection * speed * Time.deltaTime;
        signDistance = (position - origPosition).magnitude * Mathf.Sign(moveDirection.x);
        gameObject.transform.position = position;
    }
}
