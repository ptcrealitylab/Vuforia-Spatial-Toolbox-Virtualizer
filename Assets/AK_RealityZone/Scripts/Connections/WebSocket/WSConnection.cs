/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
using UnityEngine;
using UnityEngine.Events;
using System;
using WebSocketSharp;
using System.Collections;
using SimpleJSON;

public class WSConnection : MonoBehaviour
{

    public bool subscribe_pose = true;

    public delegate void ReceivedData(Vector2 data, Quaternion orientation);
    public static event ReceivedData OnPoseDataReceived;

    //public string host = "ws://192.168.12.20";
    public string host = "ws://10.10.10.112";
    public int port = 9090;

    private WebSocket _ws;

    private bool rotate;
    private bool translate;
    private float angularVelocity = 0.5f;
    private float linearVelocity = 0.5f;
    private float rotateStartTime = 0f;

    private float angleToRotate = 0f;
    private float distanceToTravel = 0f;

    private Quaternion _currentRobotAngle = Quaternion.identity;
    private Vector2 _currentRobotPosition = Vector2.zero;
    private Vector3 _currentAngularVel = Vector3.zero;
    private Vector3 _currentLinearVel = Vector3.zero;
    
    public bool connected = false;

    // Use this for initialization
    void Start()
    {

        ConnectWS();

    }

    public void ConnectWS(){

        try
        {
            Debug.Log("Trying to connect...: " + host + ":" + port);

            _ws = new WebSocket(host + ":" + port);
            _ws.OnMessage += (sender, e) => this.OnMessage(e.Data);

            _ws.Connect();

            if (_ws.IsAlive)
            {
                connected = true;

                Debug.Log("Connected to " + host + ":" + port);

                if (subscribe_pose)
                {
                    this.SubscribeRobotPose();
                }
                else
                {
                    this.SubscribeCmdVel();
                }
            }
            else
            {
                Debug.Log("No connection available on " + host + ":" + port);

                connected = false;
            }
        }
        catch (Exception e)
        {
            Debug.Log("Error: " + e);
        }

    }

    public void Rotate(){

        float angleDeg = angleToRotate;

        float angleRad = angleDeg * Mathf.Deg2Rad;

        float time = angleRad / angularVelocity;

        rotate = true;

        Debug.Log("Rotate DURING: " + time);

    }

    public void MoveForward()
    {

        float distance = distanceToTravel;

        float time = distance / linearVelocity;

        translate = true;
        StartCoroutine(MoveForward(time));

    }

    public void SubscribeRobotPose(){

        string s = @"{""op"":""subscribe"",""topic"":""/robot_pose""}";

        Send(s);

    }

    public void SubscribeCmdVel()
    {

        string s = @"{""op"":""subscribe"",""topic"":""/cmd_vel""}";

        //string s = @"{""op"":""subscribe"",""topic"":""/mirEventTrigger""}";
        //string s = @"{""op"":""subscribe"",""topic"":""/robot_pose""}";
        //string s = @"{""op"":""subscribe"",""topic"":""/mircontrol/get_loggers""}";
        //string s = @"{""op"":""subscribe"",""topic"":""/mircontrol/command""}";

        Send(s);

    }

    public void MoveCommand(Vector3 linear, Vector3 angular){

        string s = @"{""op"":""publish"",""topic"":""/cmd_vel"",""msg"":{""linear"":{""x"":" + linear.x + @",""y"":" + linear.y + @",""z"":" + linear.z + @"},""angular"":{""x"":" + angular.x + @",""y"":" + angular.y + @",""z"":" + angular.z + "}}}";

        Send(s);

    }

    public void Send(string s){
        Debug.Log("Sending " + s);
        _ws.Send(s);
    }


    private void OnMessage(string s)
    {

        //Debug.Log(s);

        if ((s != null) && !s.Equals(""))
        {
            // Parse data
            JSONNode N = JSON.Parse(s);

            if (subscribe_pose){
                float posX = float.Parse(N["msg"]["position"]["x"]);
                float posY = float.Parse(N["msg"]["position"]["y"]);

                float orientationX = float.Parse(N["msg"]["orientation"]["x"]);
                float orientationY = float.Parse(N["msg"]["orientation"]["y"]);
                float orientationZ = float.Parse(N["msg"]["orientation"]["z"]);
                float orientationW = float.Parse(N["msg"]["orientation"]["w"]);

                _currentRobotPosition = new Vector2(posX, posY);
                Vector2 currentRobotOrientation = new Vector2(orientationZ, orientationW);

                _currentRobotAngle = new Quaternion(orientationX, orientationY, orientationZ, orientationW);
                //_currentRobotAngle = Vector2.Angle(new Vector2(1, 0), currentRobotOrientation);

                //Debug.Log("POSITION: " + _currentRobotPosition);
                //Debug.Log("ORIENTATION: " + GetCurrentYaw());

                OnPoseDataReceived(_currentRobotPosition, _currentRobotAngle);

            } else {

                float linearX = float.Parse(N["msg"]["linear"]["x"]);
                float linearY = float.Parse(N["msg"]["linear"]["y"]);
                float linearZ = float.Parse(N["msg"]["linear"]["z"]);

                float angularX = float.Parse(N["msg"]["angular"]["x"]);
                float angularY = float.Parse(N["msg"]["angular"]["y"]);
                float angularZ = float.Parse(N["msg"]["angular"]["z"]);

                _currentAngularVel = new Vector3(angularX, angularY, angularZ);

                _currentLinearVel = new Vector3(linearX, linearY, linearZ);

            }
        }
    }

    public float GetCurrentYaw(){

        float yaw = 2.0f * Mathf.Asin(_currentRobotAngle.z);
        if ((_currentRobotAngle.w * _currentRobotAngle.z) < 0.0){
            yaw = -Mathf.Abs(yaw);
        }

        return yaw * Mathf.Rad2Deg;
    }

    public Vector2 GetCurrentRobotPosition()
    {
        return _currentRobotPosition;
    }

    // Update is called once per frame
    void Update()
    {

        if (rotate)
        {
            Debug.Log("Rotate at: " + Time.time);
            //MoveCommand(Vector3.zero, new Vector3(angularVelocity, angularVelocity, angularVelocity));
            MoveCommand(Vector3.zero, new Vector3(angularVelocity, angularVelocity, angularVelocity));
        }

        if (translate)
        {
            Debug.Log("Move at: " + Time.time);
            MoveCommand(new Vector3(linearVelocity, linearVelocity, linearVelocity), Vector3.zero);
        }

    }

    IEnumerator Rotate(float time)
    {
        yield return new WaitForSeconds(time);
        rotate = false;
    }

    IEnumerator MoveForward(float time)
    {
        yield return new WaitForSeconds(time);
        translate = false;
    }

    private void OnApplicationQuit()
    {

        try{

            Debug.Log("Closing WebSocket");
            _ws.Close();
        
        }
        catch (Exception e)
        {

            Debug.Log("Couldn't parse MIR Response");

        }

    }


    public Vector3 GetAngularVelocity(){
        return _currentAngularVel;
    }
    public Vector3 GetLinearVelocity()
    {
        return _currentLinearVel;
    }
}
