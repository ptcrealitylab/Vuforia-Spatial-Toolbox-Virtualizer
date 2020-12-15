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
