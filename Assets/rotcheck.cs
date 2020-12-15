/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rotcheck : MonoBehaviour {
    public Quaternion localRotation;
    public Quaternion rotation;
    public Vector3 axis;
    public float angle;
    public Vector3 localAxis;
    public float localAngle;


    public Matrix4x4 transformMatrix;

	// Use this for initialization
	void Start () {

        GameObject dummyParent = new GameObject();
        dummyParent.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        dummyParent.transform.rotation = Quaternion.identity;
        
        GameObject dummyChild = new GameObject();


        dummyChild.transform.position = new Vector3(Random.value, Random.value, Random.value);

        //Quaternion rot = Quaternion.AngleAxis(Random.value*360.0f, new Vector3(Random.value, Random.value, Random.value));
        Quaternion rot = Quaternion.AngleAxis(Random.value * 360.0f, new Vector3(Random.value, Random.value, Random.value));
        Vector3 trans = new Vector3(Random.value, Random.value, Random.value);



        dummyParent.transform.rotation = rot;
        dummyParent.transform.position = trans;
        dummyChild.transform.parent = dummyParent.transform;
        Debug.Log("local position of child: " + dummyChild.transform.localPosition);

        Vector3 a = Quaternion.Inverse(rot) * (dummyChild.transform.position - trans);
        Debug.Log("by multiplying: " + a);
        Debug.Log("diff: " + (dummyChild.transform.localPosition - a).magnitude);










        /*
        Debug.Log("by multiplying: ");
        Vector3 a = rot*dummyChild.transform.position + trans;

        Debug.Log(a);

        
        dummyChild.transform.parent = dummyParent.transform;
        //dummyChild.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        dummyParent.transform.rotation =rot;
        dummyParent.transform.position = trans;
        Debug.Log("by parenting: ");
        Debug.Log(dummyChild.transform.position);

        Debug.Log("diff: " + (a - dummyChild.transform.position).magnitude);
        */






	}
	
	// Update is called once per frame
	void Update () {

        localRotation = gameObject.transform.localRotation;
        rotation = gameObject.transform.rotation;
        rotation.ToAngleAxis(out angle, out axis);
        localRotation.ToAngleAxis(out localAngle, out localAxis);

        transformMatrix = gameObject.transform.worldToLocalMatrix;
    }
}
