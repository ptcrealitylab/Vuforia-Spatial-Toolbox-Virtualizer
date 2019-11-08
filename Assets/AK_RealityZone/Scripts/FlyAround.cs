using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyAround : MonoBehaviour
{
    private float theta = 0;
    private float dTheta = 0.12f;
    private float r = 3;
    private float y = 3;
    public GameObject scannerCenter;
    // Use this for initialization
    void Start()
    {
        var diff = gameObject.transform.position - scannerCenter.transform.position;
        diff.y = 0;
        r = diff.magnitude;
    }

    // Update is called once per frame
    void Update()
    {
        theta += dTheta * Time.deltaTime;
        Vector3 newPos = scannerCenter.transform.position;
        newPos.x += Mathf.Cos(theta) * r;
        newPos.y = gameObject.transform.position.y;
        newPos.z += Mathf.Sin(theta) * r;
        gameObject.transform.position = newPos;
        var diff = gameObject.transform.position - scannerCenter.transform.position;
        gameObject.transform.rotation = Quaternion.LookRotation(-diff);
    }
}
