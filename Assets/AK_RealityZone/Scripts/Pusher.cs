/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//server bridge for the reality zone
//written by Hisham Bedri, Reality Lab, 2019

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using BestHTTP.SocketIO;
using Dpoch.SocketIO;
using SimpleJSON;
using System.Threading.Tasks;

public class Pusher : MonoBehaviour {


    private SocketIO socket;
    public GameObject pusherOrigin;

    public akplay player;
    public ShadingController shadingController;

    //modules it needs to communicate with:
    public GameObject cloner;
    public GameObject gifRecorder;

    public Shader depthShader;
    private Material depthMat;

    private RenderTexture rt;
    public bool sendColorOnly = false;
    private Task pushTask = Task.CompletedTask;

    public bool connected = false;

    private bool inDemoMode = false;

    //public Vector3 debugVector;
    // Use this for initialization
    void Start () {
        socket = new SocketIO("ws://127.0.0.1:3020/socket.io/?EIO=4&transport=websocket");
        depthMat = new Material(depthShader);
        emergencyTex = new Texture2D(2, 2);

        rt = new RenderTexture(resWidth, resHeight, 32);

        socket.OnOpen += OnConnected;
        // socket.OnConnect += OnConnected;
        socket.OnClose += OnDisconnected;
        socket.OnError += (err) => Debug.Log("Socket Error: " + err);
        // socket.On("disconnect", OnDisconnected);
        socket.On("message", OnMessage);
        socket.On("resolution", OnResolution);
        socket.On("resolutionPhone", OnPhoneResolution);
        socket.On("pose", OnPose);
        socket.On("poseVuforia", OnPoseVuforia);
        socket.On("poseVuforiaDesktop", OnPoseVuforiaDesktop);
        socket.On("cameraPosition", OnCameraPosition);

        socket.On("vuforiaResult_server_system", On_vuforiaResult_server_system);
        socket.On("realityEditorObject_server_system", On_realityEditorObject_server_system);

        socket.On("startRecording_server_system", On_startRecording_server_system);
        socket.On("stopRecording_server_system", On_stopRecording_server_system);
        socket.On("twin_server_system", On_twin_server_system);
        socket.On("clearTwins_server_system", On_clearTwins_server_system);
        socket.On("zoneInteractionMessage_server_system", On_zoneInteractionMessage_server_system);

        socket.Connect();
        /*
        SocketOptions options = new SocketOptions();
        options.AutoConnect = false;

        Manager = new SocketManager(new System.Uri("http://127.0.0.1:3020/socket.io/"), options);
        //Manager = new SocketManager(new System.Uri("http://10.10.10.10:3020/socket.io/"), options);
        Manager.Socket.On(SocketIOEventTypes.Connect, OnConnected);
        Manager.Socket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
        Manager.Socket.On("message", OnMessage);
        Manager.Socket.On("resolution",OnResolution);
        Manager.Socket.On("resolutionPhone", OnPhoneResolution);
        Manager.Socket.On("pose", OnPose);
        Manager.Socket.On("poseVuforia", OnPoseVuforia);
        Manager.Socket.On("poseVuforiaDesktop", OnPoseVuforiaDesktop);
        Manager.Socket.On("cameraPosition", OnCameraPosition);

        Manager.Socket.On("vuforiaResult_server_system", On_vuforiaResult_server_system);
        Manager.Socket.On("realityEditorObject_server_system", On_realityEditorObject_server_system);

        Manager.Socket.On("startRecording_server_system", On_startRecording_server_system);
        Manager.Socket.On("stopRecording_server_system", On_stopRecording_server_system);
        Manager.Socket.On("twin_server_system", On_twin_server_system);
        Manager.Socket.On("clearTwins_server_system", On_clearTwins_server_system);
        Manager.Socket.On("zoneInteractionMessage_server_system", On_zoneInteractionMessage_server_system);

        Manager.Socket.On(SocketIOEventTypes.Error, (socket, packet, args) => Debug.LogError(string.Format("Error: {0}", args[0].ToString())));

        Manager.Open();
        */
        //connected = true;
        tex = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);
    }

    float lastTime = 0.0f;
    float lastGarbageTime = 0.0f;
    public float fps = 1.0f;
    // Update is called once per frame
    void Update () {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleDemoMode();
        }
		if(Time.time - lastTime > 1.0f/fps)
        {
            lastTime = Time.time;
            if (connected && pushTask.IsCompleted)
            {
               // Debug.Log(cameraInfo.Count);
                foreach (KeyValuePair<string, CameraInformation> entry in cameraInfo)
                {
//                    pushTask = Task.Run(() =>
//                    {
                        // set camera position and rotation to the cameraInfo

                        // Vector3 cameraPosition = entry.Value.position;
                        // Quaternion cameraRotation = entry.Value.rotation;

                        cam.transform.localPosition = entry.Value.position;
                        cam.transform.localRotation = entry.Value.rotation;

                        // Debug.Log(entry.Value.position);

                        // send the screenshot to the corresponding editorId (the key)
                        string editorId = entry.Key;
                        // string thisSocketId = editorToSocketId[editorId];
                        // Socket thisSocket = connectedSockets[thisSocketId];

                        // Debug.Log("editor " + editorId + " is using socket " + thisSocketId);

                        string encodedBytes = getScreenshot();
                        string encodedDepthBytes = sendColorOnly ? "" : getDepthScreenshot();

                        //send message!
                        if (sendColorOnly)
                        {
                            socket.Emit("image", encodedBytes + ";_;" + ";_;" + editorId + ";_;" + cameraInfo.Count); // count gives the rescale factor client needs to apply
                            // Manager.Socket.Emit("image", encodedBytes + ";_;" + editorId);
                        }
                        else
                        {
                            socket.Emit("image", encodedBytes + ";_;" + encodedDepthBytes + ";_;" + editorId + ";_;" + cameraInfo.Count);
                            // Manager.Socket.Emit("image", encodedBytes + ";_;" + editorId + ";_;" + encodedDepthBytes);
                        }
                    //                    });
                }

                cameraInfo.Clear();

                /*
                string encodedBytes = getScreenshot();
                string encodedDepthBytes = sendColorOnly ? "" : getDepthScreenshot();

                pushTask = Task.Run(() =>
                {
                    //send message!
                    if (sendColorOnly)
                    {
                        socket.Emit("image", new JSONObject(encodedBytes));
                    } else
                    {
                        socket.Emit("image", new JSONObject(encodedBytes + ";_;" + encodedDepthBytes));
                    }
                });
                */
            }
        }


        if(Time.time - lastGarbageTime > 60.0f)
        {
            lastGarbageTime = Time.time;
            Resources.UnloadUnusedAssets();
        }
	}

    int getAdjustedResWidth()
    {
        if (cameraInfo.Count == 0)
        {
            return resWidth;
        }
        return (int)Mathf.Round(resWidth / cameraInfo.Count);
    }

    int getAdjustedResHeight()
    {
        if (cameraInfo.Count == 0)
        {
            return resHeight;
        }
        return (int)Mathf.Round(resHeight / cameraInfo.Count);
    }

    public int resWidth = 480;
    public int resHeight = 320;
    int phoneResWidth = 714;
    int phoneResHeight = 413;
    public GameObject cam;
    Texture2D tex;
    public GameObject debugCube;

    float factor = 1f;
    public float resolutionFactor = 2.0f;
    public float scaleFactor = 1.0f;

    public GameObject emergencyDebugCube;
    public GameObject emergencyDebugCube2;

    Texture2D emergencyTex;

    public bool rescale = true;
    string getScreenshot()
    {
        int scaledResWidth = getAdjustedResWidth();
        int scaledResHeight = getAdjustedResHeight();
        //RenderTexture rt = new RenderTexture(resWidth, resHeight, 24, RenderTextureFormat.Depth);
        //Debug.Log("capturing screen at: " + resWidth + " " + resHeight);
        cam.GetComponent<Camera>().targetTexture = rt;
        cam.GetComponent<Camera>().Render();
        cam.GetComponent<Camera>().targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        tex.Apply();

        //emergencyTex = tex;
        //emergencyDebugCube2.GetComponent<Renderer>().material.mainTexture = tex;

        //debugCube.GetComponent<Renderer>().material.mainTexture = tex;

        RenderTexture.active = null; // JC: added to avoid errors
        cam.GetComponent<Camera>().targetTexture = null;

        byte[] bytes;
        string output;
        if (useJPG)
        {
            bytes = tex.EncodeToJPG(80);
            output = "data:image/jpg;base64," + System.Convert.ToBase64String(bytes);
        }
        else
        {
            bytes = tex.EncodeToPNG();
            output = output = "data:image/png;base64," + System.Convert.ToBase64String(bytes);
        }
        //byte[] bytes = tex.EncodeToJPG();
        //byte[] bytes = tex.EncodeToPNG();
        //Debug.Log("sending: " + bytes.Length + " bytes");
        //string output = "data:image/jpg;base64," + System.Convert.ToBase64String(bytes);
        //string output = "data:image/png;base64," + System.Convert.ToBase64String(bytes);
        return output;
    }

    public bool useJPG = true;

    string getDepthScreenshot()
    {
        cam.GetComponent<Camera>().targetTexture = rt;
        cam.GetComponent<Camera>().Render();
        cam.GetComponent<Camera>().targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        tex.Apply();
        Graphics.Blit(tex, rt, depthMat);
        tex.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        tex.Apply();

        RenderTexture.active = null; // JC: added to avoid errors
        cam.GetComponent<Camera>().targetTexture = null;

        byte[] bytes;
        string output;
        if (useJPG)
        {
            bytes = tex.EncodeToJPG();
            output = "data:image/jpg;base64," + System.Convert.ToBase64String(bytes);
        }
        else
        {
            bytes = tex.EncodeToPNG();
            output = output = "data:image/png;base64," + System.Convert.ToBase64String(bytes);
        }
        return output;
    }

    void OnDestroy()
    {
        Debug.Log("OnDestroy");
        // Leaving this sample, close the socket
        socket.Close();
    }



    public void sendToVuforia(string data)
    {
        if (connected)
        {
            socket.Emit("vuforiaImage_system_server", data);
        }
    }

    void On_vuforiaResult_server_system(SocketIOEvent e)
    {
        var args = e.Data;
        string jsonPacket = args[0].ToString();
        //GameObject.Find("VuforiaModule").GetComponent<VuforiaModule>().vuforiaResponse(jsonPacket);

    }

    void On_realityEditorObject_server_system(SocketIOEvent e)
    {
        var args = e.Data;
        string jsonPacket = args[0].ToString();
        //GameObject.Find("VuforiaModule").GetComponent<VuforiaModule>().realityEditorObject(jsonPacket);
    }

    public void Emit_VuforiaModuleUpdate_system_server(string data)
    {
        if (connected)
        {
            socket.Emit("vuforiaModuleUpdate_system_server", data);
        }
    }

    public void On_startRecording_server_system(SocketIOEvent e)
    {
        Debug.Log("received start recording message from server");
        gifRecorder.GetComponent<GifRecorder>().startRecording();
    }

    public void On_stopRecording_server_system(SocketIOEvent e)
    {
        Debug.Log("received stop recording message from server");
        gifRecorder.GetComponent<GifRecorder>().stopRecording();
    }

    public void On_twin_server_system(SocketIOEvent e)
    {
        Debug.Log("received twin message from server");
        //cloner.GetComponent<Cloner>().clone();
    }

    public void On_clearTwins_server_system(SocketIOEvent e)
    {
        Debug.Log("received clear message from server");
        //cloner.GetComponent<Cloner>().clear();
    }


    GameObject findObject(string name)
    {
        GameObject foundObject = null;
        Object[] objects = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.GameObject));
        for(int i = 0; i<objects.Length; i++)
        {
            if(objects[i].name == name)
            {
                foundObject = (GameObject)objects[i];
            }
        }
        return foundObject;
    }


    public void On_zoneInteractionMessage_server_system(SocketIOEvent e)
    {
        var args = e.Data;
        Debug.Log("received interaction message from server");
        Debug.Log("args: " + args);
        Debug.Log("packet: " + e);
        //unpack it:
        string jsonPacket = args[0].ToString();
        JSONNode jn = JSON.Parse(jsonPacket);
        //get module states:
        JSONArray ja_module_state = jn["moduleList"].AsArray;
        for(int i = 0; i < ja_module_state.Count; i++)
        {




            string moduleName = ja_module_state[i]["name"];
            bool moduleState = ja_module_state[i]["active"];
            GameObject module = findObject(moduleName);
            Debug.Log("RealityZoneInteraction Setting module: " + moduleName + " to: " + moduleState + " found module: " + module);
            Debug.Log("length of module name: " + moduleName.Length);
            if(module != null)
            {
                module.SetActive(moduleState);
            }
            //GameObject.Find(moduleName).SetActive(moduleState);

        }

        JSONArray ja_action = jn["actionList"].AsArray;
        for (int i = 0; i < ja_action.Count; i++)
        {
            string objectName = ja_action[i]["object"];
            string command = ja_action[i]["command"];
            GameObject module = findObject(objectName);
            Debug.Log("RealityZoneInteraction sending message to: " + objectName + " with command: " + command + " found object: " + module);
            if (module != null)
            {
                GameObject.Find(objectName).SendMessage(command);
            }


        }


        //cloner.GetComponent<Cloner>().clear();
    }

    public void newGif(string location)
    {
        if (connected)
        {
            var data = new JSONObject();
            data.Add("gifurl", location);
            socket.Emit("realityZoneGif_system_server", data);
        }
    }



    void OnMessage(SocketIOEvent e)
    {
        /*
        Debug.Log("received: " + packet + " with " + args[0].ToString());

        JSONNode jn = JSON.Parse(args[0].ToString());
        if (!jn || !jn.IsObject)
        {
            Debug.Log("but it's not an obj" + jn.ToString());
            return;
        }
        if (jn["command"].IsString)
        {
            string command = jn["command"].Value;
            Debug.Log("command " + command);
            switch (command)
            {
                case "toggleVisualizations":
                    player.ToggleVisualizations();
                    break;
                case "toggleTracking":
                    player.ToggleTracking();
                    break;
                case "toggleLines":
                    player.ToggleLines();
                    break;
                case "resetLines":
                    player.ResetLines();
                    break;
            }
        } else
        {
            Debug.Log("NTO ASDF ASTR");
        }
        */
        var args = e.Data;
        string command = args[0].ToString();
        Debug.Log("command " + command);
        char[] quotes = { '"' };
        switch (command.Trim(quotes))
        {
            case "toggleVisualizations":
                player.ToggleVisualizations();
                break;
            case "toggleSkeletons":
                player.ToggleSkeletons();
                break;
            case "toggleTracking":
                player.ToggleTracking();
                break;
            case "toggleLines":
                player.ToggleLines();
                break;
            case "toggleDemoMode":
                ToggleDemoMode();
                break;
            case "toggleXRayView":
                shadingController.ToggleShading();
                break;
            case "resetLines":
                player.ResetLines();
                break;
        }
    }

    private void ToggleDemoMode()
    {
        inDemoMode = !inDemoMode;
        player.SetShowSkeletons(true);
        player.SetShowLines(inDemoMode);
        player.SetShowVisualizations(!inDemoMode);
        shadingController.SwitchShading(!inDemoMode);
    }

    private void OnConnected()
    {
        if (connected)
        {
            // return;
        }
        //identify yourself as the station.
        Debug.Log("connected to server");
        socket.Emit("name", "station");
        connected = true;
    }

    private void OnDisconnected()
    {
        if (!connected)
        {
           return;
        }
        connected = false;
        Debug.Log("server disconnected");
        socket.Connect();
        //identify yourself as the station.
        //Manager.Socket.Emit("name", "station");
        // remove socket from uuid -> socket map
        // connectedSockets[socket.Id] = editorId;
        /* 
        connectedSockets.Remove(socket.Id);
        string editorId = socketToEditorId[socket.Id];
        socketToEditorId.Remove(socket.Id);
        editorToSocketId.Remove(editorId);
        */
        // cameraInfo.Remove(editorId); // TODO: print cameraInfo.length to figure out why it's not speeding up...
    }

    void OnResolution(SocketIOEvent e)
    {
        var args = e.Data;
        Debug.Log("resolution received: " + e);
        Debug.Log("args: " + args[0]);
        string[] parts = args[0].ToString().Split(',');
        resWidth = (int)(float.Parse(parts[0])/factor);
        resHeight = (int)(float.Parse(parts[1])/factor);
        //resWidth = 1920;
        //resHeight = 1080;
        //resHeight = resHeight * 2;
        Destroy(tex);
        tex = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);
        cam.GetComponent<Camera>().aspect = (float)resWidth / (float)resHeight;
        Destroy(rt);
        rt = new RenderTexture(resWidth, resHeight, 32);
    }

    public void SendSkeleton(string skeletonObject)
    {
        if (connected)
        {
            socket.Emit("realityZoneSkeleton", skeletonObject);
        }
    }

    void OnPhoneResolution(SocketIOEvent e)
    {
        var args = e.Data;
        Debug.Log("phone resolution received: " + e);
        string[] parts = args[0].ToString().Split(',');
        //phoneResWidth = (int)(float.Parse(parts[0]) / factor);
        //phoneResHeight = (int)(float.Parse(parts[1]) / factor);
    }

    void OnPose(SocketIOEvent e)
    {
        var args = e.Data;
        Debug.Log("onpose received: " + e);
        Debug.Log("args: " + args[0]);


        Vector3 position = new Vector3(float.Parse(args[0][0].ToString()),
                                        float.Parse(args[0][1].ToString()),
                                        float.Parse(args[0][2].ToString()));

        Quaternion orientation = new Quaternion(float.Parse(args[0][3].ToString()),
                                        float.Parse(args[0][4].ToString()),
                                        float.Parse(args[0][5].ToString()),
                                        float.Parse(args[0][6].ToString()));

        cam.transform.position = position;
        cam.transform.rotation = orientation;

        /*
        for (int i = 0; i < ((List<object>)args[0]).Count; i++)
        {
            float val = float.Parse(((List<object>)args[0])[i].ToString());
            Debug.Log("i: " + i + " v: " + val);
        }
        */

        /*
        var array = JSON.Parse(args[0].ToString());
        for(int i = 0; i < array.Count; i++)
        {
            Debug.Log("i: " + i + " v: " + (float)array[i][0].AsFloat);
        }
        */
    }

    public bool udpVersion = false;
    public Vector3 debugVector;
    public void pushUDP(string text)
    {
        if (!udpVersion)
        {
            return;
        }
        //parse JSON:
        //Debug.Log("pusher received: " + text);

        JSONNode jn = JSON.Parse(text);
        //Debug.Log("after prasing json: " + jn["matrix"]["model"]["newTurtletBjbb04jalux"][15].ToString());
        //Debug.Log(jn["matrix"]);

        //Debug.Log(jn["matrix"]["model"]["newTurtletBjbb04jalux"]);
        if (jn["matrix"]["model"]["newTurtletBjbb04jalux"] == null) {
            return;
        }

        Matrix4x4 worldToCamera = new Matrix4x4();
        Vector4 col0 = new Vector4(float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][0].ToString()),
                                   float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][1].ToString()),
                                   float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][2].ToString()),
                                   float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][3].ToString()));
        Vector4 col1 = new Vector4(float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][4].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][5].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][6].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][7].ToString()));
        Vector4 col2 = new Vector4(float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][8].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][9].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][10].ToString()),
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][11].ToString()));
        Vector4 col3 = new Vector4(float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][12].ToString())*1000.0f,
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][13].ToString()) * 1000.0f,
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][14].ToString()) * 1000.0f,
                           float.Parse(jn["matrix"]["model"]["newTurtletBjbb04jalux"][15].ToString()));
        worldToCamera.SetColumn(0, col0);
        worldToCamera.SetColumn(1, col1);
        worldToCamera.SetColumn(2, col2);
        worldToCamera.SetColumn(3, col3);


        Matrix4x4 cameraToWorld = worldToCamera.inverse;

        //this new matrix has:
        //camera position in cameraToWorld[0:2,3]
        //3X3 rotation matrix in cameraToWorld[0:2,0:2]


        Vector4 forwardPos4 = cameraToWorld * (new Vector4(0,0,1,1));
        Vector4 upPos4 = cameraToWorld * (new Vector4(0, 1, 0, 1));
        Vector4 camPos4 = cameraToWorld * (new Vector4(0, 0, 0, 1));
        Vector3 forwardPos = new Vector3(forwardPos4.x / forwardPos4.w, forwardPos4.y / forwardPos4.w, forwardPos4.z / forwardPos4.w);
        Vector3 upPos = new Vector3(upPos4.x / upPos4.w, upPos4.y / upPos4.w, upPos4.z / upPos4.w);
        Vector3 camPos = new Vector3(camPos4.x / camPos4.w, camPos4.y / camPos4.w, camPos4.z / camPos4.w);
        Vector3 forwardVec = forwardPos - camPos;
        Vector3 upVec = upPos - camPos;

        debugVector = new Vector3();
        debugVector = upVec;
        //Debug.Log("upvec: " + upVec);

        Vector4 lastColumn = cameraToWorld.GetColumn(3);
        Vector3 camPosition = new Vector3(lastColumn.x, lastColumn.y, -lastColumn.z);

        Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);


        cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f), camPosition.y * (scaleFactor / 1000.0f), scaleFactor * camPosition.z / 1000.0f);
        //cam.transform.position = camPosition/(scaleFactor*1000.0f);
        cam.transform.rotation = camRotation;


        /*




        Vector4 rotColumn0 = cameraToWorld.GetColumn(0);
        //Vector3 camForwardVector = new Vector3(rotColumn2.x, rotColumn2.y, -rotColumn2.z) + camPosition;
        Vector3 camRightVector = new Vector3(rotColumn0.x, rotColumn0.y, rotColumn0.z);
        //Debug.Log("cam right vector: " + camRightVector);

        Vector4 rotColumn1 = cameraToWorld.GetColumn(1);
        //Vector3 camUpVector = new Vector3(rotColumn1.x, rotColumn1.y, -rotColumn1.z) + camPosition;
        //Vector3 camUpVector = new Vector3(rotColumn1.x, rotColumn1.y, -rotColumn1.z);
        Vector3 camUpVector = new Vector3(-rotColumn1.x, -rotColumn1.y, -rotColumn1.z);
        //Debug.Log("cam up vector: " + camUpVector);

        Vector4 rotColumn2 = -cameraToWorld.GetColumn(2);
        //Vector3 camForwardVector = new Vector3(rotColumn2.x, rotColumn2.y, -rotColumn2.z) + camPosition;
        //Vector3 camForwardVector = new Vector3(-rotColumn2.x, -rotColumn2.y, rotColumn2.z);
        Vector3 camForwardVector = new Vector3(rotColumn2.x, rotColumn2.y, rotColumn2.z);
        //Debug.Log("cam forward vector: " + camForwardVector);

        Quaternion camRotation = Quaternion.LookRotation(camForwardVector, camUpVector);

        cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f), camPosition.y * (scaleFactor / 1000.0f), scaleFactor * camPosition.z / 1000.0f);
        //cam.transform.position = camPosition/(scaleFactor*1000.0f);
        cam.transform.rotation = camRotation;
        */

    }

    public float calculated_fovy;
    public float calculated_fovy_prime;
    public float calculated_aspect;
    public float slider = 1.0f;

    public Matrix4x4 lastProjectionMatrix;

    public GameObject upPoint;
    public GameObject forwardPoint;

    public struct CameraInformation
    {
        public CameraInformation(Vector3 _position, Quaternion _rotation)
        {
            position = _position;
            rotation = _rotation;
        }

        public Vector3 position;
        public Quaternion rotation;
    }

    // maps editorIds to (position, rotation) structs
    Dictionary<string, CameraInformation> cameraInfo = new Dictionary<string, CameraInformation>();

    // Dictionary<string, Socket> connectedSockets = new Dictionary<string, Socket>();
    Dictionary<string, string> socketToEditorId = new Dictionary<string, string>();
    Dictionary<string, string> editorToSocketId = new Dictionary<string, string>();

    //this is the one being used.
    void OnCameraPosition(SocketIOEvent e)
    {
        var args = e.Data;
        string jsonPacket = args[0].ToString();
        // Debug.Log("received: " + jsonPacket);
        JSONNode jn = JSON.Parse(jsonPacket);

        string editorId = jn["editorId"];
        if (!editorToSocketId.ContainsKey(editorId))
        {
            Debug.Log("Pose from new editor (" + editorId + ")");

            editorToSocketId[editorId] = "exists...";
            /*
            socketToEditorId[socket.Id] = editorId;
            editorToSocketId[editorId] = socket.Id;
            connectedSockets[socket.Id] = socket;
            */
        }

        JSONArray mvarray = jn["cameraPoseMatrix"].AsArray;
        JSONArray parray = jn["projectionMatrix"].AsArray;

        // camera matrix (inverse view matrix), converted from toolbox format to Matrix4x4
        Matrix4x4 mvMatrix = new Matrix4x4();
        mvMatrix.SetColumn(0, new Vector4(mvarray[0].AsFloat, mvarray[1].AsFloat, mvarray[2].AsFloat, mvarray[3].AsFloat));
        mvMatrix.SetColumn(1, new Vector4(mvarray[4].AsFloat, mvarray[5].AsFloat, mvarray[6].AsFloat, mvarray[7].AsFloat));
        mvMatrix.SetColumn(2, new Vector4(mvarray[8].AsFloat, mvarray[9].AsFloat, mvarray[10].AsFloat, mvarray[11].AsFloat));
        mvMatrix.SetColumn(3, new Vector4(mvarray[12].AsFloat, mvarray[13].AsFloat, mvarray[14].AsFloat, mvarray[15].AsFloat));

        // projection matrix, converted from toolbox format to Matrix4x4.. we use the same projection matrix to match camera FoV and aspect ratio
        Matrix4x4 pMatrix = new Matrix4x4();
        pMatrix.SetColumn(0, new Vector4(parray[0].AsFloat, parray[1].AsFloat, parray[2].AsFloat, parray[3].AsFloat));
        pMatrix.SetColumn(1, new Vector4(parray[4].AsFloat, -parray[5].AsFloat, parray[6].AsFloat, parray[7].AsFloat));
        pMatrix.SetColumn(2, new Vector4(parray[8].AsFloat, parray[9].AsFloat, parray[10].AsFloat, parray[11].AsFloat));
        pMatrix.SetColumn(3, new Vector4(parray[12].AsFloat, parray[13].AsFloat, parray[14].AsFloat, parray[15].AsFloat));
        lastProjectionMatrix = pMatrix;

        float fov_y = 2.0f * Mathf.Atan(1.0f / pMatrix.m11) * 180.0f / Mathf.PI;
        float fov_y_prime = 2.0f * Mathf.Atan(resHeight * fov_y / 2.0f * Mathf.Deg2Rad / phoneResHeight) * Mathf.Rad2Deg;
        float aspect = (float)resWidth / (float)resHeight;

        cam.GetComponent<Camera>().fieldOfView = fov_y;
        //cam.GetComponent<Camera>().fieldOfView = fov_y_prime;
        cam.GetComponent<Camera>().aspect = aspect;
        calculated_fovy = fov_y;
        calculated_aspect = aspect;
        calculated_fovy_prime = fov_y_prime;

        //Debug.Log("reality zone origin!");
        // Matrix4x4 cameraToWorld = mvMatrix.inverse;
        Matrix4x4 cameraToWorld = mvMatrix; // already inverse of view matrix so don't need to invert again
        Vector4 lastColumn = cameraToWorld.GetColumn(3);

        // calculate forward and up vectors from toolbox's camera matrix so that we can set the unity camera to match it
        Vector3 camPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 0));
        Vector3 forwardPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 1));
        Vector3 upPos = cameraToWorld.MultiplyPoint(new Vector3(0, 1, 0));

        Vector3 forwardVec = forwardPos - camPos;
        Vector3 upVec = upPos - camPos;

        // these need to be inverted for some reason.... maybe it's converting from right-handed (vuforia) to left-handed (unity)?
        upVec.x = -upVec.x;
        upVec.y = -upVec.y;
        upVec.z = -upVec.z;

        forwardVec.x = -forwardVec.x;
        forwardVec.y = -forwardVec.y;
        forwardVec.z = -forwardVec.z;

        forwardPoint.transform.localPosition = (forwardVec).normalized * 0.2f + camPos / 1000.0f;
        forwardPoint.transform.forward = cam.transform.position - forwardPoint.transform.position;
        upPoint.transform.localPosition = (upVec).normalized * 0.2f + camPos / 1000.0f;
        upPoint.transform.forward = cam.transform.position - upPoint.transform.position;

        Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);

        // divide by 1000 because toolbox uses mm not meters. store in cameraInfo list and render later so that multiple clients don't conflict
        cameraInfo[editorId] = new CameraInformation(new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f), camRotation);

    }

    void OnPoseVuforiaDesktop(SocketIOEvent e)
    {
        var args = e.Data;
        string jsonPacket = args[0].ToString();
        Debug.Log("received: " + jsonPacket);
        JSONNode jn = JSON.Parse(jsonPacket);

        string editorId = jn["editorId"];
        if (!editorToSocketId.ContainsKey(editorId))
        {
            Debug.Log("Pose from new editor (" + editorId + ")");
            /*
            socketToEditorId[socket.Id] = editorId;
            editorToSocketId[editorId] = socket.Id;
            connectedSockets[socket.Id] = socket;
            */
            editorToSocketId[editorId] = "exists...";
        }

        JSONArray mvarray = jn["modelViewMatrix"].AsArray; // this is identical to cameraPoseMatrix in latest version
        //JSONArray parray = jn["projectionMatrix"].AsArray;
        JSONArray parray = jn["realProjectionMatrix"].AsArray;

        Matrix4x4 mvMatrix = new Matrix4x4();
        mvMatrix.SetColumn(0, new Vector4(mvarray[0].AsFloat, mvarray[1].AsFloat, mvarray[2].AsFloat, mvarray[3].AsFloat));
        mvMatrix.SetColumn(1, new Vector4(mvarray[4].AsFloat, mvarray[5].AsFloat, mvarray[6].AsFloat, mvarray[7].AsFloat));
        mvMatrix.SetColumn(2, new Vector4(mvarray[8].AsFloat, mvarray[9].AsFloat, mvarray[10].AsFloat, mvarray[11].AsFloat));
        mvMatrix.SetColumn(3, new Vector4(mvarray[12].AsFloat, mvarray[13].AsFloat, mvarray[14].AsFloat, mvarray[15].AsFloat));

        //Debug.Log("mvMatrix: " + mvMatrix);

        Matrix4x4 pMatrix = new Matrix4x4();
        pMatrix.SetColumn(0, new Vector4(parray[0].AsFloat, parray[1].AsFloat, parray[2].AsFloat, parray[3].AsFloat));
        pMatrix.SetColumn(1, new Vector4(parray[4].AsFloat, -parray[5].AsFloat, parray[6].AsFloat, parray[7].AsFloat));
        //pMatrix.SetColumn(1, new Vector4(parray[4].AsFloat, parray[5].AsFloat, parray[6].AsFloat, parray[7].AsFloat));
        pMatrix.SetColumn(2, new Vector4(parray[8].AsFloat, parray[9].AsFloat, parray[10].AsFloat, parray[11].AsFloat));
        pMatrix.SetColumn(3, new Vector4(parray[12].AsFloat, parray[13].AsFloat, parray[14].AsFloat, parray[15].AsFloat));
        lastProjectionMatrix = pMatrix;

        //Matrix4x4 pMatrix = lastProjectionMatrix;

        float fov_y = 2.0f * Mathf.Atan(1.0f / pMatrix.m11) * 180.0f / Mathf.PI;
        float fov_y_prime = 2.0f * Mathf.Atan(resHeight * fov_y / 2.0f * Mathf.Deg2Rad / phoneResHeight) * Mathf.Rad2Deg;


        //float aspect = Mathf.Abs(pMatrix.m00/pMatrix.m11);
        float aspect = (float)resWidth / (float)resHeight;


        cam.GetComponent<Camera>().fieldOfView = fov_y;
        //cam.GetComponent<Camera>().fieldOfView = fov_y_prime;
        cam.GetComponent<Camera>().aspect = aspect;
        calculated_fovy = fov_y;
        calculated_aspect = aspect;
        calculated_fovy_prime = fov_y_prime;

        //Debug.Log("camera mode: " + jn["cameraMode"]);
        if(jn["cameraMode"] == "REALITY_ZONE_ORIGIN") //if you do .toString(), it adds quotes, watch out.
        {

            //GameObject.Find("VuforiaModule").GetComponent<VuforiaModule>().continouslyUpdateVuforia = true;

            //Debug.Log("reality zone origin!");
            Matrix4x4 cameraToWorld = mvMatrix.inverse;
            Vector4 lastColumn = cameraToWorld.GetColumn(3);

            Vector3 camPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 0));
            camPos.x = -camPos.x;
            camPos.y = -camPos.y;
            camPos.z = -camPos.z;

            Vector3 forwardPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 1));
            forwardPos.x = -forwardPos.x;
            forwardPos.y = -forwardPos.y;
            forwardPos.z = -forwardPos.z;

            Vector3 upPos = cameraToWorld.MultiplyPoint(new Vector3(0, 1, 0));
            upPos.x = -upPos.x;
            upPos.y = -upPos.y;
            upPos.z = -upPos.z;



            Vector3 forwardVec = forwardPos - camPos;
            Vector3 upVec = upPos - camPos;

            upVec.x = -upVec.x;
            upVec.y = -upVec.y;
            upVec.z = -upVec.z;

            forwardVec.x = -forwardVec.x;
            forwardVec.y = -forwardVec.y;
            forwardVec.z = -forwardVec.z;

            forwardPoint.transform.localPosition = (forwardVec).normalized * 0.2f + camPos / 1000.0f;
            forwardPoint.transform.forward = cam.transform.position - forwardPoint.transform.position;
            upPoint.transform.localPosition = (upVec).normalized * 0.2f + camPos / 1000.0f;
            upPoint.transform.forward = cam.transform.position - upPoint.transform.position;

            Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);

            //cam.transform.localPosition = new Vector3(camPos.x * (scaleFactor / 1000.0f * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
            //cam.transform.localPosition = new Vector3(camPos.x  / 1000.0f, camPos.y / 1000.0f, scaleFactor * camPos.z / 1000.0f * 2.0f);

            /*
            cam.transform.localPosition = new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f);
            cam.transform.localRotation = camRotation;
            */

            cameraInfo[editorId] = new CameraInformation(new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f), Quaternion.LookRotation(forwardVec, upVec));

            /*
            CameraInformation thisInfo = cameraInfo[editorId];
            thisInfo.position = new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f);
            thisInfo.rotation = Quaternion.LookRotation(forwardVec, upVec);
            */
        }
        else
        {
            //GameObject.Find("VuforiaModule").GetComponent<VuforiaModule>().continouslyUpdateVuforia = false;
            gameObject.transform.position = pusherOrigin.transform.position;
            gameObject.transform.rotation = pusherOrigin.transform.rotation;


            Matrix4x4 cameraToWorld = mvMatrix.inverse;
            Vector4 lastColumn = cameraToWorld.GetColumn(3);

            Vector3 camPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 0));
            camPos.x = camPos.x;
            camPos.y = -camPos.y;
            camPos.z = -camPos.z;

            Vector3 forwardPos = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 1));
            //forwardPos.x = -forwardPos.x;
            forwardPos.y = -forwardPos.y;
            forwardPos.z = -forwardPos.z;




            Vector3 upPos = cameraToWorld.MultiplyPoint(new Vector3(0, 1, 0));
            //upPos.x = -upPos.x;
            upPos.y = -upPos.y;
            upPos.z = -upPos.z;

            Vector3 forwardVec = forwardPos - camPos;
            Vector3 upVec = upPos - camPos;

            upVec.x = -upVec.x;
            upVec.y = -upVec.y;
            upVec.z = -upVec.z;

            forwardVec.x = -forwardVec.x;
            forwardVec.y = -forwardVec.y;
            forwardVec.z = -forwardVec.z;

            forwardPoint.transform.localPosition = (forwardVec).normalized * 0.2f + camPos / 1000.0f;
            forwardPoint.transform.forward = cam.transform.position - forwardPoint.transform.position;
            upPoint.transform.localPosition = (upVec).normalized * 0.2f + camPos / 1000.0f;
            upPoint.transform.forward = cam.transform.position - upPoint.transform.position;

            Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);

            //cam.transform.localPosition = new Vector3(camPos.x * (scaleFactor / 1000.0f * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
            //cam.transform.localPosition = new Vector3(camPos.x  / 1000.0f, camPos.y / 1000.0f, scaleFactor * camPos.z / 1000.0f * 2.0f);

            /*
            cam.transform.localPosition = new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f);
            cam.transform.localRotation = camRotation;
            */

            cameraInfo[editorId] = new CameraInformation(new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f), Quaternion.LookRotation(forwardVec, upVec));

            /*
            if (cameraInfo.ContainsKey(editorId))
            {
                cameraInfo[editorId] = new CameraInformation(new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f), Quaternion.LookRotation(forwardVec, upVec));
            }
            else
            {
                cameraInfo.Add(editorId, new CameraInformation(new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f), Quaternion.LookRotation(forwardVec, upVec)));
            }
            */

            /*
            CameraInformation thisInfo = cameraInfo[editorId];
            thisInfo.position = new Vector3(camPos.x / 1000.0f, camPos.y / 1000.0f, camPos.z / 1000.0f);
            thisInfo.rotation = Quaternion.LookRotation(forwardVec, upVec);
            */
        }





        /*
        Matrix4x4 cameraToWorld = mvMatrix.inverse;
        //Debug.Log("mv matrix: " + mvMatrix);
        //Debug.Log("camera to world: " + cameraToWorld);
        Vector4 lastColumn = cameraToWorld.GetColumn(3);
        Vector3 camPosition = new Vector3(lastColumn.x, -lastColumn.y, -lastColumn.z);
        //Vector3 camPosition = new Vector3(lastColumn.x, lastColumn.y, lastColumn.z);


        Vector4 forwardPos4 = cameraToWorld * (new Vector4(0, 0, 1, 1));
        Vector4 upPos4 = cameraToWorld * (new Vector4(0, 1, 0, 1));
        Vector4 camPos4 = cameraToWorld * (new Vector4(0, 0, 0, 1));
        Vector3 forwardPos = new Vector3(forwardPos4.x / forwardPos4.w, forwardPos4.y / forwardPos4.w, forwardPos4.z / forwardPos4.w);
        Vector3 upPos = new Vector3(upPos4.x / upPos4.w, upPos4.y / upPos4.w, upPos4.z / upPos4.w);
        Vector3 camPos = new Vector3(camPos4.x / camPos4.w, camPos4.y / camPos4.w, camPos4.z / camPos4.w);
        Vector3 forwardVec = forwardPos - camPos;
        Vector3 upVec = upPos - camPos;


        camPos.x = -camPos.x;
        forwardVec.x = -forwardVec.x;
        upVec.x = -upVec.x;



        //forwardVec.x = -forwardVec.x;
        //upVec.x = -upVec.x;

        //forwardVec.y = -forwardVec.y;
        //upVec.y = -upVec.y;

        //debugVector = new Vector3();
        //upVec = -upVec;
        debugVector = upVec;

        //Debug.Log("upvec: " + upVec);

        Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);
        //cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f * 2.0f), camPosition.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPosition.z / 1000.0f *2.0f);


        ////cam.transform.position = new Vector3(camPos.x * (scaleFactor / 1000.0f  * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
        ////cam.transform.rotation = camRotation;


        cam.transform.localPosition = new Vector3(camPos.x * (scaleFactor / 1000.0f * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
        cam.transform.localRotation = camRotation;
        */

        return;
    }


    void OnPoseVuforia(SocketIOEvent e)
    {
        //Debug.Log("onpose vuforia: ");
        if (udpVersion)
        {
            return;
        }
        /*
        string output = "";
        for(int i =0; i<16; i++)
        {
            output += ((List<object>)args[0])[i].ToString() + ",";
        }
        Debug.Log(output);
        */

        //Debug.Log("pose received: " + packet.ToString());
        //Debug.Log("length of args: " + args.Length);
        var args = e.Data;
        //Debug.Log("args of 0: " + args[0]);
        //string jsonPacket = packet.ToString();
        string jsonPacket = args[0].ToString();
        //Debug.Log("json packet: " + jsonPacket);
        JSONNode jn = JSON.Parse(jsonPacket);

        JSONArray mvarray = jn["modelViewMatrix"].AsArray;
        JSONArray parray = jn["projectionMatrix"].AsArray;

        //Debug.Log("projection matrix raw: " + jn["projectionMatrix"].AsArray.ToString());

        //string modelViewMatrixString = jn["modelViewMatrix"].AsArray.ToString();
        //string projectionMatrixString = jn["projectionMatrix"].AsArray.ToString();
        //Debug.Log("modelview: " + modelViewMatrixString);
        //Debug.Log("projection: " + projectionMatrixString);

        Matrix4x4 mvMatrix = new Matrix4x4();
        mvMatrix.SetColumn(0, new Vector4(mvarray[0].AsFloat, mvarray[1].AsFloat, mvarray[2].AsFloat, mvarray[3].AsFloat));
        mvMatrix.SetColumn(1, new Vector4(mvarray[4].AsFloat, mvarray[5].AsFloat, mvarray[6].AsFloat, mvarray[7].AsFloat));
        mvMatrix.SetColumn(2, new Vector4(mvarray[8].AsFloat, mvarray[9].AsFloat, mvarray[10].AsFloat, mvarray[11].AsFloat));
        mvMatrix.SetColumn(3, new Vector4(mvarray[12].AsFloat, mvarray[13].AsFloat, mvarray[14].AsFloat, mvarray[15].AsFloat));

        Matrix4x4 pMatrix = new Matrix4x4();
        pMatrix.SetColumn(0, new Vector4(parray[0].AsFloat, parray[1].AsFloat, parray[2].AsFloat, parray[3].AsFloat));
        pMatrix.SetColumn(1, new Vector4(parray[4].AsFloat, parray[5].AsFloat, parray[6].AsFloat, parray[7].AsFloat));
        pMatrix.SetColumn(2, new Vector4(parray[8].AsFloat, parray[9].AsFloat, parray[10].AsFloat, parray[11].AsFloat));
        pMatrix.SetColumn(3, new Vector4(parray[12].AsFloat, parray[13].AsFloat, parray[14].AsFloat, parray[15].AsFloat));



        lastProjectionMatrix = pMatrix;

        //Debug.Log("mv matrix: " + mvMatrix.ToString());
        //Debug.Log("pv matrix: " + pMatrix.ToString());

        /*
        float fx = parray[0].AsFloat;
        float fy = parray[5].AsFloat;
        float cx = parray[8].AsFloat;
        float cy = parray[9].AsFloat;

        float fov = 2 * Mathf.Atan(resHeight / (2.0f * fy)) * Mathf.Rad2Deg;
        float aspect = resWidth / resHeight;
        */


        float fov_y = 2.0f * Mathf.Atan(1.0f / pMatrix.m11) * 180.0f / Mathf.PI;
        float fov_y_prime = 2.0f*Mathf.Atan(resHeight * fov_y/2.0f * Mathf.Deg2Rad / phoneResHeight) * Mathf.Rad2Deg;


        //float aspect = Mathf.Abs(pMatrix.m00/pMatrix.m11);
        float aspect = (float)resWidth / (float)resHeight;


        //cam.GetComponent<Camera>().fieldOfView = fov_y;
        cam.GetComponent<Camera>().fieldOfView = fov_y_prime;
        cam.GetComponent<Camera>().aspect = aspect;
        calculated_fovy = fov_y;
        calculated_aspect = aspect;
        calculated_fovy_prime = fov_y_prime;
        //Debug.Log("fovy: " + fov_y + " aspect: " + aspect);

        /*
        //Debug.Log("fx: " + fx);
        //Debug.Log("fy: " + fy);
        //Debug.Log("cx: " + cx);
        //Debug.Log("cy: " + cy);
        //Debug.Log("fov: " + fov);
        //Debug.Log("aspect: " + aspect);

        //pMatrix.m23 = -pMatrix.m23;
        pMatrix.m00 = pMatrix.m00;
        pMatrix.m22 = -pMatrix.m22;
        pMatrix.m32 = 1;
        //pMatrix.SetRow(3, new Vector4(0, 0, -1, 0));
        cam.GetComponent<Camera>().projectionMatrix = pMatrix;
        Debug.Log("p matrix: " + pMatrix);
        //cam.GetComponent<Camera>().fieldOfView = fov;
        //cam.GetComponent<Camera>().aspect = aspect;
        */




        //mvMatrix.m03 = mvMatrix.m03 * 1000.0f;
        //mvMatrix.m13 = mvMatrix.m13 * 1000.0f;
        Matrix4x4 cameraToWorld = mvMatrix.inverse;
        //Debug.Log("mv matrix: " + mvMatrix);
        //Debug.Log("camera to world: " + cameraToWorld);
        Vector4 lastColumn = cameraToWorld.GetColumn(3);
        Vector3 camPosition = new Vector3(lastColumn.x, -lastColumn.y, -lastColumn.z);


        Vector4 forwardPos4 = cameraToWorld * (new Vector4(0, 0, 1, 1));
        Vector4 upPos4 = cameraToWorld * (new Vector4(0, 1, 0, 1));
        Vector4 camPos4 = cameraToWorld * (new Vector4(0, 0, 0, 1));
        Vector3 forwardPos = new Vector3(forwardPos4.x / forwardPos4.w, forwardPos4.y / forwardPos4.w, forwardPos4.z / forwardPos4.w);
        Vector3 upPos = new Vector3(upPos4.x / upPos4.w, upPos4.y / upPos4.w, upPos4.z / upPos4.w);
        Vector3 camPos = new Vector3(camPos4.x / camPos4.w, camPos4.y / camPos4.w, camPos4.z / camPos4.w);
        Vector3 forwardVec = forwardPos - camPos;
        Vector3 upVec = upPos - camPos;
        //forwardVec.x = -forwardVec.x;
        //upVec.x = -upVec.x;

        //forwardVec.y = -forwardVec.y;
        //upVec.y = -upVec.y;

        //debugVector = new Vector3();
        debugVector = upVec;

        //Debug.Log("upvec: " + upVec);

        Quaternion camRotation = Quaternion.LookRotation(forwardVec, upVec);
        //cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f * 2.0f), camPosition.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPosition.z / 1000.0f *2.0f);

        /*
        cam.transform.position = new Vector3(camPos.x * (scaleFactor / 1000.0f  * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
        cam.transform.rotation = camRotation;
        */

        cam.transform.localPosition = new Vector3(camPos.x * (scaleFactor / 1000.0f * 2.0f), camPos.y * (scaleFactor / 1000.0f * 2.0f), scaleFactor * camPos.z / 1000.0f * 2.0f);
        cam.transform.localRotation = camRotation;


        /*
        Vector4 rotColumn0 = cameraToWorld.GetColumn(0);
        Vector3 camRightVector = new Vector3(rotColumn0.x, rotColumn0.y, rotColumn0.z);

        Vector4 rotColumn1 = cameraToWorld.GetColumn(1);
        Vector3 camUpVector = new Vector3(rotColumn1.x, rotColumn1.y, -rotColumn1.z);

        Vector4 rotColumn2 = -cameraToWorld.GetColumn(2);
        Vector3 camForwardVector = new Vector3(-rotColumn2.x, -rotColumn2.y, rotColumn2.z);

        Quaternion camRotation = Quaternion.LookRotation(camForwardVector, camUpVector);
        cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f), camPosition.y * (scaleFactor / 1000.0f), scaleFactor * camPosition.z / 1000.0f);
        cam.transform.rotation = camRotation;
        */
        return;


        //Debug.Log("args: " + ((List<object>)args[0]).Count);

        /*
        Matrix4x4 worldToCamera = new Matrix4x4();
        Vector4 col0 = new Vector4(float.Parse(((List<object>)args[0])[0].ToString()),
                                   float.Parse(((List<object>)args[0])[1].ToString()),
                                   float.Parse(((List<object>)args[0])[2].ToString()),
                                   float.Parse(((List<object>)args[0])[3].ToString()) );
        Vector4 col1 = new Vector4(float.Parse(((List<object>)args[0])[4].ToString()),
                           float.Parse(((List<object>)args[0])[5].ToString()),
                           float.Parse(((List<object>)args[0])[6].ToString()),
                           float.Parse(((List<object>)args[0])[7].ToString()));
        Vector4 col2 = new Vector4(float.Parse(((List<object>)args[0])[8].ToString()),
                           float.Parse(((List<object>)args[0])[9].ToString()),
                           float.Parse(((List<object>)args[0])[10].ToString()),
                           float.Parse(((List<object>)args[0])[11].ToString()));
        Vector4 col3 = new Vector4(float.Parse(((List<object>)args[0])[12].ToString()),
                           float.Parse(((List<object>)args[0])[13].ToString()),
                           float.Parse(((List<object>)args[0])[14].ToString()),
                           float.Parse(((List<object>)args[0])[15].ToString()));
        worldToCamera.SetColumn(0, col0);
        worldToCamera.SetColumn(1, col1);
        worldToCamera.SetColumn(2, col2);
        worldToCamera.SetColumn(3, col3);


        Matrix4x4 cameraToWorld = worldToCamera.inverse;
        */
        /*
        Matrix4x4 cameraToWorld = mvMatrix.inverse;

        //this new matrix has:
        //camera position in cameraToWorld[0:2,3]
        //3X3 rotation matrix in cameraToWorld[0:2,0:2]

        Vector4 lastColumn = cameraToWorld.GetColumn(3);
        Vector3 camPosition = new Vector3(lastColumn.x, lastColumn.y, -lastColumn.z);

        Vector4 rotColumn0 = cameraToWorld.GetColumn(0);
        //Vector3 camForwardVector = new Vector3(rotColumn2.x, rotColumn2.y, -rotColumn2.z) + camPosition;
        Vector3 camRightVector = new Vector3(rotColumn0.x, rotColumn0.y, rotColumn0.z);
        //Debug.Log("cam right vector: " + camRightVector);

        Vector4 rotColumn1 = cameraToWorld.GetColumn(1);
        //Vector3 camUpVector = new Vector3(rotColumn1.x, rotColumn1.y, -rotColumn1.z) + camPosition;
        Vector3 camUpVector = new Vector3(rotColumn1.x, rotColumn1.y, -rotColumn1.z);
        //Debug.Log("cam up vector: " + camUpVector);

        Vector4 rotColumn2 = -cameraToWorld.GetColumn(2);
        //Vector3 camForwardVector = new Vector3(rotColumn2.x, rotColumn2.y, -rotColumn2.z) + camPosition;
        Vector3 camForwardVector = new Vector3(-rotColumn2.x, -rotColumn2.y, rotColumn2.z);
        //Debug.Log("cam forward vector: " + camForwardVector);

        Quaternion camRotation = Quaternion.LookRotation(camForwardVector, camUpVector);

        cam.transform.position = new Vector3(camPosition.x * (scaleFactor / 1000.0f), camPosition.y * (scaleFactor / 1000.0f), scaleFactor*camPosition.z/1000.0f);
        //cam.transform.position = camPosition/(scaleFactor*1000.0f);
        cam.transform.rotation = camRotation;
        */

        /*
        for (int i = 0; i < ((List<object>)args[0]).Count; i++)
        {
            float val = float.Parse(((List<object>)args[0])[i].ToString());
            Debug.Log("i: " + i + " v: " + val);
        }
        */

        /*
        var array = JSON.Parse(args[0].ToString());
        for(int i = 0; i < array.Count; i++)
        {
            Debug.Log("i: " + i + " v: " + (float)array[i][0].AsFloat);
        }
        */
    }




}
