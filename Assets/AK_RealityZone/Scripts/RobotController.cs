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

public class RobotController : MonoBehaviour
{

    public GameObject robotDummy;
    public GameObject timeDummy;
    public LineRenderer line;

    //private WSConnection websocket;

    private List<Point> motionPoints;

    private Vector3 lastPos;
    private Vector3 currentPos;
    private Quaternion currentOri;

    private bool current = true;

    private int timeIndex;

    // Start is called before the first frame update
    void Start()
    {

        //websocket = GetComponent<WSConnection>();

        motionPoints = new List<Point>();
        robotDummy = this.gameObject;
        lastPos = robotDummy.transform.position;

    }
    /*
    void OnEnable()
    {
        WSConnection.OnPoseDataReceived += DataReceived;
    }

    void OnDisable()
    {
        WSConnection.OnPoseDataReceived -= DataReceived;
    }
    
    public void DataReceived(Vector2 position, Quaternion orientation)
    {

        currentPos = new Vector3(position.x / 5, position.y / 5, -0.05f);
        currentOri = orientation;

    }
    */
    // Update is called once per frame
    void Update()
    {
        currentPos = robotDummy.transform.position;
        //robotDummy.transform.localPosition = currentPos;
        //robotDummy.transform.localRotation = currentOri;
        

        if (Vector3.Distance(currentPos, lastPos) > 0.05f)
        {

            Point newPoint = new Point(robotDummy.transform.position, Time.timeSinceLevelLoad);

            motionPoints.Add(newPoint);
            lastPos = currentPos;

            if (line.positionCount < motionPoints.Count) line.positionCount++;

            line.SetPosition(motionPoints.Count - 1, robotDummy.transform.position);
        }

        /*
        if (current)
        {
            timeDummy.transform.position = robotDummy.transform.position;
        }
        else
        {
            timeDummy.transform.position = motionPoints[timeIndex].position;
        }*/
    }

    public void ScrollTime(float sliderValue)
    {
        current = false;
        timeIndex = (int) Mathf.Floor(motionPoints.Count * sliderValue);
    }

    public void ResetCurrentTime()
    {
        current = true;
    }

    private struct Point
    {
        public Vector3 position;
        public float timeStamp;
        public Point(Vector3 pos, float time)
        {
            position = pos;
            timeStamp = time;
        }
    }
}


