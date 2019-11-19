using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointTowards : MonoBehaviour {
    private Camera target;
	// Use this for initialization
	void Start () {
        target = Camera.main;
	}
	
	// Update is called once per frame
	void OnRenderObject() {
        gameObject.transform.rotation = Camera.current.transform.rotation;
	}
}
