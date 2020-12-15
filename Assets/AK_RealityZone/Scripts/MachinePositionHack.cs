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

public class MachinePositionHack : MonoBehaviour {
    public GameObject AK_receiver;
    List<akplay.camInfo> camInfoList;

    public GameObject hackPosition;
    public GameObject machinePosition;

    public bool performHackMomentary = false;
    public bool performHackPermanent = false;

    public int target_serial = 0;

    bool camerasReady = false;

    // Use this for initialization
    void Start () {
		
	}


    void doHack()
    {
        camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;

        for (int cc = 0; cc < camInfoList.Count; cc++)
        {
            if(camInfoList[cc].serial == target_serial)
            {
                hackPosition.transform.parent = gameObject.transform;
                gameObject.transform.position = camInfoList[cc].visualization.transform.position;
                gameObject.transform.rotation = camInfoList[cc].visualization.transform.rotation;

                machinePosition.transform.position = hackPosition.transform.position;
                machinePosition.transform.rotation = hackPosition.transform.rotation;

                gameObject.transform.parent = null;
            }
        }
    }

    // Update is called once per frame
    void Update () {
        if (!camerasReady && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            camerasReady = true;
            camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;
        }



        if (performHackMomentary || performHackPermanent)
        {
            performHackMomentary = false;
            doHack();
        }

    }
}
