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

public class FlyAround : MonoBehaviour
{
    private float theta = 0;
    private float dTheta = 0.12f;
    private float r = 1.8f;
    private float y = 2.5f;
    public GameObject scannerCenter;
    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        theta += dTheta * Time.deltaTime;
        Vector3 newPos = scannerCenter.transform.position;
        newPos.x += Mathf.Cos(theta) * r;
        newPos.y = y; // gameObject.transform.position.y;
        newPos.z += Mathf.Sin(theta) * r;
        gameObject.transform.position = newPos;
        var diff = gameObject.transform.position - scannerCenter.transform.position;
        gameObject.transform.rotation = Quaternion.LookRotation(-diff);
    }
}
