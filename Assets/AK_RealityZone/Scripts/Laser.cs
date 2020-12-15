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
