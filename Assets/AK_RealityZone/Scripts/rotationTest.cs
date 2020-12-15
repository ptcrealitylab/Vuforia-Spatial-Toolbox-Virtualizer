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

public class rotationTest : MonoBehaviour {


    public GameObject O1;
    public GameObject O2;
    public GameObject O3;
    public GameObject O4;

    // Use this for initialization
    void Start () {
		
	}

    void getTransform(Quaternion startQ, Quaternion stopQ, Vector3 startT, Vector3 stopT, out Quaternion Qe, out Vector3 Te)
    {
        GameObject dummyParent = new GameObject();
        dummyParent.transform.rotation = startQ;
        dummyParent.transform.position = startT;

        GameObject dummyChild = new GameObject();
        dummyChild.transform.rotation = stopQ;
        dummyChild.transform.position = stopT;

        dummyChild.transform.parent = dummyParent.transform;

        Qe = dummyChild.transform.localRotation;
        Te = dummyChild.transform.localPosition;

        GameObject.Destroy(dummyParent);
        GameObject.Destroy(dummyChild);

        //Qe = stopQ * Quaternion.Inverse(startQ);
        //Te = stopT - startT;
        //Te = stopT - stopQ * Quaternion.Inverse(startQ) * startT;
    }

    // Update is called once per frame
    void Update () {

        Quaternion Q1 = O1.transform.rotation;
        Vector3 T1 = O1.transform.position;

        Quaternion Q2 = O2.transform.rotation;
        Vector3 T2 = O2.transform.position;

        Quaternion Q3 = O3.transform.rotation;
        Vector3 T3 = O3.transform.position;

        Quaternion Q4 = O4.transform.rotation;
        Vector3 T4 = O4.transform.position;

        Quaternion Qe = Quaternion.identity;
        Vector3 Te = new Vector3();
        getTransform(Q1, Q2, T1, T2, out Qe, out Te);

        O4.transform.rotation = O3.transform.rotation * Qe;
        O4.transform.position = O3.transform.rotation*Te + O3.transform.position;


        //Quaternion Qf = Q2 * Quaternion.Inverse(Q1);
        //Vector3 Tf = -(Q2 * Quaternion.Inverse(Q1) * T1) + T2;

        /*
        Debug.Log("Q2: " + Q2);
        Debug.Log("T2: " + T2);

        Debug.Log("Q1F: " + Qf * Q1);
        Debug.Log("T1F: " + (Qf * T1 + Tf));
        */


        //O4.transform.rotation = Qf * O3.transform.rotation;
        //O4.transform.position = (T2 - T1) + O3.transform.position;

        /*
        O4.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        O4.transform.rotation = Qf * O3.transform.rotation;
        O4.transform.position = O3.transform.position + (O2.transform.position - O1.transform.position);
        */

        //O4.transform.rotation = Qf*O3.transform.rotation;
        //O4.transform.position = O3.transform.position + Tf;

    }
}
