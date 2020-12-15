/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//Target based calibration for the azure kinect
//written by Hisham Bedri, Reality Lab, 2019

using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class AK_calibration : MonoBehaviour {

    public float h_low = 0.0f;
    public float h_high = 255.0f;
    public float s_low = 0.0f;
    public float s_high = 255.0f;
    public float l_low = 0.0f;
    public float l_high = 255.0f;

    bool camerasReady = false;
    public bool showThreshold;

    public GameObject AK_receiver;

    public List<GameObject> threshold_display_list;
    public List<GameObject> checkerboard_display_list;

    public GameObject calibrationSaverAndLoader;


    // Use this for initialization
    void Start () {
		
	}

    void setupCalibrationUI()
    {
        float delta = 0.15f;
        for(int i = 0; i<AK_receiver.GetComponent<akplay>().camInfoList.Count; i++)
        {
            GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
            threshold.layer = LayerMask.NameToLayer("Debug");
            threshold.name = "threshold_" + i;
            threshold.transform.parent = gameObject.transform;
            threshold.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            threshold.transform.localPosition = new Vector3(0.0f, delta*i, 0.0f);
            threshold.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));
            threshold_display_list.Add(threshold);

            GameObject checkerboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            checkerboard.layer = LayerMask.NameToLayer("Debug");
            checkerboard.name = "checkerboard_" + i;
            checkerboard.transform.parent = gameObject.transform;
            checkerboard.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            checkerboard.transform.localPosition = new Vector3(delta, delta * i, 0.0f);
            checkerboard.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));
            checkerboard_display_list.Add(checkerboard);
        }
    }

    float lastThresholdTime = 0.0f;
    float thresholdPeriod = 0.5f;


    //Texture2D orangeTexture = null;
    //Texture2D colorTexture = null;

    // Update is called once per frame
    void Update () {
        //check if cameras are ready:
        if (!camerasReady && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            camerasReady = true;
            setupCalibrationUI();
        }

        if (camerasReady)
        {
            if(showThreshold && (Time.time - lastThresholdTime) > thresholdPeriod){
                lastThresholdTime = Time.time;
                handleThreshold();
            }

            if (!showThreshold)
            {
                if (Input.GetKeyDown(KeyCode.D))
                {
                    handleCalibration();
                    calibrationSaverAndLoader.GetComponent<CalibrationLoaderAndSaver>().saveCalibration();
                }
            }
        }
    }


    void handleCalibration()
    {
        for (int i = 0; i < AK_receiver.GetComponent<akplay>().camInfoList.Count; i++)
        {
            //create color mat:
            byte[] colorBytes = ((Texture2D)(AK_receiver.GetComponent<akplay>().camInfoList[i].colorCube.GetComponent<Renderer>().material.mainTexture)).GetRawTextureData();
            GCHandle ch = GCHandle.Alloc(colorBytes, GCHandleType.Pinned);
            Mat colorMat = new Mat(AK_receiver.GetComponent<akplay>().camInfoList[i].color_height, AK_receiver.GetComponent<akplay>().camInfoList[i].color_width, CvType.CV_8UC4);
            Utils.copyToMat(ch.AddrOfPinnedObject(), colorMat);
            ch.Free();

            //OpenCVForUnity.CoreModule.Core.flip(colorMat, colorMat, 0);

            //detect a chessboard in the image, and refine the points, and save the pixel positions:
            MatOfPoint2f positions = new MatOfPoint2f();
            int resizer = 4;
            resizer = 1; //noresize!
            Mat colorMatSmall = new Mat(); //~27 ms each
            Imgproc.resize(colorMat, colorMatSmall, new Size(colorMat.cols() / resizer, colorMat.rows() / resizer));
            bool success = Calib3d.findChessboardCorners(colorMatSmall, new Size(7, 7), positions);
            for (int ss = 0; ss < positions.rows(); ss++)
            {
                double[] data = positions.get(ss, 0);
                data[0] = data[0] * resizer;
                data[1] = data[1] * resizer;

                positions.put(ss, 0, data);
            }

            //subpixel, drawing chessboard, and getting orange blobs takes 14ms
            TermCriteria tc = new TermCriteria();
            Imgproc.cornerSubPix(colorMat, positions, new Size(5, 5), new Size(-1, -1), tc);

            Mat chessboardResult = new Mat();
            colorMat.copyTo(chessboardResult);
            Calib3d.drawChessboardCorners(chessboardResult, new Size(7, 7), positions, success);

           

            //Find the orange blobs:
            Mat orangeMask = new Mat();
            Vector2[] blobs = getOrangeBlobs(ref colorMat, ref orangeMask);

            //find blob closest to chessboard
            if (success && (blobs.Length > 0))
            {
                Debug.Log("found a chessboard and blobs for camera: " + i);

                // time to get pin1 and chessboard positions: 27ms
                //find pin1:
                Point closestBlob = new Point();
                int pin1idx = getPin1(positions, blobs, ref closestBlob);
                Imgproc.circle(chessboardResult, new Point(positions.get(pin1idx, 0)[0], positions.get(pin1idx, 0)[1]), 10, new Scalar(255, 0, 0), -1);
                Imgproc.circle(chessboardResult, closestBlob, 10, new Scalar(255, 255, 0), -1);


                //get world positions of chessboard
                Point[] realWorldPointArray = new Point[positions.rows()];
                Point3[] realWorldPointArray3 = new Point3[positions.rows()];
                Point[] imagePointArray = new Point[positions.rows()];
                //getChessBoardWorldPositions(positions, pin1idx, 0.0498f, ref realWorldPointArray, ref realWorldPointArray3, ref imagePointArray); //green and white checkerboard.
                getChessBoardWorldPositions(positions, pin1idx, 0.07522f, ref realWorldPointArray, ref realWorldPointArray3, ref imagePointArray); //black and white checkerboard.


                string text = "";
                float decimals = 1000.0f;
                int text_red = 255;
                int text_green = 0;
                int text_blue = 0;
                text = ((int)(realWorldPointArray3[0].x * decimals)) / decimals + "," + ((int)(realWorldPointArray3[0].y * decimals)) / decimals + "," + ((int)(realWorldPointArray3[0].z * decimals)) / decimals;
                //text = sprintf("%f,%f,%f", realWorldPointArray3[0].x, realWorldPointArray3[0].y, realWorldPointArray3[0].z);
                Imgproc.putText(chessboardResult, text, new Point(positions.get(0, 0)[0], positions.get(0, 0)[1]), 0, .6, new Scalar(text_red, text_green, text_blue));
                text = ((int)(realWorldPointArray3[6].x * decimals)) / decimals + "," + ((int)(realWorldPointArray3[6].y * decimals)) / decimals + "," + ((int)(realWorldPointArray3[6].z * decimals)) / decimals;
                //text = sprintf("%f,%f,%f", realWorldPointArray3[0].x, realWorldPointArray3[0].y, realWorldPointArray3[0].z);
                Imgproc.putText(chessboardResult, text, new Point(positions.get(6, 0)[0], positions.get(6, 0)[1]), 0, .6, new Scalar(text_red, text_green, text_blue));
                text = ((int)(realWorldPointArray3[42].x * decimals)) / decimals + "," + ((int)(realWorldPointArray3[42].y * decimals)) / decimals + "," + ((int)(realWorldPointArray3[42].z * decimals)) / decimals;
                //text = sprintf("%f,%f,%f", realWorldPointArray3[0].x, realWorldPointArray3[0].y, realWorldPointArray3[0].z);
                Imgproc.putText(chessboardResult, text, new Point(positions.get(42, 0)[0], positions.get(42, 0)[1]), 0, .6, new Scalar(text_red, text_green, text_blue));
                text = ((int)(realWorldPointArray3[48].x * decimals)) / decimals + "," + ((int)(realWorldPointArray3[48].y * decimals)) / decimals + "," + ((int)(realWorldPointArray3[48].z * decimals)) / decimals;
                //text = sprintf("%2.2f,%2.2f,%2.2f", realWorldPointArray3[48].x, realWorldPointArray3[48].y, realWorldPointArray3[48].z);
                Imgproc.putText(chessboardResult, text, new Point(positions.get(48, 0)[0], positions.get(48, 0)[1]), 0, .6, new Scalar(text_red, text_green, text_blue));




                Mat cameraMatrix = Mat.eye(3, 3, CvType.CV_64F);
                cameraMatrix.put(0, 0, AK_receiver.GetComponent<akplay>().camInfoList[i].color_fx);
                cameraMatrix.put(1, 1, AK_receiver.GetComponent<akplay>().camInfoList[i].color_fy);
                cameraMatrix.put(0, 2, AK_receiver.GetComponent<akplay>().camInfoList[i].color_cx);
                cameraMatrix.put(1, 2, AK_receiver.GetComponent<akplay>().camInfoList[i].color_cy);

                double[] distortion = new double[8];
                
                distortion[0] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k1;
                distortion[1] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k2;
                distortion[2] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_p1;
                distortion[3] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_p2;
                distortion[4] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k3;
                distortion[5] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k4;
                distortion[6] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k5;
                distortion[7] = AK_receiver.GetComponent<akplay>().camInfoList[i].color_k6;
                

                /*
                distortion[0] = 0.0;
                distortion[1] = 0.0;
                distortion[2] = 0.0;
                distortion[3] = 0.0;
                distortion[4] = 0.0;
                distortion[5] = 0.0;
                distortion[6] = 0.0;
                distortion[7] = 0.0;
                */

                //~1 ms to solve for pnp
                Mat rvec = new Mat();
                Mat tvec = new Mat();
                bool solvepnpSucces = Calib3d.solvePnP(new MatOfPoint3f(realWorldPointArray3), new MatOfPoint2f(imagePointArray), cameraMatrix, new MatOfDouble(distortion), rvec, tvec);

                Mat R = new Mat();
                Calib3d.Rodrigues(rvec, R);


                //calculate unity vectors, and camera transforms
                Mat camCenter = -R.t() * tvec;
                Mat forwardOffset = new Mat(3, 1, tvec.type());
                forwardOffset.put(0, 0, 0);
                forwardOffset.put(1, 0, 0);
                forwardOffset.put(2, 0, 1);
                Mat upOffset = new Mat(3, 1, tvec.type());
                upOffset.put(0, 0, 0);
                upOffset.put(1, 0, -1);
                upOffset.put(2, 0, 0);

                Mat forwardVectorCV = R.t() * (forwardOffset - tvec);
                forwardVectorCV = forwardVectorCV - camCenter;
                Mat upVectorCV = R.t() * (upOffset - tvec);
                upVectorCV = upVectorCV - camCenter;

                Vector3 forwardVectorUnity = new Vector3((float)forwardVectorCV.get(0, 0)[0], (float)forwardVectorCV.get(2, 0)[0], (float)forwardVectorCV.get(1, 0)[0]); //need to flip y and z due to unity coordinate system
                Vector3 upVectorUnity = new Vector3((float)upVectorCV.get(0, 0)[0], (float)upVectorCV.get(2, 0)[0], (float)upVectorCV.get(1, 0)[0]); //need to flip y and z due to unity coordinate system
                Vector3 camCenterUnity = new Vector3((float)camCenter.get(0, 0)[0], (float)camCenter.get(2, 0)[0], (float)camCenter.get(1, 0)[0]);
                Quaternion rotationUnity = Quaternion.LookRotation(forwardVectorUnity, upVectorUnity);







                GameObject colorMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //colorMarker.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
                //colorMarker.transform.parent = AK_receiver.transform;
                colorMarker.layer = LayerMask.NameToLayer("Debug");
                colorMarker.transform.position = camCenterUnity;
                colorMarker.transform.rotation = Quaternion.LookRotation(forwardVectorUnity, upVectorUnity);
                colorMarker.GetComponent<Renderer>().material.color = Color.blue;

                Vector3 forwardDepth = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.MultiplyPoint(forwardVectorUnity);
                Vector3 upDepth = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.MultiplyPoint(upVectorUnity);
                Vector3 camCenterDepth = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.MultiplyPoint(camCenterUnity);
                Quaternion rotationDepth = Quaternion.LookRotation(forwardDepth, upDepth);

                GameObject depthMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                depthMarker.layer = LayerMask.NameToLayer("Debug");
                depthMarker.transform.parent = colorMarker.transform;
                //depthMarker.transform.localScale = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.lossyScale;
                
                depthMarker.transform.localRotation = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.inverse.rotation;

                Vector3 matrixPosition = new Vector3(AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.inverse.GetColumn(3).x,
                                                        AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.inverse.GetColumn(3).y,
                                                        AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.inverse.GetColumn(3).z);
                

                /*
                depthMarker.transform.localRotation = AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.rotation;

                Vector3 matrixPosition = new Vector3(AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.GetColumn(3).x,
                                                        AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.GetColumn(3).y,
                                                        AK_receiver.GetComponent<akplay>().camInfoList[i].color_extrinsics.GetColumn(3).z);
                */

                depthMarker.transform.localPosition = -matrixPosition;
                depthMarker.transform.parent = null;

                colorMarker.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
                depthMarker.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);

                //depthMarker.transform.parent = AK_receiver.transform;
                //depthMarker.transform.position = camCenterDepth;
                //depthMarker.transform.rotation = Quaternion.LookRotation(forwardDepth-camCenterDepth, upDepth-camCenterDepth);
                depthMarker.GetComponent<Renderer>().material.color = Color.red;


                AK_receiver.GetComponent<akplay>().camInfoList[i].visualization.transform.position = depthMarker.transform.position; //need to flip y and z due to unity coordinate system
                AK_receiver.GetComponent<akplay>().camInfoList[i].visualization.transform.rotation = depthMarker.transform.rotation;


            }


            //draw chessboard result to calibration ui:
            Texture2D colorTexture = new Texture2D(chessboardResult.cols(), chessboardResult.rows(), TextureFormat.BGRA32, false);
            colorTexture.LoadRawTextureData((IntPtr)chessboardResult.dataAddr(), (int)chessboardResult.total() * (int)chessboardResult.elemSize());
            colorTexture.Apply();
            checkerboard_display_list[i].GetComponent<Renderer>().material.mainTexture = colorTexture;

            //draw threshold to calibration ui:
            Texture2D orangeTexture = new Texture2D(orangeMask.cols(), orangeMask.rows(), TextureFormat.R8, false);
            orangeTexture.LoadRawTextureData((IntPtr)orangeMask.dataAddr(), (int)orangeMask.total() * (int)orangeMask.elemSize());
            orangeTexture.Apply();
            threshold_display_list[i].GetComponent<Renderer>().material.mainTexture = orangeTexture;
        }
    }



    void handleThreshold()
    {
        for (int i = 0; i < AK_receiver.GetComponent<akplay>().camInfoList.Count; i++)
        {
            //create color mat:
            byte[] colorBytes = ((Texture2D)(AK_receiver.GetComponent<akplay>().camInfoList[i].colorCube.GetComponent<Renderer>().material.mainTexture)).GetRawTextureData();
            GCHandle ch = GCHandle.Alloc(colorBytes, GCHandleType.Pinned);
            Mat colorMat = new Mat(AK_receiver.GetComponent<akplay>().camInfoList[i].color_height, AK_receiver.GetComponent<akplay>().camInfoList[i].color_width, CvType.CV_8UC4);
            Utils.copyToMat(ch.AddrOfPinnedObject(), colorMat);
            ch.Free();

            //perform threshold on color
            Mat orangeMask = new Mat();
            getOrangeMask(ref colorMat, ref orangeMask);

            //copy to threshold part of calibration ui
            Texture2D orangeTexture = new Texture2D(orangeMask.cols(), orangeMask.rows(), TextureFormat.R8, false);
            orangeTexture.LoadRawTextureData((IntPtr)orangeMask.dataAddr(), (int)orangeMask.total() * (int)orangeMask.elemSize());
            orangeTexture.Apply();
            threshold_display_list[i].GetComponent<Renderer>().material.mainTexture = orangeTexture;

            //copy to color part of calibration ui
            Texture2D colorTexture = new Texture2D(colorMat.cols(), colorMat.rows(), TextureFormat.BGRA32, false);
            colorTexture.LoadRawTextureData((IntPtr)colorMat.dataAddr(), (int)colorMat.total() * (int)colorMat.elemSize());
            colorTexture.Apply();
            checkerboard_display_list[i].GetComponent<Renderer>().material.mainTexture = colorTexture;
        }
    }

    void getOrangeMask(ref Mat colorImage, ref Mat orangeMask)
    {
        Mat hsvMat = new Mat();
        Imgproc.cvtColor(colorImage, hsvMat, Imgproc.COLOR_RGB2HSV);
        orangeMask = new Mat();

        Scalar orangeLower = new Scalar((int)Mathf.Clamp(h_low, 0.0f, 255.0f), (int)Mathf.Clamp(s_low, 0.0f, 255.0f), (int)Mathf.Clamp(l_low, 0.0f, 255.0f));
        Scalar orangeUpper = new Scalar((int)Mathf.Clamp(h_high, 0.0f, 255.0f), (int)Mathf.Clamp(s_high, 0.0f, 255.0f), (int)Mathf.Clamp(l_high, 0.0f, 255.0f));

        Core.inRange(hsvMat, orangeLower, orangeUpper, orangeMask);
    }

    Vector2[] getOrangeBlobs(ref Mat colorImage, ref Mat orangeMask)
    {


        Mat hsvMat = new Mat();
        Imgproc.cvtColor(colorImage, hsvMat, Imgproc.COLOR_RGB2HSV);
        orangeMask = new Mat();

        Scalar orangeLower = new Scalar((int)Mathf.Clamp(h_low, 0.0f, 255.0f), (int)Mathf.Clamp(s_low, 0.0f, 255.0f), (int)Mathf.Clamp(l_low, 0.0f, 255.0f));
        Scalar orangeUpper = new Scalar((int)Mathf.Clamp(h_high, 0.0f, 255.0f), (int)Mathf.Clamp(s_high, 0.0f, 255.0f), (int)Mathf.Clamp(l_high, 0.0f, 255.0f));

        Core.inRange(hsvMat, orangeLower, orangeUpper, orangeMask);

        List<MatOfPoint> contours = new List<MatOfPoint>();

        Mat heirarchy = new Mat();
        Imgproc.findContours(orangeMask, contours, heirarchy, Imgproc.RETR_CCOMP, Imgproc.CHAIN_APPROX_SIMPLE);

        Vector2[] blobs = new Vector2[contours.Count];
        for (int i = 0; i < contours.Count; i++)
        {
            //get center.
            OpenCVForUnity.CoreModule.Rect rectangle = Imgproc.boundingRect(contours[i]);
            blobs[i] = new Vector2(rectangle.x, rectangle.y);
        }

        return blobs;

    }

    int getPin1(MatOfPoint2f positions, Vector2[] blobs, ref Point closestBlob)
    {
        Vector2 chessPosition = new Vector2((float)positions.get(0, 0)[0], (float)positions.get(0, 0)[1]);
        int closest = 0;
        float closestDist = (blobs[0] - chessPosition).magnitude;
        for (int i = 0; i < blobs.Length; i++)
        {
            for (int ch = 0; ch < positions.rows(); ch++)
            {

                chessPosition = new Vector2((float)positions.get(ch, 0)[0], (float)positions.get(ch, 0)[1]);
                if ((blobs[i] - chessPosition).magnitude < closestDist)
                {
                    closest = i;
                    closestDist = (blobs[i] - chessPosition).magnitude;
                }

            }
        }
        closestBlob = new Point(blobs[closest].x, blobs[closest].y);

        //find pin1!
        int pin1idx = 0;
        float pin1dist = (blobs[closest] - (new Vector2((float)positions.get(0, 0)[0], (float)positions.get(0, 0)[1]))).magnitude;
        for (int ch = 0; ch < positions.rows(); ch++)
        {
            if (ch == 0 || ch == 6 || ch == 42 || ch == 48)
            {
                float newDist = (blobs[closest] - (new Vector2((float)positions.get(ch, 0)[0], (float)positions.get(ch, 0)[1]))).magnitude;
                if (newDist < pin1dist)
                {
                    pin1idx = ch;
                    pin1dist = newDist;
                }
            }
        }
        //Debug.Log("pin 1 idx: " + pin1idx);
        return pin1idx;
    }

    void getChessBoardWorldPositions(MatOfPoint2f positions, int pin1idx, float distBetweenCorners, ref Point[] realWorldPointArray, ref Point3[] realWorldPointArray3, ref Point[] imagePointArray)
    {
        //i want a list of 2d points and a corresponding list of 3d points:
        //float distBetweenCorners = 0.0498f; //meters

        realWorldPointArray = new Point[positions.rows()];
        realWorldPointArray3 = new Point3[positions.rows()];
        imagePointArray = new Point[positions.rows()];

        for (int i = 0; i < positions.rows(); i++)
        {


            double xp = 0.0;
            double zp = 0.0;
            double yp = 0.0;
            if (pin1idx == 0)
            {
                xp = (i % 7);
                zp = -((int)(i / 7));
            }
            if (pin1idx == 6)
            {
                xp = (i / 7);
                zp = ((int)(i % 7)) - 6;
            }
            if (pin1idx == 42)
            {
                xp = -((int)i / 7) + 6;
                zp = -(i % 7);
            }
            if (pin1idx == 48)
            {
                xp = -(i % 7) + 6;
                zp = ((int)i / 7) - 6;
            }

            xp = xp * distBetweenCorners;
            zp = zp * distBetweenCorners;

            realWorldPointArray[i] = new Point(xp, zp);
            realWorldPointArray3[i] = new Point3(xp, zp, 0.0);
            imagePointArray[i] = new Point(positions.get(i, 0)[0], positions.get(i, 0)[1]);
            //calibPointList2.Add(new Point(positions.get(i, 0)[0], positions.get(i, 0)[1]));
            //calibPointList3.Add(new Point3(xp,0.0,zp));

        }
    }


}
