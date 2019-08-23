using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour {

    public GameObject startObject;
    public GameObject stopObject;

    public Material mat;
    

	// Use this for initialization
	void Awake () {
        mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = Color.red;
        gameObject.GetComponent<LineRenderer>().SetVertexCount(2);
        gameObject.GetComponent<LineRenderer>().material = mat;
        gameObject.GetComponent<LineRenderer>().SetColors(Color.red, Color.red);

    }
	
	// Update is called once per frame
	void Update () {
        if(startObject != null && stopObject != null)
        {

            Vector3[] points = new Vector3[2];
            points[0] = startObject.transform.position;
            points[1] = stopObject.transform.position;
            gameObject.GetComponent<LineRenderer>().SetPositions(points);

        }
    }
}
