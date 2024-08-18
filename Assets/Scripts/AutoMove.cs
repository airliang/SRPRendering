using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoMove : MonoBehaviour
{
    public float speed = 0.5f;
    public float rotateSpeed = 10.0f;
    private float signDistance = 0;
    public Vector3 moveDirection = Vector3.right;
    public float changeDirectionDistance = 5;
    private Vector3 origPosition;
    private float currentRotateDegree = 0;
    // Start is called before the first frame update
    void Start()
    {
        signDistance = 0;
        origPosition = gameObject.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        currentRotateDegree += rotateSpeed * Time.deltaTime;
        //float test = 380.0f % 360.0f;
        //if (currentRotateDegree > 360.0f)
        {
            currentRotateDegree %= 360.0f;
        }
        transform.localRotation = Quaternion.AngleAxis(currentRotateDegree, Vector3.up);
        bool bChangeDirection = Mathf.Abs(signDistance) > changeDirectionDistance;
        if (bChangeDirection)
            moveDirection = -moveDirection;
        Vector3 position = gameObject.transform.position + moveDirection * speed * Time.deltaTime;
        signDistance = (position - origPosition).magnitude * Mathf.Sign(moveDirection.x);
        gameObject.transform.position = position;
    }
}
