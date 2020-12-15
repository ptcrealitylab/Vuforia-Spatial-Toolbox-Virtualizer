/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
using UnityEngine;
using SimpleJSON;
using Dpoch.SocketIO;

public class ObjectPositionSender : MonoBehaviour {
    private SocketIO socket;
    private bool connected;
    public string objectServerUrl = "http://127.0.0.1:8080";

    void Start()
    {
        // socket = GetComponent(typeof(SocketIOComponent)) as SocketIOComponent;
        /*
        SocketOptions options = new SocketOptions();
        options.AutoConnect = false;

        Manager = new SocketManager(new System.Uri(objectServerUrl + "/socket.io/"), options);
        Manager.Socket.On(SocketIOEventTypes.Connect, OnConnected);
        Manager.Socket.On(SocketIOEventTypes.Disconnect, OnDisconnected);

        Manager.Socket.On(SocketIOEventTypes.Error, (socket, packet, args) => Debug.LogError(string.Format("Error: {0}", args[0].ToString())));

        Manager.Open();
        */
    }

    void Update()
    {
    }


    void OnDestroy()
    {
        // socket.Close();
    }

    private void OnConnected(SocketIOEvent e)
    {
        //identify yourself as the station.
        Debug.Log("OPS connected to server");
        connected = true;
    }

    private void OnDisconnected(SocketIOEvent e)
    {
        connected = false;
        Debug.Log("OPS server disconnected");
    }

    public void SendSkeleton(JSONArray skeletons)
    {
        if (!connected)
        {
            return;
        }

        // socket.Emit(skeletons.ToString());
        // Manager.Socket.Emit("/update/humanPoses", skeletons.ToString());
        /** The following code would use a fancier API for generic positioning instead
         of just sending raw skeleton data
        foreach (var skeleton in skeletons.Values)
        {
            string topLevelKey = "human" + skeleton["id"].ToString();
            
            foreach (var joint in skeleton["joints"])
            {
                string objectKey = topLevelKey + "joint" + joint.Key;
                double x = joint.Value["x"].AsDouble * 1000;
                double y = joint.Value["y"].AsDouble * 1000;
                double z = joint.Value["z"].AsDouble * 1000;
                JSONObject updateMsg = new JSONObject();
                updateMsg["objectKey"] = objectKey;
                updateMsg["position"] = new JSONObject();
                updateMsg["position"]["x"] = x;
                updateMsg["position"]["y"] = y;
                updateMsg["position"]["z"] = z;
                updateMsg["rotationInRadians"] = 0;
                updateMsg["editorId"] = "test";
                Manager.Socket.Emit("/update/object/position", updateMsg.ToString());
            }
        }
        */
    }
}
