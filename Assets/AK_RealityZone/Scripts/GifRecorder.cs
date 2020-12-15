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

public class GifRecorder : MonoBehaviour {

    public GameObject cameraObject;
    public GameObject pusher;

    public bool press_this_to_record;
    public bool recording = false;
    public string gif_directory = "F:/RealityBeast/gifs";

    RenderTexture[] rt_array;

	// Use this for initialization
	void Start () {
		
	}


    int frameNumber = 0;
    public int maxFrames = 100;


    public void startRecording()
    {
        Debug.Log("start recording command");
        recording = true;
        frameNumber = 0;
        if(rt_array != null)
        {
            //dispose of previous frames:
            for (int i = 0; i < rt_array.Length; i++)
            {
                rt_array[i].Release();
            }
        }


        rt_array = new RenderTexture[maxFrames];
        for(int i = 0; i<maxFrames; i++)
        {
            rt_array[i] = new RenderTexture(cameraObject.GetComponent<Camera>().pixelWidth, cameraObject.GetComponent<Camera>().pixelHeight, 24);
        }

    }


    public void stopRecording()
    {
        recording = false;
        //string location = Application.dataPath + "/gif";
        string location = gif_directory + "/" + System.DateTime.Now.ToString("MM_dd_yyyy_h_mm_ss_tt");
        Debug.Log("saving gif images to: " + location);

        try
        {
            if (!System.IO.Directory.Exists(location))
            {
                System.IO.Directory.CreateDirectory(location);
            }


            //var folder = System.IO.Directory.CreateDirectory(location); // returns a DirectoryInfo object
            //dump all the rendertextures to files:
            if(frameNumber > 0)
            {
                Texture2D tex = new Texture2D(rt_array[0].width, rt_array[0].height, TextureFormat.ARGB32, false);
                for (int i = 0; i < (int)Mathf.Min(frameNumber,maxFrames); i++)
                {
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt_array[i];
                    tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    byte[] bytes = tex.EncodeToPNG();
                    string filename = location + "/image_" + System.String.Format("{0:0000}", i) + ".png";
                    System.IO.File.WriteAllBytes(filename, bytes);
                }

                //string command = "/C ffmpeg -i " + location + "/image_%04d.png " + location + "/output.gif";
                string command = "/C ffmpeg -framerate 50 -i " + location + "/image_%04d.png " + location + "/output.gif";
                Debug.Log("converting images to gif using: " + command);
                  // ffmpeg -i image_%04d.png output.gif
                var processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", command);
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                var process = System.Diagnostics.Process.Start(processInfo);
                process.WaitForExit();
                process.Close();


                pusher.GetComponent<Pusher>().newGif(location + "/output.gif");

                
                //delete other images:
                var info = new System.IO.DirectoryInfo(location);
                System.IO.FileInfo[] fileInfo = info.GetFiles();
                for(int ff = 0; ff<fileInfo.Length; ff++)
                {
                    if (fileInfo[ff].Name.Contains(".png"))
                    {
                        //delete it
                        System.IO.File.Delete(fileInfo[ff].FullName);
                    }
                }
                
                


            }
        }
        catch (System.IO.IOException ex)
        {
            Debug.Log(ex.Message);
        }
    }



	// Update is called once per frame
	void Update () {
        /*
        if (press_this_to_record && !recording)
        {
            //start recording
            //recording = true;
            //frameNumber = 0;
            startRecording();
        }

        if(!press_this_to_record && recording)
        {
            //stop recording   
            //recording = false;
            stopRecording();
        }
        */

        if (recording) {
            RenderTexture previousActive = RenderTexture.active;

            int idx = (int)Mathf.Min(frameNumber, maxFrames-1);
            RenderTexture currentActive = rt_array[idx];
            RenderTexture.active = rt_array[idx];
            cameraObject.GetComponent<Camera>().targetTexture = rt_array[idx];
            cameraObject.GetComponent<Camera>().Render();
            cameraObject.GetComponent<Camera>().targetTexture = null;
            RenderTexture.active = previousActive;

            frameNumber++;
        }


            
    }
}
