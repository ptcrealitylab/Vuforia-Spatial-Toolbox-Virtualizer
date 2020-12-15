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
using SimpleJSON;

public class VuforiaModule : MonoBehaviour {

    public GameObject imageCube;
    public GameObject depthCameraObject;
    public bool v_mode = false;


    public bool pressToSaveVuforiaObjectsToFile = false;
    public bool pressToFakeVuforiaUpdate = false;
    public string comment = "";

    public GameObject origin;

    public GameObject debugObject;
    public Matrix4x4 debugMatrix;

    /*      
     id: obj.id,
     ip: obj.ip,
            versionNumber: obj.vn,
            protocol: obj.pr,
            temporaryChecksum: obj.tcs,
            zone: obj.zone,
            xmlAddress: xmlAddress,
            datAddress: datAddress
            */
    struct realityEditorObjectStruct
    {
        public string id;
        public string ip;
        public string versionNumber;
        public string temporaryChecksum;
        public string zone;
        public string xmlAddress;
        public string datAddress;
        public Vector3 position;
        public Quaternion rotation;
        public int gameObjectIdx;
        //todo: fix position, rotation saving/loading
    }
    Dictionary<string, realityEditorObjectStruct> realityEditorObjectDict = new Dictionary<string, realityEditorObjectStruct>();
    Dictionary<string, realityEditorObjectStruct> realityEditorSavedObjectDict = new Dictionary<string, realityEditorObjectStruct>();
    public List<GameObject> realityEditorObjectVisualizerList = new List<GameObject>();

	// Use this for initialization
	void Start () {


        loadVuforiaObjectsFromFile();


	}

    void sendVuforiaUpdate()
    {
        //var visibleObjectsString = "{\"amazonBox0zbc6yetuoyj\":[-0.9964230765028328,0.009800613580158324,-0.08393736781840644,0,0.022929297584320937,0.987345281122669,-0.15691860457929643,0,-0.08133670600902992,0.15828222984430484,0.9840378965122027,0,329.2388939106902,77.77425852082308,-1489.0291313193022,1],\"kepwareBox4Qimhnuea3n6\":[-0.9913746548884379,0.034050970326370084,-0.12655601222953172,0,0.051082427979348755,0.9896809533465255,-0.13387410555924745,0,-0.12069159903807483,0.1391844414830788,0.9828837698713899,0,212.63741144996703,206.15960431449824,-1826.5693898311488,1],\"_WORLD_OBJECT_local\":[-0.9742120258704184,-0.00437900631612313,-0.22559212287700664,0,-0.035464931453757634,-0.9844127588621392,0.17226278091636868,0,-0.22282991544549868,0.17582138398908906,0.9588711290537871,0,161.99950094104497,-166.5114134489016,-519.0149842693205,1],\"_WORLD_OBJECT_PF5x8fv3zcgm\":[-0.9742120258704184,-0.00437900631612313,-0.22559212287700664,0,-0.035464931453757634,-0.9844127588621392,0.17226278091636868,0,-0.22282991544549868,0.17582138398908906,0.9588711290537871,0,161.99950094104497,-166.5114134489016,-519.0149842693205,1]}";
        //var visibleObjects = JSON.parse(visibleObjectsString);

        string visibleString = "\"{";

        SimpleJSON.JSONObject ja = new SimpleJSON.JSONObject();

        for(int i = 0; i<realityEditorObjectVisualizerList.Count; i++)
        {

            visibleString += "\\\"" + realityEditorObjectVisualizerList[i].name + "\\\":[";



            Matrix4x4 worldToOrigin = origin.transform.worldToLocalMatrix;
            Matrix4x4 worldToObject = realityEditorObjectVisualizerList[i].transform.worldToLocalMatrix;



            //M_world_object = M_origin_object * M_world_origin 
            //M_origin_object = M_world_object * (M_world_origin)^-1

            Matrix4x4 originToObject = worldToObject * worldToOrigin.inverse;
            Matrix4x4 objectToOrigin = originToObject.inverse;

            Matrix4x4 m = objectToOrigin;
            //Matrix4x4 m = originToObject;

            //Debug.Log("World to Origin: " + worldToOrigin);
            //Debug.Log("world to object: " + worldToObject);
            //Debug.Log("origin to object: " + originToObject);
            //Debug.Log("object to origin: " + objectToOrigin);

            /*
            GameObject dummy = new GameObject();
            dummy.transform.position = realityEditorObjectVisualizerList[i].transform.position;
            dummy.transform.rotation = realityEditorObjectVisualizerList[i].transform.rotation;
            dummy.transform.parent = virtualCam.transform;

            Matrix4x4 m = Matrix4x4.TRS(dummy.transform.localPosition, dummy.transform.localRotation, dummy.transform.localScale);
            m = m.inverse;
            

            GameObject.Destroy(dummy);
            */
            /*
            //fix righthand?
            m[0, 2] = -m[0, 2];
            m[1, 2] = -m[1, 2];
            m[2, 2] = -m[2, 2];
            */

            
            //fix righthand?
            //m[0, 0] = -m[0, 0];
            //m[1, 0] = -m[1, 0];
            //m[2, 0] = -m[2, 0];
            

            /*
            //fix rotation:
            m[0, 0] = -m[0, 0];
            m[0, 1] = -m[0, 1];
            m[0, 2] = -m[0, 2];
            */
            
            
            //flip translation
            m[0, 3] = -m[0, 3];
            m[1, 3] = -m[1, 3];
            m[2, 3] = -m[2, 3];


            
            //fix rotation:
            m[0, 2] = -m[0, 2];
            m[1, 2] = -m[1, 2];
            m[2, 2] = -m[2, 2];
            


            /*
            //flip all the x's
            m[0, 0] = -m[0, 0];
            m[0, 1] = -m[0, 1];
            m[0, 2] = -m[0, 2];
            //m[0, 3] = -m[0, 3];
            */



            /*
            for (int mm = 0; mm<3; mm++)
            {
                for(int nn = 0; nn<3; nn++)
                {
                    m[mm, nn] = 0;
                }
            }
            m[0, 0] = -1;
            m[1, 1] = 1;
            m[2, 2] = 1;
            */

            //fix vuforia mm to m
            m[0, 3] = 1000.0f * m[0, 3];
            m[1, 3] = 1000.0f * m[1, 3];
            m[2, 3] = 1000.0f * m[2, 3];

            //Debug.Log(realityEditorObjectVisualizerList[i].name);
            //Debug.Log("full matrix: " + m);
            JSONArray ma = new JSONArray();
            for(int jj = 0; jj<16; jj++)
            {
                //int sampleIdx = jj; //read through rows first
                int sampleIdx = (jj % 4) * 4 + (int)(jj / 4);
                int row = jj % 4;
                int col = (int)(jj / 4);

                //Debug.Log("idx: " + jj + " row: " + row + " col: " + col + " value: " + dummy.transform.localToWorldMatrix[row, col]);

                //visibleString += ((float)dummy.transform.localToWorldMatrix[sampleIdx]).ToString("0.00000000");
                visibleString += (m[row,col]).ToString("0.00000000"); //this should be backwards, but works?
                if (jj < 15)
                {
                    visibleString += ",";
                }
                ma.Add((float)m[sampleIdx]);
            }
            ja[realityEditorObjectVisualizerList[i].name] = ma;
            visibleString += "]";
            if(i < realityEditorObjectVisualizerList.Count - 1)
            {
                visibleString += ",";
            }

            
            


            //dump matrix to array:
            //dummy.transform.localToWorldMatrix;

        }
        visibleString += "}\"";

        //Debug.Log("visible object string: " + visibleObjectsString);
        //Debug.Log("this object string: " + ja.ToString());
        //Debug.Log("vis string: " + visibleString);
        GameObject.Find("pusher").GetComponent<Pusher>().Emit_VuforiaModuleUpdate_system_server(visibleString);

    }

    public void loadVuforiaObjectsFromFile()
    {
        string txt = System.IO.File.ReadAllText("vuforiaSaved.txt");
        JSONArray ja = JSON.Parse(txt).AsArray;
        realityEditorSavedObjectDict.Clear();
        for(int i = 0; i<ja.Count; i++)
        {
            JSONNode jn = ja[i];
            realityEditorObjectStruct reos = new realityEditorObjectStruct();
            reos.id = jn["id"];
            reos.ip = jn["ip"];
            reos.temporaryChecksum = jn["temporaryChecksum"];
            reos.versionNumber = jn["versionNumber"];
            reos.zone = jn["zone"];
            reos.xmlAddress = jn["xmlAddress"];
            reos.datAddress = jn["datAddress"];

            reos.position = new Vector3(jn["Tx"].AsFloat, jn["Ty"].AsFloat, jn["Tz"].AsFloat);
            reos.rotation = new Quaternion(jn["Qx"].AsFloat, jn["Qy"].AsFloat, jn["Qz"].AsFloat, jn["Qw"].AsFloat);

            Debug.Log("reos recovered position for: " + reos.id + " " + jn.ToString() + " " + reos.position);
            realityEditorSavedObjectDict[reos.id] = reos;
        }
    }


    void updateStructsBasedOnGameObjects()
    {
        List<string> keys = new List<string>(realityEditorObjectDict.Keys);
        for(int i = 0; i<keys.Count; i++)
        {
            realityEditorObjectStruct reos = realityEditorObjectDict[keys[i]];

            //Debug.Log("looking for a corresponding gameobject for: " + reos.id);
            //find the gameobject with this name:
            int idx = -1;
            for(int jj = 0; jj<realityEditorObjectVisualizerList.Count; jj++)
            {
                //Debug.Log(realityEditorObjectVisualizerList[jj].name + " == " + reos.id + "?" + (realityEditorObjectVisualizerList[jj].name == reos.id));
                if(realityEditorObjectVisualizerList[jj].name == reos.id)
                {
                    idx = jj;
                }
            }

            if (idx >= 0)
            {
                //Debug.Log("updating struct for: " + realityEditorObjectVisualizerList[idx].name + " " + realityEditorObjectVisualizerList[idx].transform.position);
                reos.position = realityEditorObjectVisualizerList[idx].transform.position;
                reos.rotation = realityEditorObjectVisualizerList[idx].transform.rotation;
            }
            else
            {
                Debug.Log("*** could not update struct for: " + reos.id);
            }
            realityEditorObjectDict[keys[i]] = reos;
        }

        /*
        foreach(KeyValuePair<string, realityEditorObjectStruct> pair in realityEditorObjectDict)
        {
            realityEditorObjectStruct reos = realityEditorObjectDict[pair.Key];
            reos.position = realityEditorObjectVisualizerList[pair.Value.gameObjectIdx].transform.position;
            reos.rotation = realityEditorObjectVisualizerList[pair.Value.gameObjectIdx].transform.rotation;
            realityEditorObjectDict[pair.Key] = reos;
        }
        */
    }


    public void saveVuforiaObjectsToFile()
    {
        //update position of this object's struct in the dictionary:
        updateStructsBasedOnGameObjects();

        JSONArray ja = new JSONArray();
        foreach(KeyValuePair<string, realityEditorObjectStruct> pair in realityEditorObjectDict)
        {

            //Debug.Log("save operating on: " + pair.Key + " " + pair.Value.ip);
            //Debug.Log("content of struct");
            Debug.Log(pair.Value.id + " " + pair.Value.ip + " " + pair.Value.temporaryChecksum + " " + pair.Value.versionNumber + " " + pair.Value.zone + " " + pair.Value.xmlAddress + " " + pair.Value.datAddress);

            //Debug.Log("temporary checksum value: " + pair.Value.temporaryChecksum + " nulloperator: " + (pair.Value.temporaryChecksum ?? "null") + " ==\"\"?" + (pair.Value.temporaryChecksum == ""));

            JSONNode jo = new SimpleJSON.JSONObject();
            jo.Add("id", pair.Value.id ?? "");
            jo.Add("ip", pair.Value.ip ?? "");
            jo.Add("temporaryChecksum", pair.Value.temporaryChecksum ?? "");
            jo.Add("versionNumber", pair.Value.versionNumber ?? "");
            jo.Add("zone", pair.Value.zone ?? "");
            jo.Add("xmlAddress", pair.Value.xmlAddress ?? "");
            jo.Add("dataAddress", pair.Value.datAddress ?? "");
            jo.Add("Tx", pair.Value.position.x);
            jo.Add("Ty", pair.Value.position.y);
            jo.Add("Tz", pair.Value.position.z);
            jo.Add("Qx", pair.Value.rotation.x);
            jo.Add("Qy", pair.Value.rotation.y);
            jo.Add("Qz", pair.Value.rotation.z);
            jo.Add("Qw", pair.Value.rotation.w);
            /*
            jo.Add("id", pair.Value.id);
            jo.Add("ip", pair.Value.ip);              
            jo.Add("temporaryChecksum", pair.Value.temporaryChecksum);
            jo.Add("versionNumber", pair.Value.versionNumber);
            jo.Add("zone", pair.Value.zone);
            jo.Add("xmlAddress", pair.Value.xmlAddress);
            jo.Add("dataAddress", pair.Value.datAddress);
            jo.Add("Tx", pair.Value.position.x);
            jo.Add("Ty", pair.Value.position.y);
            jo.Add("Tz", pair.Value.position.z);
            jo.Add("Qx", pair.Value.rotation.x);
            jo.Add("Qy", pair.Value.rotation.y);
            jo.Add("Qz", pair.Value.rotation.z);
            jo.Add("Qw", pair.Value.rotation.w);
            */
            //JSONNode cameraCalibrationResult = new JSONObject();
            //cameraCalibrationResult.Add("camera_enumeration", 1);
            //cameraCalibrationResult.Add("serial_number", "cool");


            //jo["id"] = pair.Value.id;
            /*
            jo["ip"] = pair.Value.ip;
            jo["temporaryChecksum"] = pair.Value.temporaryChecksum;
            jo["versionNumber"] = pair.Value.versionNumber;
            jo["zone"] = pair.Value.zone;
            jo["xmlAddress"] = pair.Value.xmlAddress;
            jo["datAddress"] = pair.Value.datAddress;






            jo["Tx"] = pair.Value.position.x;
            jo["Ty"] = pair.Value.position.y;
            jo["Tz"] = pair.Value.position.z;
            jo["Qx"] = pair.Value.rotation.x;
            jo["Qy"] = pair.Value.rotation.y;
            jo["Qz"] = pair.Value.rotation.z;
            jo["QW"] = pair.Value.rotation.w;
            */
            //Debug.Log("camera calibration result: " + cameraCalibrationResult.ToString());
            //Debug.Log("adding jo: " + jo.ToString());

            ja.Add(jo);


        }
        //Debug.Log("json array: " + ja.ToString());
        System.IO.File.WriteAllText("vuforiaSaved.txt", ja.ToString());
    }

    public void realityEditorObject(string jsonPacket)
    {
        JSONNode jn = JSON.Parse(jsonPacket);
        //Debug.Log("received object: " + jsonPacket);

        realityEditorObjectStruct reos = new realityEditorObjectStruct();
        reos.id = jn["id"] ?? "";
        reos.ip = jn["ip"] ?? "";
        reos.temporaryChecksum = jn["temporaryChecksum"] ?? "";
        reos.versionNumber = jn["versionNumber"] ?? "";
        reos.zone = jn["zone"] ?? "";
        reos.xmlAddress = jn["xmlAddress"] ?? "";
        reos.datAddress = jn["datAddress"] ?? "";


        if (realityEditorObjectDict.ContainsKey(reos.id))
        {
            if(realityEditorObjectDict[reos.id].temporaryChecksum != reos.temporaryChecksum)
            {
                //todo: redownload model files!
            }
        }
        else
        {
            GameObject go = new GameObject();
            go.name = reos.id;
            go.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            go.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            go.transform.rotation = Quaternion.identity;




            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            cube.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            cube.transform.rotation = Quaternion.identity;
            cube.name = reos.id;
            cube.transform.parent = go.transform;
            cube.GetComponent<MeshRenderer>().enabled = false;

            //todo
            //get the transform from a previously saved version if it exists:
            if (realityEditorSavedObjectDict.ContainsKey(reos.id))
            {
                reos.position = realityEditorSavedObjectDict[reos.id].position;
                reos.rotation = realityEditorSavedObjectDict[reos.id].rotation;
                go.transform.position = reos.position;
                go.transform.rotation = reos.rotation;
            }
            else
            {
                //keep default position saved
                reos.position = go.transform.position;
                reos.rotation = go.transform.rotation;
            }

            realityEditorObjectVisualizerList.Add(go);
            reos.gameObjectIdx = realityEditorObjectVisualizerList.Count;

            StartCoroutine(downloadAndSendModelFiles(reos.id, reos.xmlAddress, reos.datAddress));

            Debug.Log("adding new object: " + reos);
            realityEditorObjectDict.Add(reos.id, reos);




            //add it to vuforia search list as well:
        }


    }

    IEnumerator downloadAndSendModelFiles(string id, string xmlAddress, string datAddress)
    {
        string xmlText = "";
        WWW wwwXml = new WWW(xmlAddress);
        yield return wwwXml;
        xmlText = wwwXml.text;

        string datText = "";
        WWW wwwDat = new WWW(datAddress);
        yield return wwwDat;
        datText = wwwDat.text;

        Debug.Log("got xml and dat for: " + id);
        Debug.Log(xmlText);
        Debug.Log(datText);


        yield return null;
    }


    public void vuforiaResponse(string jsonPacket)
    {
        //Debug.Log("received: " + jsonPacket);
        JSONNode jn = JSON.Parse(jsonPacket);
        string name = jn["name"];
        name = "vuforia_object_" + name;

        GameObject vuforiaObject = GameObject.Find(name);
        if (vuforiaObject == null)
        {
            vuforiaObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vuforiaObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            vuforiaObject.GetComponent<MeshRenderer>().enabled = false;
            vuforiaObject.name = name;
        }

        float Tx = jn["Tx"].AsFloat;
        float Ty = jn["Ty"].AsFloat;
        float Tz = jn["Tz"].AsFloat;
        float Qx = jn["Qx"].AsFloat;
        float Qy = jn["Qy"].AsFloat;
        float Qz = jn["Qz"].AsFloat;
        float Qw = jn["Qw"].AsFloat;

        vuforiaObject.transform.parent = depthCameraObject.transform;
        vuforiaObject.transform.localPosition = new Vector3(Tx, Ty, Tz);
        vuforiaObject.transform.localRotation = new Quaternion(Qx, Qy, Qz, Qw);
        vuforiaObject.transform.parent = null;


        Debug.Log(jsonPacket);
    }

    void sendToVuforia()
    {
        //Debug.Log("capturing image for vuforia");
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        sw.Reset();
        sw.Start();
        Texture2D colorTexture = (Texture2D)imageCube.GetComponent<Renderer>().material.mainTexture;
        sw.Stop();
        //Debug.Log("time to acquire texture pointer: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        RenderTexture colorRT = new RenderTexture(colorTexture.width, colorTexture.height, 24, RenderTextureFormat.ARGB32);
        sw.Stop();
        //Debug.Log("time to declare render texture: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        Graphics.Blit(colorTexture, colorRT); //move it from colortexture gpu to rendertexture
        sw.Stop();
        //Debug.Log("time to blit texture to render texture: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        RenderTexture.active = colorRT;
        sw.Stop();
        //Debug.Log("time to make render texture active: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));


        //debugFreezeRendertexture = colorRT;
        sw.Reset();
        sw.Start();
        Texture2D colorFreeze = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.ARGB32, false);
        sw.Stop();
        //Debug.Log("time to declare a freeze texture: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        colorFreeze.ReadPixels(new UnityEngine.Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0);
        sw.Stop();
        //Debug.Log("time to read pixels from rendertexture: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        byte[] c = colorFreeze.GetRawTextureData();
        string outstring = "";
        for(int i = 0; i<50; i++)
        {
            outstring += c[i] + "-";
        }
        //Debug.Log("first 50: " + outstring);

        sw.Reset();
        sw.Start();
        colorFreeze.Apply();
        sw.Stop();
        //Debug.Log("time to do apply: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));




        sw.Reset();
        sw.Start();
        byte[] b = colorFreeze.GetRawTextureData();
        sw.Stop();
        //Debug.Log("time to return raw texture data: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));
        //Array.Reverse(b);

        string outstring2 = "";
        for (int i = 0; i < 50; i++)
        {
            outstring2 += b[i] + "-";
        }
        //Debug.Log("first 50: " + outstring2);

        /*
        //flip up down:
        byte[] b_flip = new byte[b.Length];
        for (int i = 0; i < colorFreeze.height; i++)
        {
            for (int j = 0; j < 4 * colorFreeze.width; j++)
            {
                b_flip[i * (colorFreeze.width * 4) + j] = b[((colorFreeze.height - 1) - i) * (colorFreeze.width * 4) + j];
            }
        }
        //b = b_flip;
        */

    sw.Reset();
        sw.Start();
        string image_data = System.Convert.ToBase64String(b);
        sw.Stop();
        //Debug.Log("time to convert to base 64: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));


        sw.Reset();
        sw.Start();
        JSONNode vuforiaRequest = new SimpleJSON.JSONObject();
        vuforiaRequest.Add("imageData", image_data);
        vuforiaRequest.Add("imageWidth", colorFreeze.width);
        vuforiaRequest.Add("imageHeight", colorFreeze.height);

        vuforiaRequest.Add("A_offset", 0);
        vuforiaRequest.Add("R_offset", 1);
        vuforiaRequest.Add("G_offset", 2);
        vuforiaRequest.Add("B_offset", 3);
        string data = vuforiaRequest.ToString();
        sw.Stop();
        //Debug.Log("time to make vuforia string: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        GameObject.Find("pusher").GetComponent<Pusher>().sendToVuforia(data);
        sw.Stop();
        //Debug.Log("time to send vuforia string: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

        sw.Reset();
        sw.Start();
        DestroyImmediate(colorRT);
        DestroyImmediate(colorFreeze);
        sw.Stop();
        //Debug.Log("time to destroy resources: " + ((float)sw.ElapsedTicks / (float)System.TimeSpan.TicksPerMillisecond).ToString("F6"));

    }

    IEnumerator flash()
    {
        comment = "done.";
        yield return new WaitForSeconds(0.5f);
        //yield new WaitForSeconds(0.5f);
        comment = "";
        yield return null;
    }

    public bool continouslyUpdateVuforia = true;
    float vuforiaUpdateTime = 0.0f;
    float lastTime = 0.0f;
    float period = 1.0f;
	// Update is called once per frame
	void Update () {

        if (continouslyUpdateVuforia)
        {
            if(Time.time - vuforiaUpdateTime > 0.2f)
            {
                vuforiaUpdateTime = Time.time;
                sendVuforiaUpdate();
            }
        }


        debugObject = GameObject.Find("kepwareBox4Qimhnuea3n6");
        if(debugObject != null)
        {
            Matrix4x4 worldToOrigin = origin.transform.worldToLocalMatrix;
            Matrix4x4 worldToObject = debugObject.transform.worldToLocalMatrix;



            //M_world_object = M_origin_object * M_world_origin 
            //M_origin_object = M_world_object * (M_world_origin)^-1

            Matrix4x4 originToObject = worldToObject * worldToOrigin.inverse;
            Matrix4x4 objectToOrigin = originToObject.inverse;

            Matrix4x4 m = objectToOrigin;
            //Matrix4x4 m = originToObject;

            debugMatrix = m;

        }




        /*
        if (Input.GetKeyDown(KeyCode.V))
        {
            sendToVuforia();
        }
        */


        if (v_mode)
        {
            if (Time.time - lastTime > period)
            {
                lastTime = Time.time;
                sendToVuforia();
            }
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            v_mode = !v_mode;
        }


        if (pressToFakeVuforiaUpdate)
        {
            pressToFakeVuforiaUpdate = false;
            StartCoroutine(flash());
            sendVuforiaUpdate();
            //send out those vuforia updates!
        }

        if (pressToSaveVuforiaObjectsToFile)
        {
            pressToSaveVuforiaObjectsToFile = false;
            StartCoroutine(flash());
            saveVuforiaObjectsToFile();
        }



        



        /*
        if (Input.GetKeyDown(KeyCode.V))
        {
            sendToVuforia();
        }
        */

        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("capturing image for vuforia");
            Texture2D colorTexture = (Texture2D)imageCube.GetComponent<Renderer>().material.mainTexture;
            RenderTexture colorRT = new RenderTexture(colorTexture.width, colorTexture.height, 24, RenderTextureFormat.ARGB32);
            Graphics.Blit(colorTexture, colorRT); //move it from colortexture gpu to rendertexture
            RenderTexture.active = colorRT;
            //debugFreezeRendertexture = colorRT;
            Texture2D colorFreeze = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.ARGB32, false);
            colorFreeze.ReadPixels(new UnityEngine.Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0);
            colorFreeze.Apply();

            string fileName = Application.dataPath + "/vuforia_capture.png";
            Debug.Log("saving to: " + fileName);
            System.IO.File.WriteAllBytes(fileName, colorFreeze.EncodeToPNG());

            //string col_file_name = Application.dataPath + "/col_" + i + ".bytes";
            //System.IO.File.WriteAllBytes(col_file_name, colorFreeze.GetRawTextureData());
            DestroyImmediate(colorRT);
            DestroyImmediate(colorFreeze);
        }
		
	}
}
