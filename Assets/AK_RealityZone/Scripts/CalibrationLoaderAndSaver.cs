//calibration loader and saver for the azure kinect
//written by Hisham Bedri, Reality Lab, 2019

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;

public class CalibrationLoaderAndSaver : MonoBehaviour {
    public GameObject AK_receiver;
    List<akplay.camInfo> camInfoList;

    bool camerasReady = false;

    public bool saveCalibrationButton = false;
    public string calibrationFileName = "AK_calibration.txt";



    public void saveCalibration()
    {
        JSONObject ja = JSONObject.arr;
        for(int cc = 0; cc<camInfoList.Count; cc++)
        {
            JSONObject jn = new JSONObject();
            jn.AddField("serial", camInfoList[cc].serial);
            jn.AddField("position_x", camInfoList[cc].visualization.transform.position.x);
            jn.AddField("position_y", camInfoList[cc].visualization.transform.position.y);
            jn.AddField("position_z", camInfoList[cc].visualization.transform.position.z);
            jn.AddField("quaternion_x", camInfoList[cc].visualization.transform.rotation.x);
            jn.AddField("quaternion_y", camInfoList[cc].visualization.transform.rotation.y);
            jn.AddField("quaternion_z", camInfoList[cc].visualization.transform.rotation.z);
            jn.AddField("quaternion_w", camInfoList[cc].visualization.transform.rotation.w);
            ja.Add(jn);
        }
        string path = Application.dataPath + "/" + calibrationFileName;
        System.IO.File.WriteAllText(path, ja.ToString());
        Debug.Log("saveing calibration string to: " + path + " with data: " + ja.ToString());

    }

    void loadCalibration()
    {
        string path = Application.dataPath + "/" + calibrationFileName;
        string calibration_string = System.IO.File.ReadAllText(path);
        JSONNode jn = JSON.Parse(calibration_string);
        JSONArray ja = jn.AsArray;

        for(int i = 0; i<ja.Count; i++)
        {

            //find the camera with this serial number:
            int camIdx = -1;
            for(int cc = 0; cc<camInfoList.Count; cc++)
            {
                if(camInfoList[cc].serial == ja[i]["serial"].AsInt)
                {
                    camIdx = cc;
                }
            }

            if(camIdx >= 0)
            {
                Vector3 cam_position = new Vector3(ja[camIdx]["position_x"].AsFloat,
                                                    ja[camIdx]["position_y"].AsFloat,
                                                    ja[camIdx]["position_z"].AsFloat);

                Quaternion cam_rotation = new Quaternion(ja[camIdx]["quaternion_x"].AsFloat,
                                                        ja[camIdx]["quaternion_y"].AsFloat,
                                                        ja[camIdx]["quaternion_z"].AsFloat,
                                                        ja[camIdx]["quaternion_w"].AsFloat);

                //Debug.Log("Setting camera: " + camIdx + " rotation: " + cam_rotation);

                camInfoList[camIdx].visualization.transform.position = cam_position;
                camInfoList[camIdx].visualization.transform.rotation = cam_rotation;
            }



        }






    }





    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (!camerasReady && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            camerasReady = true;
            camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;
            loadCalibration();
        }


        if (saveCalibrationButton)
        {
            saveCalibrationButton = false;

            if (camerasReady)
            {
                saveCalibration();
            }
        }



    }
}
