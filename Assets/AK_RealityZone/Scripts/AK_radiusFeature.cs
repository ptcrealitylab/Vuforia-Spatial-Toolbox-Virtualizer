/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//autocalibration for the azure kinect
//written by Hisham Bedri, Reality Lab, 2019


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AK_radiusFeature : MonoBehaviour {
    [Space(10)]
    [Header("Press this Button To AutoCalibrate!")]
    public bool calibrate = false;
    [Space(10)]

    [Space(10)]
    [Header("Corner Detection Settings:")]
    public int numSpheres = 100; 
    public int iterations = 100;
    public int num_corners_to_extract = 100; //
    public int constellation_size = 0;
    [Space(5)]
    public int search_size = 5;
    public float proximity = 0.4f;
    public float result_multiplier = 1.0f;
    public float radius = 1.0f;
    public float theta = 30.0f;
    public float phi = 30.0f;


    [Space(10)]
    [Header("Corner Description Settings:")]
    public int descriptor_size = 50;
    public int descriptor_width = 10;
    public int descriptor_height = 5;
    [Space(2)]
    public float descriptor_proximity;
    public int descriptor_search_size;
    public float descriptor_min_dist;
    public float descriptor_max_dist;
    public float descriptor_min_dot;
    public float descriptor_max_dot;

    [Space(2)]
    public float feature_max = 1.0f;
    public enum feature_visualization_enum
    {
        spin_histogram,
        curvature,
        dotpitch,
        color,
        column
    }
    public feature_visualization_enum feature_to_visualize = feature_visualization_enum.spin_histogram;
    feature_visualization_enum last_feature_to_visualize = feature_visualization_enum.spin_histogram;


    [Space(10)]
    [Header("Feature Filtering Settings")]
    public float heightDiffThreshold = 0.1f; //difference in hegiht between two features where we say, they are probably not a match
    public float feature_dist_threshold = 0.1f; //maximum distance between the feature vectors, the rest are dropped
    public float min_match_height = 0.2f; //minimum level off the floor where we consider a feature, here its 20cm
    public bool debugMode = false; //dont touch this haha.

    [Space(10)]
    [Header("RANSAC Matching Settings")]
    public float RANSAC_inlier_threshold = 0.2f;
    public int RANSAC_iterations = 100;
    public int depthSampleSize = 10;
    public bool denseRansacVizualization = false;





    [Space(10)]
    [Header("For testing calibration between two cameras:")]
    public bool icpButton = false;
    public bool getCornersButton = false;
    public int startCamera = 0;
    public int stopCamera = 1;



    [Space(10)]
    [Header("Post Calibration ICP for minor adjustments")]

    [Space(10)]
    public float ICP_period = 0.2f; //time between bursts
    public int num_ICP_bursts = 10; //number of total bursts
    public int icp_iterations = 10;

    public int icp_super_iterations = 1;
    public float icp_neighbor_thresh = 0.1f; //this gets overwritten and unused during icp annealing in calibrateFull();

    public float icp_sample_radius = 3.0f;
    public float icp_result_scale = 0.1f;
    public float min_icp_height = 0.1f;


    [Space(10)]
    [Header("Floor Estimation Settings")]
    public int floorSampleSize = 300;
    public int floorRansacIterations = 300;
    public float floorInlierThreshold = 0.001f;
    public float floorOutlierThreshold = 0.01f;



    [Space(10)]
    [Header("Normal estimation settings")]
    public float normal_multiplier = 1.0f;
    public int normal_filter_size = 5;




    //buttons:
    public bool placeSpheresButton = false;
    public bool walkSpheresButton = false;
    public int num_matches = 100; //unnecessary for feature matching at this point, too scared to delete haha



    [Space(10)]

    //settings:

    public float sphere_size = 0.01f;
    public float icp_sphere_size = 0.05f;


    [Space(10)]


    //references
    public GameObject AK_receiver;
    public ComputeShader radiusFeatureDetectorCompute;
    public ComputeShader cornerDetectorCompute;
    List<akplay.camInfo> camInfoList;
    public List<GameObject> result_display_list = new List<GameObject>();

    public List<List<sphereStruct>> sphereList = new List<List<sphereStruct>>();
    public GameObject laserPrefab;

    public GameObject keyHolder;
    public GameObject inlierHolder;
    public GameObject outlierHolder;

    public GameObject calibrationSaverAndLoader;


    [Space(10)]

    //internal:
    bool camerasReady = false;
    ComputeBuffer camInfoBuffer;
    RenderTexture[] resultTexture;
    RenderTexture[] cornerTexture;
    RenderTexture[] normalTexture;
    RenderTexture[] descriptorTexture;
    EstimateBiggestPlane.planeData[] floorInfo;
    float max_corner_score = 0.0f;


    //one array of x pixels per camera, 
    //the algorithm will fill the buffer with the best 100 corners. 
    //it'll leave the rest -1 if there are less than 100 corners
    List<int[]> corner_x_pixels = new List<int[]>();
    List<int[]> corner_y_pixels = new List<int[]>();
    List<float[]> corner_scores = new List<float[]>();
    List<Vector3[]> corner_points = new List<Vector3[]>();
    List<List<float[]>> corner_descriptions = new List<List<float[]>>(); //ugh i know that's ugly. fight me.

    public struct corner_struct
    {
        public int x_pixel;
        public int y_pixel;
        public float score;
        public Vector3 point;
        public float[] description;
        public GameObject sphere;
        public float[] descriptionArray;
        public GameObject descriptionSquare;
        public Texture2D descriptionTex;
        public float curvature;
        public float dotpitch;
        public float color;
        public float column;
    }

    List<corner_struct[]> corner_info_list = new List<corner_struct[]>();
    //List<match_struct> matches = new List<match_struct>();

    List<GameObject> laserList = new List<GameObject>();


    public struct sphereStruct
    {
        public GameObject startSphere;
        public GameObject currentSphere;
        public Vector2 startPixel;
        public Vector2 currentPixel;
        public GameObject sphere;
    }

    public struct match_struct
    {
        public int start_camera;
        public int stop_camera;
        public int start_corner_idx;
        public int stop_corner_idx;
        public corner_struct start_corner;
        public corner_struct stop_corner;
        public float value;
        public GameObject laser;
    }





    struct infoStruct
    {
        public Matrix4x4 depthCameraToWorld;
        public Matrix4x4 worldToDepthCamera;

        public float camera_x;
        public float camera_y;
        public float camera_z;
        public float cameraActive;

        public float color_cx;
        public float color_cy;
        public float color_fx;
        public float color_fy;

        public float color_k1;
        public float color_k2;
        public float color_k3;
        public float color_k4;
        public float color_k5;
        public float color_k6;

        public float color_codx;
        public float color_cody;
        public float color_p1;
        public float color_p2;
        public float color_radius;

        public Matrix4x4 color_extrinsic;


        public float depth_cx;
        public float depth_cy;
        public float depth_fx;
        public float depth_fy;

        public float depth_k1;
        public float depth_k2;
        public float depth_k3;
        public float depth_k4;
        public float depth_k5;
        public float depth_k6;

        public float depth_codx;
        public float depth_cody;
        public float depth_p1;
        public float depth_p2;
        public float depth_radius;
    }

    //Unity Timeline Functions:
    #region


    // Use this for initialization
    void Start() {

        //test rotation stuff:
        Quaternion Q1 = Quaternion.AngleAxis(Random.value * 360.0f, new Vector3(Random.value, Random.value, Random.value));
        Quaternion Q2 = Quaternion.AngleAxis(Random.value * 360.0f, new Vector3(Random.value, Random.value, Random.value));

        Vector3 T1 = new Vector3(Random.value, Random.value, Random.value);
        Vector3 T2 = new Vector3(Random.value, Random.value, Random.value);

        Quaternion Qf = Q2 * Quaternion.Inverse(Q1);
        Vector3 Tf = -(Q2 * Quaternion.Inverse(Q1) * T1) + T2;

        Debug.Log("Q2: " + Q2);
        Debug.Log("T2: " + T2);

        Debug.Log("Q1F: " + Qf * Q1);
        Debug.Log("T1F: " + (Qf * T1 + Tf));





        //test graph:
        //Debug.Log("testing graph!!!");

        /*
        Graph.initialize(6);
        Graph.addDistance(0, 1, 15.0f);
        Graph.addDistance(1, 2, 10.0f);
        Graph.addDistance(2, 3, 10.0f);
        Graph.addDistance(0, 4, 1.0f);
        Graph.addDistance(4, 3, 50.0f);
        Graph.addDistance(4, 5, 1.0f);

        Graph.addDistance(0, 5, 22.0f);
        Graph.addDistance(5, 2, 2.0f);

        float finalDist = 0.0f;
        List<int> path = new List<int>();
        bool foundAWay = false;
        Graph.getShortestDistance(0, 3, new List<int>(), out finalDist, out path, out foundAWay);

        Debug.Log("graph test:");
        Debug.Log("shortest dist: " + finalDist);
        Debug.Log("found a way: " + foundAWay);
        string output = "";
        for (int i = 0; i<path.Count; i++)
        {
            output += "" + path[i] + ",";
        }

        Debug.Log("path is: [" + output + "]");
        */


    }



    bool ICP_annealing = false;
    float last_anneal_time = 0.0f;
    int ICP_burst_count = 0;




    // Update is called once per frame
    void Update()
    {
        //check if cameras are ready:
        if (!camerasReady && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            camerasReady = true;
            do_setup(); //i think setup() is Unity reserved.
        }

        if (camerasReady)
        {
            //run feature detector:
            //getFeatures();

            if (getCornersButton)
            {
                getCornersButton = false;
                getCorners();
            }

            if (feature_to_visualize != last_feature_to_visualize)
            {
                updateCornerVisualization();
                last_feature_to_visualize = feature_to_visualize;
            }


            if (icpButton)
            {
                icpButton = false;
                for (int i = 0; i < icp_super_iterations; i++)
                {
                    int overlap = 0;
                    runICP(startCamera, stopCamera, true, out overlap);
                }
                debugRenderTex = ((RenderTexture)debugCube.GetComponent<Renderer>().material.mainTexture);
            }

            if (calibrate)
            {
                calibrate = false;
                calibrateFull();
                ICP_annealing = true;
                ICP_burst_count = 0;

                for(int cc = 0; cc<cornerHolder.Count; cc++)
                {
                    cornerHolder[cc].SetActive(false);
                }
                for(int cc = 0; cc<floorInfo.Length; cc++)
                {
                    floorInfo[cc].floorObject.SetActive(false);
                }
            }

        }

        
        if (ICP_annealing)
        {

            float last_depth_sample_size = depthSampleSize;
            depthSampleSize = 50;
            if(Time.time - last_anneal_time > ICP_period)
            {
                last_anneal_time = Time.time;
                if(num_ICP_bursts < ICP_burst_count / 2)
                {
                    for (int cc = 1; cc < camInfoList.Count; cc++)
                    {
                        int overlap = 0;
                        icp_neighbor_thresh = 0.05f;
                        runICP(0, cc, true, out overlap);
                    }
                    ICP_burst_count++;
                }
                else
                {
                    for (int cc = 1; cc < camInfoList.Count; cc++)
                    {
                        icp_neighbor_thresh = 0.05f;
                        int overlap = 0;
                        runICP(0, cc, true, out overlap);
                    }
                    ICP_burst_count++;
                }

                if(ICP_burst_count > num_ICP_bursts)
                {
                    ICP_annealing = false;
                }

            }
            depthSampleSize = (int)last_depth_sample_size;

            calibrationSaverAndLoader.GetComponent<CalibrationLoaderAndSaver>().saveCalibration();
        }
        






        if (placeSpheresButton || Input.GetKeyDown(KeyCode.O))
        {
            //place some spheres on the pointclouds.
            placeSpheresButton = false;
            placeSpheres();

        }

        if (walkSpheresButton || Input.GetKeyDown(KeyCode.P))
        {
            walkSpheresButton = false;
            //walk the spheres on the pointclouds
            for (int i = 0; i < iterations; i++)
            {
                walkSpheres();
            }
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("on application quit!");
        takeDown();
    }

    void takeDown()
    {
        if (camInfoBuffer != null)
        {
            //Debug.Log("disposing of cam info buffer because its not null");
            camInfoBuffer.Dispose();
        }
    }
    #endregion

    //these two functions I wrote to debug stuff / explore. they are unnecessary in the production pipeline:
    void placeSpheres()
    {
        for (int i = 0; i < sphereList.Count; i++)
        {
            for (int j = 0; j < sphereList[i].Count; j++)
            {
                GameObject.Destroy(sphereList[i][j].startSphere);
                GameObject.Destroy(sphereList[i][j].currentSphere);
            }
            sphereList[i].Clear();
        }
        sphereList.Clear();

        for (int cc = 0; cc < camInfoList.Count; cc++)
        {


            byte[] depthTexBytes = camInfoList[cc].depthTex.GetRawTextureData();
            byte[] distortionTexBytes = camInfoList[cc].distortionMapTex.GetRawTextureData();

            List<sphereStruct> sphereListForCamera = new List<sphereStruct>();
            for (int i = 0; i < numSpheres; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(sphere_size, sphere_size, sphere_size);
                sphere.GetComponent<Renderer>().material.color = Color.red;
                //sphere.transform.parent = gameObject.transform;


                //put it in the right spot:
                int random_x = (int)(Random.value * (float)camInfoList[cc].depth_width);
                int random_y = (int)(Random.value * (float)camInfoList[cc].depth_height);

                sphere.transform.parent = camInfoList[cc].visualization.transform;
                float depth = 0.0f;
                sphere.transform.localPosition = getPoint(random_x, random_y, camInfoList[cc].depth_width, camInfoList[cc].depth_height, ref depthTexBytes, ref distortionTexBytes, out depth);


                GameObject sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere2.transform.localScale = new Vector3(sphere_size, sphere_size, sphere_size);
                sphere2.GetComponent<Renderer>().material.color = Color.blue;
                sphere2.transform.parent = camInfoList[cc].visualization.transform;
                sphere2.transform.localPosition = sphere.transform.localPosition;
                sphere2.SetActive(false); //will activate once we start walking.


                sphereStruct sphereInfo = new sphereStruct();
                sphereInfo.startSphere = sphere;
                sphereInfo.currentSphere = sphere2;
                sphereInfo.startPixel = new Vector2(random_x, random_y);
                sphereInfo.currentPixel = new Vector2(random_x, random_y);


                sphereListForCamera.Add(sphereInfo);
            }
            sphereList.Add(sphereListForCamera);
        }
    }

    void walkSpheres()
    {
        for (int cc = 0; cc < sphereList.Count; cc++)
        {
            byte[] depthTexBytes = camInfoList[cc].depthTex.GetRawTextureData();
            byte[] distortionTexBytes = camInfoList[cc].distortionMapTex.GetRawTextureData();

            for (int ss = 0; ss < sphereList[cc].Count; ss++)
            {
                sphereList[cc][ss].currentSphere.SetActive(true);


                List<Vector2> gradientPixelArray = new List<Vector2>(); //each has: x-pixel, y-pixel
                List<Vector3> gradientPointArray = new List<Vector3>();
                List<float> gradientValueArray = new List<float>();

                int idx = 0;
                //check the middle and the 4 directions:
                for (int xx = -1; xx < 2; xx++)
                {
                    for (int yy = -1; yy < 2; yy++)
                    {
                        if (xx != 0 && yy != 0)
                        {
                            if ((sphereList[cc][ss].currentPixel.x + xx) >= 0
                                && (sphereList[cc][ss].currentPixel.y + yy) >= 0
                                && (sphereList[cc][ss].currentPixel.x + xx) < camInfoList[cc].depth_width
                                && (sphereList[cc][ss].currentPixel.y + yy) < camInfoList[cc].depth_height)
                            {
                                Vector2 newPixel = new Vector2(sphereList[cc][ss].currentPixel.x + xx, sphereList[cc][ss].currentPixel.y + yy);
                                float depth = 0.0f;
                                Vector3 point = getPoint((int)newPixel.x, (int)newPixel.y, camInfoList[cc].depth_width, camInfoList[cc].depth_height, ref depthTexBytes, ref distortionTexBytes, out depth);
                                if (depth > 0.0f)
                                {
                                    gradientPixelArray.Add(newPixel);
                                    gradientPointArray.Add(point);
                                    gradientValueArray.Add(point.magnitude);
                                }
                            }
                        }
                    }
                }

                //find the pixel that minimizes the gradient!
                int minIdx = findMinIdxFloatArray(gradientValueArray.ToArray());

                /*
                if(cc == 0)
                {
                    Debug.Log("Current pixel: " + sphereList[cc][ss].currentPixel);
                    for (int i = 0; i < gradientPixelArray.Count; i++)
                    {
                        Debug.Log("pixel " + i + ": " + gradientPixelArray[i].x + "," + gradientPixelArray[i].y + "-[" + gradientValueArray[i] + "]");
                    }
                    Debug.Log("min idx: " + minIdx);

                }
                */


                if (minIdx >= 0)
                {
                    sphereStruct sphereInfo = sphereList[cc][ss];
                    sphereInfo.currentPixel = gradientPixelArray[minIdx];
                    sphereInfo.currentSphere.transform.localPosition = gradientPointArray[minIdx];
                    sphereList[cc][ss] = sphereInfo;
                }


            }
        }
    }


    //this stuff is super necessary tho!
    int findMinIdxFloatArray(float[] arr)
    {
        int idx = -1;
        float smallest = 0.0f;
        for (int i = 0; i < arr.Length; i++) {
            if (arr[i] < smallest || idx < 0)
            {
                smallest = arr[i];
                idx = i;
            }
        }
        return idx;
    }


    Vector3 getPoint(int px, int py, int width, int height, ref byte[] depthTexBytes, ref byte[] distortionTexBytes, out float depth)
    {
        Vector3 point = new Vector3(0.0f, 0.0f, 0.0f);

        int idx = py * width + px;

        byte lsb = depthTexBytes[2 * idx + 0];
        byte msb = depthTexBytes[2 * idx + 1];
        byte[] values = new byte[2];
        values[0] = lsb;
        values[1] = msb;
        depth = (float)System.BitConverter.ToUInt16(values, 0);
        //depth = depth*65.536f;
        depth = depth / 1000.0f;

        byte[] fvalues = new byte[4];

        fvalues[0] = distortionTexBytes[8 * idx + 0];
        fvalues[1] = distortionTexBytes[8 * idx + 1];
        fvalues[2] = distortionTexBytes[8 * idx + 2];
        fvalues[3] = distortionTexBytes[8 * idx + 3];
        //System.Array.Reverse(fvalues);
        float x_distortion = (float)System.BitConverter.ToSingle(fvalues, 0);

        fvalues[0] = distortionTexBytes[8 * idx + 4];
        fvalues[1] = distortionTexBytes[8 * idx + 5];
        fvalues[2] = distortionTexBytes[8 * idx + 6];
        fvalues[3] = distortionTexBytes[8 * idx + 7];
        //System.Array.Reverse(fvalues);
        float y_distortion = (float)System.BitConverter.ToSingle(fvalues, 0);

        point.x = depth * x_distortion;
        point.y = depth * -y_distortion;
        point.z = depth;

        return point;
    }


    void do_setup()
    {

        float delta = 0.15f;
        for (int i = 0; i < AK_receiver.GetComponent<akplay>().camInfoList.Count; i++)
        {
            GameObject result_display = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result_display.layer = LayerMask.NameToLayer("Debug");
            result_display.name = "threshold_" + i;
            result_display.transform.parent = gameObject.transform;
            result_display.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            result_display.transform.localPosition = new Vector3(0.0f, -delta * i, 0.0f);
            //result_display.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            result_display_list.Add(result_display);
        }

        resultTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];
        cornerTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];
        normalTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];
        descriptorTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];
        floorInfo = new EstimateBiggestPlane.planeData[AK_receiver.GetComponent<akplay>().camInfoList.Count];

        camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;

        //Debug.Log("setting up cam info list count: " + camInfoList.Count);
        //camInfoBuffer = new ComputeBuffer(camInfoList.Count, 268);  //3 matrices and 19 floats: 3*64 + 19*4 = 268
        camInfoBuffer = new ComputeBuffer(camInfoList.Count, 328);  //3 matrices and 19 floats: 3*64 + 34*4 = 328
        getCameraInfo();
    }


    void calibrateFull()
    {
        tic();
        clearCornerInfo();
        //Debug.Log("clear corner Info took: " + toc() + " ms");

        getFloorPlanes();
        //Debug.Log("get floor planes took: " + toc() + " ms");

        //outputs a corner detected image: which has hotspots near corners in the depth map
        //these are saved in cornerTexture renderTexture array (global)
        getNormalTexture();
        //Debug.Log("get normal texture took: " + toc() + " ms");
        getCornerTexture();
        //Debug.Log("get corner texture took: " + toc() + " ms");

        //extract corner points:
        extractCornerPoints();
        //Debug.Log("extract corner points took: " + toc() + " ms");

        placeCornerPoints();
        //Debug.Log("place corner points took: " + toc() + " ms");

        describeCornerPoints();
        //Debug.Log("describe corner points took: " + toc() + " ms");

        updateCornerVisualization();
        //Debug.Log("update corner visualization took: " + toc() + " ms");

        if (num_matches > num_corners_to_extract)
        {
            num_matches = num_corners_to_extract;
        }


        float[,] overlapBetweenCameras = new float[camInfoList.Count, camInfoList.Count];
        Quaternion[,] rotationsBetweenCameras = new Quaternion[camInfoList.Count, camInfoList.Count];
        Vector3[,] translationBetweenCameras = new Vector3[camInfoList.Count, camInfoList.Count];

        Graph.initialize(camInfoList.Count);
        bool[,] pairs = new bool[camInfoList.Count, camInfoList.Count];
        for (int i = 0; i < camInfoList.Count; i++)
        {
            for (int j = 0; j < camInfoList.Count; j++)
            {
                if (i == j)
                {
                    pairs[i, j] = true; //signify that we dont need to do a correlation between a camera and itself.
                }
                else
                {
                    pairs[i, j] = false;
                }

            }
        }


        Quaternion cam0_Q = camInfoList[0].visualization.transform.rotation;
        Vector3 cam0_T = camInfoList[0].visualization.transform.position;

        Quaternion[] q_array = new Quaternion[camInfoList.Count];
        Vector3[] t_array = new Vector3[camInfoList.Count];

        for (int cc = 0; cc < camInfoList.Count; cc++)
        {
            q_array[cc] = camInfoList[cc].visualization.transform.rotation;
            t_array[cc] = camInfoList[cc].visualization.transform.position;
        }



        for (int start_camera_idx = 0; start_camera_idx < camInfoList.Count; start_camera_idx++)
        {
            for (int stop_camera_idx = 0; stop_camera_idx < camInfoList.Count; stop_camera_idx++)
            {
                if (start_camera_idx != stop_camera_idx)
                {
                    if (!pairs[start_camera_idx, stop_camera_idx])
                    //if (!pairs[start_camera_idx, stop_camera_idx] && start_camera_idx == 1 && stop_camera_idx == 2)
                    {
                        pairs[start_camera_idx, stop_camera_idx] = true;
                        pairs[stop_camera_idx, start_camera_idx] = true;

                        camInfoList[start_camera_idx].visualization.transform.rotation = q_array[start_camera_idx];
                        camInfoList[start_camera_idx].visualization.transform.position = t_array[start_camera_idx];

                        camInfoList[stop_camera_idx].visualization.transform.rotation = q_array[stop_camera_idx];
                        camInfoList[stop_camera_idx].visualization.transform.position = t_array[stop_camera_idx];



                        Debug.Log("doing correlation between camera: " + start_camera_idx + " and " + stop_camera_idx);

                        int start = start_camera_idx;
                        int stop = stop_camera_idx;

                        //Quaternion Q_start_stop = Quaternion.identity;
                        //Vector3 T_start_stop = new Vector3();
                        float overlap = 0.0f;
                        // forward equation:
                        // Q_start_stop * start + T_start_stop = stop

                        //Quaternion original_stop_Q = camInfoList[stop].visualization.transform.rotation;
                        //Vector3 original_stop_T = camInfoList[stop].visualization.transform.position;

                        //getFloorPlanes();

                        //camInfoList[start_camera_idx].visualization.transform.rotation = cam0_Q;
                        //camInfoList[start_camera_idx].visualization.transform.position = cam0_T;




                        tic();
                        List<match_struct> matches = getHeightMatches(start, stop);
                        //Debug.Log("get height matches took: " + toc() + " ms");

                        drawMatches(matches);
                        //Debug.Log("draw matches took: " + toc() + " ms");


                        if (matches.Count > 4)
                        {

                            //Debug.Log("NUMBER OF MATCHES: " + matches.Count);
                            tic();
                            Quaternion recoveredQ = new Quaternion();
                            Vector3 recoveredT = new Vector3();
                            //matches = filterMatchesWithRansac(matches, out recoveredQ, out recoveredT);
                            matches = filterMatchesWithRansacDense(matches, out recoveredQ, out recoveredT);
                            //Debug.Log("ransac filter took: " + toc() + " ms");
                            //Debug.Log("NUMBER OF MATCHES AFTER RANSAC: " + matches.Count);

                            //Debug.Log("viz 1 rotation before: " + camInfoList[1].visualization.transform.rotation);

                            Quaternion Q1 = camInfoList[start].visualization.transform.rotation;
                            Quaternion QR = recoveredQ;
                            Quaternion Q2 = camInfoList[stop].visualization.transform.rotation;

                            Quaternion Q1P = Quaternion.Inverse(Q1);
                            Quaternion QRP = Quaternion.Inverse(QR);
                            Quaternion Q2P = Quaternion.Inverse(Q2);

                            Vector3 T1 = camInfoList[start].visualization.transform.position;
                            Vector3 T2 = camInfoList[stop].visualization.transform.position;
                            Vector3 TR = recoveredT;

                            if (transformMatches)
                            {
                                //Debug.Log("Transforming camera 1 position/rotation!");
                                //Debug.Log("viz stop rotation before: " + camInfoList[stop].visualization.transform.rotation);
                                if (RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.three_d)
                                {
                                    camInfoList[stop].visualization.transform.rotation = Q1 * QRP;
                                    camInfoList[stop].visualization.transform.position = Q1 * QRP * ((QR * Q1P * T1) - TR);
                                }
                                if (RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.in_plane)
                                {
                                    camInfoList[stop].visualization.transform.rotation = QRP * Q2;
                                    camInfoList[stop].visualization.transform.position = QRP * (T2 - TR);
                                }

                                //Debug.Log("viz stop rotation after: " + camInfoList[stop].visualization.transform.rotation);

                            }


                            //Debug.Log("** cam rotation: ****");
                            float angle = 0.0f;
                            Vector3 axis = new Vector3();
                            camInfoList[stop].visualization.transform.rotation.ToAngleAxis(out angle, out axis);



                            //overlap = 100;

                            tic();
                            /*
                            icp_neighbor_thresh = 0.1f;
                            for (int i = 0; i < icp_super_iterations; i++)
                            {
                                overlap = 0;
                                int toverlap = 0;
                                runICP(start, stop, out toverlap);
                                overlap = toverlap;
                            }
                            */
                            icp_neighbor_thresh = 0.05f;
                            for (int i = 0; i < icp_super_iterations; i++)
                            {
                                overlap = 0;
                                int toverlap = 0;
                                runICP(start, stop, false, out toverlap);
                                overlap = toverlap;
                            }
                            //Debug.Log("icp super iterations took: " + toc() + " ms");



                        }
                        else
                        {
                            Debug.Log("NUMBER OF MATCHES TOO SMALL!: " + matches.Count);
                        }


                        Quaternion Q_start_stop = Quaternion.identity;
                        Quaternion Q_stop_start = Quaternion.identity;
                        Vector3 T_start_stop = new Vector3();
                        Vector3 T_stop_start = new Vector3();

                        Quaternion start_q = camInfoList[start_camera_idx].visualization.transform.rotation;
                        Quaternion stop_q = camInfoList[stop_camera_idx].visualization.transform.rotation;

                        Vector3 start_t = camInfoList[start_camera_idx].visualization.transform.position;
                        Vector3 stop_t = camInfoList[stop_camera_idx].visualization.transform.position;


                        getTransform(start_q, stop_q, start_t, stop_t, out Q_start_stop, out T_start_stop);
                        getTransform(stop_q, start_q, stop_t, start_t, out Q_stop_start, out T_stop_start);

                        //overlap = Mathf.Pow((1000.0f - overlap), 2.0f);

                        Debug.Log("overlap between " + start_camera_idx + " and " + stop_camera_idx + ": " + overlap);
                        //overlapBetweenCameras[start_camera_idx, stop_camera_idx] = 1.0f / ((float)overlap);
                        //overlapBetweenCameras[stop_camera_idx, start_camera_idx] = 1.0f / ((float)overlap);
                        overlapBetweenCameras[start_camera_idx, stop_camera_idx] = overlap;
                        overlapBetweenCameras[stop_camera_idx, start_camera_idx] = overlap;
                        rotationsBetweenCameras[start_camera_idx, stop_camera_idx] = Q_start_stop;
                        rotationsBetweenCameras[stop_camera_idx, start_camera_idx] = Q_stop_start;
                        translationBetweenCameras[start_camera_idx, stop_camera_idx] = T_start_stop;
                        translationBetweenCameras[stop_camera_idx, start_camera_idx] = T_stop_start;


                        Graph.addDistance(start_camera_idx, stop_camera_idx, overlap);
                        //Graph.addDistance(stop_camera_idx, start_camera_idx, 1000.0f - overlap);

                    }
                }

            }
        }


        /*
        GameObject go2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go2.transform.rotation = cam0_Q;
        go2.transform.position = cam0_T;
        go2.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
        go2.name = "transform_" + 0 + "_pathnum_" + 0;



        for (int cc = 1; cc < camInfoList.Count; cc++)
        {
            GameObject go4 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go4.name = "transform_" + 0 + "_pathnum_" + cc;
            for (int pp = 1; pp < cc; pp++)
            {
                Vector3 temp = go4.transform.position;
                go4.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
                go4.transform.rotation = rotationsBetweenCameras[pp-1, pp] * go4.transform.rotation;
                go4.transform.position = temp + translationBetweenCameras[pp - 1, pp];

            }
            go4.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);

        }
        */






        for (int i = 0; i < camInfoList.Count; i++)
        {
            if (i == 0)
            {
                camInfoList[0].visualization.transform.rotation = cam0_Q;
                camInfoList[0].visualization.transform.position = cam0_T;
            }
            else
            {
                float dist = 0.0f;
                List<int> path = new List<int>();
                bool foundDest = false;
                foundDest = true;
                //Graph.getShortestDistance(i, 0, new List<int>(), out dist, out path, out foundDest);


                //find closest next one:
                int closest_camera = -1;
                float best_overlap = 0.0f;
                for (int ss = 0; ss < camInfoList.Count; ss++)
                {
                    if (ss != i)
                    {
                        if (overlapBetweenCameras[i, ss] > best_overlap || closest_camera < 0)
                        {
                            best_overlap = overlapBetweenCameras[i, ss];
                            closest_camera = ss;
                        }
                    }
                }

                path.Add(0);
                if (closest_camera != 0 && closest_camera >= 0)
                {
                    path.Add(closest_camera);
                    path.Add(i);
                }
                else
                {
                    path.Add(i);
                }




                /*
                if (i == 2)
                {
                    path = new List<int>();
                    path.Add(0);
                    path.Add(1);
                    path.Add(2);
                }
                */



                if (!foundDest)
                {
                    //sorry to be dramatic.
                    Debug.Log("SOMETHING IS SERIOUSLY WRONG WITH CAMERA " + i + ": I CANT FIND A VALID GRAPH PATH TO CAM 0 FROM IT");
                }
                else
                {
                    Debug.Log("Path for camera " + i + ": " + Graph.intListToString(path));
                    //follow the path:
                    /*
                    Quaternion Qs = cam0_Q;
                    Vector3 Ts = cam0_T;
                    for(int pp = 1; pp<path.Count; pp++)
                    {
                        Qs = rotationsBetweenCameras[0, path[pp]] * Qs;
                        Ts = translationBetweenCameras[0, path[pp]] + Ts;
                    }
                    */

                    camInfoList[i].visualization.transform.rotation = cam0_Q;
                    camInfoList[i].visualization.transform.position = cam0_T;

                    /*
                    GameObject go3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go3.transform.rotation = camInfoList[i].visualization.transform.rotation;
                    go3.transform.position = camInfoList[i].visualization.transform.position;
                    go3.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
                    go3.name = "transform_" + i + "_pathidx_" + 0 + "_pathnum_" + 0;
                    */

                    for (int pp = 1; pp < path.Count; pp++)
                    {
                        /*
                        if(i == 2)
                        {
                            Debug.Log("going from " + path[pp - 1] + " to " + path[pp]);
                            Debug.Log("translating by: " + camInfoList[i].visualization.transform.position);
                            Debug.Log("rotating by: " + rotationsBetweenCameras[path[pp - 1], path[pp]]);
                            Debug.Log("translating back to: " + (camInfoList[i].visualization.transform.position + translationBetweenCameras[path[pp - 1], path[pp]]));
                        }
                        */




                        //Vector3 temp = camInfoList[i].visualization.transform.position;
                        //camInfoList[i].visualization.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
                        Quaternion Qe = rotationsBetweenCameras[path[pp - 1], path[pp]];
                        Vector3 Te = translationBetweenCameras[path[pp - 1], path[pp]];

                        Te = camInfoList[i].visualization.transform.rotation * Te;
                        camInfoList[i].visualization.transform.rotation = camInfoList[i].visualization.transform.rotation * Qe;
                        camInfoList[i].visualization.transform.position = Te + camInfoList[i].visualization.transform.position;

                        //camInfoList[i].visualization.transform.rotation = rotationsBetweenCameras[path[pp-1], path[pp]] * camInfoList[i].visualization.transform.rotation;
                        //camInfoList[i].visualization.transform.position = temp + translationBetweenCameras[path[pp - 1], path[pp]];

                        //if (i == 2)
                        // {
                        /*
                             GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                             go.transform.rotation = camInfoList[i].visualization.transform.rotation;
                             go.transform.position = camInfoList[i].visualization.transform.position;
                             go.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
                             go.name = "transform_" + i + "_pathidx_" + pp + "_pathnum_" + path[pp];
                             */

                        //}

                    }

                    //camInfoList[i].visualization.transform.rotation = Qs;
                    //camInfoList[i].visualization.transform.position = Ts;
                }


            }
        }


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





    [Space(10)]







    

    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
    void tic()
    {
        sw.Reset();
        sw.Start();
    }

    float toc()
    {
        float elapsedMS = 0.0f;
        sw.Stop();
        elapsedMS = ((float)sw.ElapsedTicks) / ((float)System.TimeSpan.TicksPerMillisecond);
        sw.Reset();
        sw.Start();
        return elapsedMS;
    }



    public bool transformMatches = false;
    void getCorners()
    {
        tic();
        clearCornerInfo();
        Debug.Log("clear corner Info took: " + toc() + " ms");

        getFloorPlanes();
        Debug.Log("get floor planes took: " + toc() + " ms");

        //outputs a corner detected image: which has hotspots near corners in the depth map
        //these are saved in cornerTexture renderTexture array (global)
        getNormalTexture();
        Debug.Log("get normal texture took: " + toc() + " ms");
        getCornerTexture();
        Debug.Log("get corner texture took: " + toc() + " ms");

        //extract corner points:
        extractCornerPoints();
        Debug.Log("extract corner points took: " + toc() + " ms");

        placeCornerPoints();
        Debug.Log("place corner points took: " + toc() + " ms");

        describeCornerPoints();
        Debug.Log("describe corner points took: " + toc() + " ms");

        updateCornerVisualization();
        Debug.Log("update corner visualization took: " + toc() + " ms");

        if (num_matches > num_corners_to_extract)
        {
            num_matches = num_corners_to_extract;
        }


        int start = startCamera;
        int stop = stopCamera;

        for (int cc = 0; cc < camInfoList.Count; cc++)
        {
            if (cc != start && cc != stop)
            {
                camInfoList[cc].visualization.SetActive(false);
            }
            else
            {
                camInfoList[cc].visualization.SetActive(true);
            }
        }

        if (debugMode)
        {
            //camInfoList[1].visualization.transform.position = camInfoList[0].visualization.transform.position;
            //camInfoList[1].visualization.transform.rotation = camInfoList[0].visualization.transform.rotation;

            //Quaternion testQ = Quaternion.AngleAxis(60.0f, new Vector3(0.0f, 1.0f, 0.0f));
            Quaternion testQ = Quaternion.AngleAxis(Random.value * 360.0f, new Vector3(Random.value, Random.value, Random.value));
            //Vector3 testT = new Vector3(4.0f, 0.0f, 0.0f);
            Vector3 testT = new Vector3(Random.value, Random.value, Random.value);

            Quaternion badQ = Quaternion.AngleAxis(60.0f, new Vector3(0.0f, 1.0f, 0.0f));
            Vector3 badT = new Vector3(-1.0f, 0.0f, -3.0f);



            for (int i = 0; i < corner_info_list[1].Length; i++)
            {
                //Vector3 startPoint = corner_info_list[0][i].sphere.transform.localPosition;
                Vector3 startPoint = corner_info_list[start][i].sphere.transform.position;
                Vector3 stopPoint = startPoint;

                if (Random.value < 0.0f)
                {
                    stopPoint = badQ * startPoint;
                    stopPoint = stopPoint + badT;
                }
                else
                {
                    stopPoint = testQ * startPoint;
                    stopPoint = stopPoint + testT;

                }
                //corner_info_list[1][i].sphere.transform.localPosition = stopPoint;
                corner_info_list[stop][i].sphere.transform.position = stopPoint;
                corner_info_list[stop][i].point = corner_info_list[stop][i].sphere.transform.localPosition;



            }



            Vector3 prevScale = floorInfo[stop].floorObject.transform.localScale;
            floorInfo[stop].floorObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            floorInfo[stop].floorObject.transform.rotation = testQ * floorInfo[0].floorObject.transform.rotation;
            floorInfo[stop].floorObject.transform.localScale = prevScale;
            floorInfo[stop].floorObject.transform.position = floorInfo[stop].floorObject.transform.position + testT;


            GameObject dummy = new GameObject();

            floorInfo[stop].point = floorInfo[stop].floorObject.transform.localPosition;

            dummy.transform.parent = camInfoList[stop].visualization.transform;
            dummy.transform.position = floorInfo[stop].floorObject.transform.position + floorInfo[stop].floorObject.transform.up;
            floorInfo[stop].normal = (dummy.transform.localPosition - floorInfo[stop].floorObject.transform.localPosition).normalized;
            floorInfo[stop].floorObject.name = "floor_" + stop + "_" + floorInfo[stop].normal.x
                                                                + "_" + floorInfo[stop].normal.y
                                                                + "_" + floorInfo[stop].normal.z;
            //floorInfo[1].normal = new Vector3(0.0f, 1.0f, 0.0f);

            /*
            floorInfo[1].point = testQ * floorInfo[0].point;
            floorInfo[1].point = floorInfo[0].point + testT;
            floorInfo[1].normal = testQ * floorInfo[0].normal;
            */

            floorInfo[start].point = floorInfo[start].floorObject.transform.localPosition;

            dummy.transform.parent = camInfoList[start].visualization.transform;
            dummy.transform.position = floorInfo[start].floorObject.transform.position + floorInfo[start].floorObject.transform.up;
            floorInfo[start].normal = (dummy.transform.localPosition - floorInfo[start].floorObject.transform.localPosition).normalized;
            floorInfo[start].floorObject.name = "floor_" + start + "_" + floorInfo[start].normal.x
                                                                + "_" + floorInfo[start].normal.y
                                                                + "_" + floorInfo[start].normal.z;
            //floorInfo[0].normal = new Vector3(0.0f, 1.0f, 0.0f);




            GameObject.Destroy(dummy);
        }


        tic();
        List<match_struct> matches = getHeightMatches(start, stop);
        Debug.Log("get height matches took: " + toc() + " ms");

        drawMatches(matches);
        Debug.Log("draw matches took: " + toc() + " ms");


        if (matches.Count > 4)
        {


            Debug.Log("NUMBER OF MATCHES: " + matches.Count);
            tic();
            Quaternion recoveredQ = new Quaternion();
            Vector3 recoveredT = new Vector3();
            //matches = filterMatchesWithRansac(matches, out recoveredQ, out recoveredT);
            matches = filterMatchesWithRansacDense(matches, out recoveredQ, out recoveredT);
            Debug.Log("ransac filter took: " + toc() + " ms");
            Debug.Log("NUMBER OF MATCHES AFTER RANSAC: " + matches.Count);

            //Debug.Log("viz 1 rotation before: " + camInfoList[1].visualization.transform.rotation);

            Quaternion Q1 = camInfoList[start].visualization.transform.rotation;
            Quaternion QR = recoveredQ;
            Quaternion Q2 = camInfoList[stop].visualization.transform.rotation;

            Quaternion Q1P = Quaternion.Inverse(Q1);
            Quaternion QRP = Quaternion.Inverse(QR);
            Quaternion Q2P = Quaternion.Inverse(Q2);

            Vector3 T1 = camInfoList[start].visualization.transform.position;
            Vector3 T2 = camInfoList[stop].visualization.transform.position;
            Vector3 TR = recoveredT;

            if (transformMatches)
            {
                Debug.Log("Transforming camera 1 position/rotation!");
                Debug.Log("viz stop rotation before: " + camInfoList[stop].visualization.transform.rotation);
                if (RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.three_d)
                {
                    camInfoList[stop].visualization.transform.rotation = Q1 * QRP;
                    camInfoList[stop].visualization.transform.position = Q1 * QRP * ((QR * Q1P * T1) - TR);
                }
                if (RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.in_plane)
                {
                    camInfoList[stop].visualization.transform.rotation = QRP * Q2;
                    camInfoList[stop].visualization.transform.position = QRP * (T2 - TR);
                }

                Debug.Log("viz stop rotation after: " + camInfoList[stop].visualization.transform.rotation);

            }

            //Debug.Log("** cam rotation: ****");
            float angle = 0.0f;
            Vector3 axis = new Vector3();
            camInfoList[stop].visualization.transform.rotation.ToAngleAxis(out angle, out axis);


            /*
            tic();
            icp_neighbor_thresh = 0.1f;
            for (int i = 0; i < icp_super_iterations; i++)
            {
                runICP(startCamera, stopCamera);
            }
            icp_neighbor_thresh = 0.05f;
            for (int i = 0; i < icp_super_iterations; i++)
            {
                runICP(startCamera, stopCamera);
            }
            Debug.Log("icp super iterations took: " + toc() + " ms");
            */

            /*
            icp_neighbor_thresh = 0.1f;
            runICP(startCamera, stopCamera);

            for (int i = 0; i<icp_super_iterations; i++)
            {
                runICP(startCamera, stopCamera);
            }
            Debug.Log("icp super iterations took: " + toc() + " ms");
            */


            //Debug.Log("quaternion: angle: " + angle + " axis: " + axis);


            //Debug.Log("new x: " + (camInfoList[1].visualization.transform.position.x - recoveredT.x) + ","
            //                                                                                    + (camInfoList[1].visualization.transform.position.y - recoveredT.y) + ","
            //                                                                                    + (camInfoList[1].visualization.transform.position.z - recoveredT.z));
            //Debug.Log("viz 1 rotation after: " + camInfoList[1].visualization.transform.rotation);

            /*
            runICP(start, stop);
            debugRenderTex = ((RenderTexture)debugCube.GetComponent<Renderer>().material.mainTexture);
            */


        }
        else
        {
            Debug.Log("NUMBER OF MATCHES TOO SMALL!: " + matches.Count);
        }

    }


    public ComputeShader icp_shader;
    public GameObject debugCube;
    public GameObject icp_viz_holder;

    List<GameObject> start_sample_sphere_list = new List<GameObject>();
    List<GameObject> stop_sample_sphere_list = new List<GameObject>();
    List<GameObject> projected_stop_sample_sphere_list = new List<GameObject>();
    List<GameObject> icp_match_list = new List<GameObject>();
    public bool ICP_IN_PLANE_ONLY = false;


    //requires floorInfo to be populated
    //requires camInfoList to be populated
    void sampleCamera(int camera_idx, int depth_sample_grid, float min_height, out List<Vector3> points)
    {
        points = new List<Vector3>();

        byte[] depthStartBytes = camInfoList[camera_idx].depthBytes;
        byte[] distortionStartBytes = camInfoList[camera_idx].XYZMapBytes;

        EstimateBiggestPlane.planeData pd_start = new EstimateBiggestPlane.planeData();
        pd_start.point = floorInfo[camera_idx].floorObject.transform.position;
        pd_start.normal = floorInfo[camera_idx].floorObject.transform.up;

        for (int xx = 0; xx < depthSampleSize; xx++)
        {
            for (int yy = 0; yy < depthSampleSize; yy++)
            {
                int row = camInfoList[camera_idx].depth_height / depthSampleSize * yy;
                int col = camInfoList[camera_idx].depth_width / depthSampleSize * xx;
                float depth = 0.0f;

                GameObject dummy = new GameObject();
                Vector3 sample = getPoint(col, row, camInfoList[camera_idx].depth_width, camInfoList[camera_idx].depth_height, ref depthStartBytes, ref distortionStartBytes, out depth);
                dummy.transform.parent = camInfoList[camera_idx].visualization.transform;
                dummy.transform.localPosition = sample;

                float radius = dummy.transform.localPosition.magnitude;
                sample = dummy.transform.position;

                GameObject.Destroy(dummy);


                float height = EstimateBiggestPlane.getHeight(sample, pd_start);
                if (depth > 0.0f && (height > min_height))
                {
                    points.Add(sample);
                }

            }
        }

    }


    void runICP(int start_idx, int stop_idx, bool shift, out int overlap)
    {

        for(int i = 0; i<start_sample_sphere_list.Count; i++)
        {
            GameObject.Destroy(start_sample_sphere_list[i]);
        }
        start_sample_sphere_list.Clear();

        for (int i = 0; i < stop_sample_sphere_list.Count; i++)
        {
            GameObject.Destroy(stop_sample_sphere_list[i]);
        }
        stop_sample_sphere_list.Clear();

        for (int i = 0; i < projected_stop_sample_sphere_list.Count; i++)
        {
            GameObject.Destroy(projected_stop_sample_sphere_list[i]);
        }
        projected_stop_sample_sphere_list.Clear();

        for (int i = 0; i < icp_match_list.Count; i++)
        {
            GameObject.Destroy(icp_match_list[i]);
        }
        icp_match_list.Clear();



        ICP.in_plane_only = ICP_IN_PLANE_ONLY;
        ICP.initializeICPClass(icp_shader, debugCube, icp_result_scale, icp_neighbor_thresh);

        List<Vector3> startSample = new List<Vector3>();
        List<Vector3> stopSample = new List<Vector3>();

        sampleCamera(start_idx, depthSampleSize, min_icp_height, out startSample);
        sampleCamera(stop_idx, depthSampleSize, min_icp_height, out stopSample);

        


        if (startSample.Count < stopSample.Count)
        {
            List<Vector3> temp = new List<Vector3>();
            for(int i = 0; i<startSample.Count; i++)
            {
                temp.Add(stopSample[i]);
            }
            stopSample = temp;
        }
        else
        {
            List<Vector3> temp = new List<Vector3>();
            for (int i = 0; i < stopSample.Count; i++)
            {
                temp.Add(startSample[i]);
            }
            startSample = temp;
        }



        Quaternion Qe;
        Vector3 Te;
        Vector3[] projectedStopPoints;
        int[] match_idx_array = new int[startSample.Count];

        projectedStopPoints = stopSample.ToArray();
        for(int i = 0; i<icp_iterations; i++)
        {
            //Vector3[] outProjectedStopPoints = new Vector3[projectedStopPoints.Length];
            ICP.ICP_iteration(startSample.ToArray(), projectedStopPoints, out Qe, out Te, out projectedStopPoints, out match_idx_array);
            //outProjectedStopPoints.CopyTo(projectedStopPoints, 0);

            Quaternion Q2 = camInfoList[stop_idx].visualization.transform.rotation;
            Vector3 T2 = camInfoList[stop_idx].visualization.transform.position;

            if (shift)
            {
                //camInfoList[stop_idx].visualization.transform.position = - (Quaternion.Inverse(Q2) * Te) + Quaternion.Inverse(Q2) * T2;
                camInfoList[stop_idx].visualization.transform.position = Quaternion.Inverse(Qe) * (T2 - Te);
                camInfoList[stop_idx].visualization.transform.rotation = Quaternion.Inverse(Qe) * Q2;

            }


        }

        overlap = 0;
        for (int i = 0; i < match_idx_array.Length; i++) {
            if(match_idx_array[i] >= 0)
            {
                overlap++;
            }
        }








        for (int i = 0; i<startSample.Count; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.layer = LayerMask.NameToLayer("Debug");
            sphere.transform.position = startSample[i];
            sphere.transform.localScale = new Vector3(icp_sphere_size, icp_sphere_size, icp_sphere_size);
            sphere.GetComponent<Renderer>().material.color = Color.blue;
            sphere.transform.parent = icp_viz_holder.transform;
            sphere.name = "start_sample_" + i;
            start_sample_sphere_list.Add(sphere);
        }
        for (int i = 0; i < stopSample.Count; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.layer = LayerMask.NameToLayer("Debug");
            sphere.transform.position = stopSample[i];
            sphere.transform.localScale = new Vector3(icp_sphere_size, icp_sphere_size, icp_sphere_size);
            sphere.GetComponent<Renderer>().material.color = Color.yellow;
            sphere.transform.parent = icp_viz_holder.transform;
            sphere.name = "stop_sample_" + i;
            stop_sample_sphere_list.Add(sphere);
        }

        for (int i = 0; i < stopSample.Count; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.layer = LayerMask.NameToLayer("Debug");
            sphere.transform.position = projectedStopPoints[i];
            sphere.transform.localScale = new Vector3(icp_sphere_size, icp_sphere_size, icp_sphere_size);
            sphere.GetComponent<Renderer>().material.color = Color.green;
            sphere.transform.parent = icp_viz_holder.transform;
            sphere.name = "projected_sample_" + i;
            projected_stop_sample_sphere_list.Add(sphere);

        }

        for (int i = 0; i<match_idx_array.Length; i++)
        {
            if (match_idx_array[i] >= 0)
            {
                GameObject laser = GameObject.Instantiate(laserPrefab);
                //laser.GetComponent<Laser>().startObject = start_sample_sphere_list[i];
                //laser.GetComponent<Laser>().stopObject = stop_sample_sphere_list[match_idx_array[i]];

                laser.GetComponent<Laser>().startObject = stop_sample_sphere_list[match_idx_array[i]];
                laser.GetComponent<Laser>().stopObject = projected_stop_sample_sphere_list[match_idx_array[i]];

                laser.transform.parent = icp_viz_holder.transform;
                icp_match_list.Add(laser);
            }
        }
    }



    
    List<match_struct> getHeightMatches(int start, int stop)
    {
        List<match_struct> match_list = new List<match_struct>();
        /*
        for (int i = 0; i < num_matches; i++)
        {
            match_struct ms = new match_struct();
            ms.start_camera = start;
            ms.stop_camera = stop;
            ms.start_corner_idx = -1;
            ms.stop_corner_idx = -1;
            match_list.Add(ms);
        }
        */

        int height_match_count = 0;
        for (int i = 0; i < num_corners_to_extract; i++) {
            if(corner_info_list[start][i].x_pixel >= 0)
            {
                /*
                for (int j = 0; j < num_corners_to_extract; j++)
                {
                    if (corner_info_list[stop][j].x_pixel >= 0)
                    {
                        //get height diff:
                        Vector3 start_corner = corner_info_list[start][i].point;
                        float start_height = (EstimateBiggestPlane.getHeight(start_corner, floorInfo[start]));
                        corner_info_list[start][i].sphere.name = "corner_" + i + "_height_" + start_height;

                        Vector3 stop_corner = corner_info_list[stop][j].point;
                        float stop_height = (EstimateBiggestPlane.getHeight(stop_corner, floorInfo[stop]));
                        corner_info_list[stop][j].sphere.name = "corner_" + j + "_height_" + stop_height;
                        if (start_height > min_match_height)
                        {
                            if (Mathf.Abs(start_height - stop_height) < heightDiffThreshold)
                            {
                                //add a new match:
                                match_struct ms = new match_struct();
                                ms.start_camera = start;
                                ms.stop_camera = stop;
                                ms.start_corner_idx = i;
                                ms.stop_corner_idx = j;
                                ms.start_corner = corner_info_list[start][i];
                                ms.stop_corner = corner_info_list[stop][j];
                                match_list.Add(ms);
                            }

                        }

                    }
                }
                */


                
                if (debugMode)
                //if (i < num_corners_to_extract)
                {
                    if (corner_info_list[stop][i].x_pixel >= 0)
                    {
                        //get height diff:
                        Vector3 start_corner = corner_info_list[start][i].point;
                        float start_height = Mathf.Abs(EstimateBiggestPlane.getHeight(start_corner, floorInfo[start]));
                        corner_info_list[start][i].sphere.name = "corner_" + i 
                                                                            + "_height_" + start_height 
                                                                            + "_curvature_" + corner_info_list[start][i].curvature 
                                                                            + "_dotpitch_" + corner_info_list[start][i].dotpitch;

                        Vector3 stop_corner = corner_info_list[stop][i].point;
                        float stop_height = Mathf.Abs(EstimateBiggestPlane.getHeight(stop_corner, floorInfo[stop]));
                        //corner_info_list[stop][i].sphere.name = "corner_" + i + "_height_" + stop_height;
                        corner_info_list[stop][i].sphere.name = "corner_" + i
                                                    + "_height_" + start_height
                                                    + "_curvature_" + corner_info_list[stop][i].curvature
                                                    + "_dotpitch_" + corner_info_list[stop][i].dotpitch;


                        if (start_height > min_match_height)
                        {
                            
                            if (Mathf.Abs(start_height - stop_height) < heightDiffThreshold)
                            {
                                //add a new match:
                                match_struct ms = new match_struct();
                                ms.start_camera = start;
                                ms.stop_camera = stop;
                                ms.start_corner_idx = i;
                                ms.stop_corner_idx = i;
                                ms.start_corner = corner_info_list[start][i];
                                ms.stop_corner = corner_info_list[stop][i];
                                match_list.Add(ms);

                                Debug.Log("added match: " + match_list.Count + " for i: " + i);
                            }

                        }
                    }
                }
                else
                {
                    for (int j = 0; j < num_corners_to_extract; j++)
                    {
                        if (corner_info_list[stop][j].x_pixel >= 0)
                        {
                            //get height diff:
                            Vector3 start_corner = corner_info_list[start][i].point;
                            float start_height = Mathf.Abs(EstimateBiggestPlane.getHeight(start_corner, floorInfo[start]));
                            //corner_info_list[start][i].sphere.name = "corner_" + i + "_height_" + start_height;
                            corner_info_list[start][i].sphere.name = "corner_" + i
                                                + "_height_" + start_height
                                                + "_curvature_" + corner_info_list[start][i].curvature
                                                + "_dotpitch_" + corner_info_list[start][i].dotpitch
                                                +"_color_" + corner_info_list[start][i].color
                                                +"_column_" + corner_info_list[start][i].column;


                            Vector3 stop_corner = corner_info_list[stop][j].point;
                            float stop_height = Mathf.Abs(EstimateBiggestPlane.getHeight(stop_corner, floorInfo[stop]));
                            //corner_info_list[stop][j].sphere.name = "corner_" + j + "_height_" + stop_height;
                            corner_info_list[stop][j].sphere.name = "corner_" + j
                                                                            + "_height_" + start_height
                                                                            + "_curvature_" + corner_info_list[stop][j].curvature
                                                                            + "_dotpitch_" + corner_info_list[stop][j].dotpitch
                                                                            + "_color_" + corner_info_list[stop][j].color
                                                                            + "_column_" + corner_info_list[stop][j].column;
                            if (start_height > min_match_height)
                            {
                                if (Mathf.Abs(start_height - stop_height) < heightDiffThreshold)
                                {

                                    height_match_count++;

                                    //if features also match?
                                    float feature_dist = 0.0f;
                                    for (int ff = 0; ff < corner_info_list[start][i].descriptionArray.Length; ff++) {
                                        //feature_dist += Mathf.Abs(corner_info_list[start][i].descriptionArray[ff] - corner_info_list[stop][j].descriptionArray[ff]);
                                        feature_dist += Mathf.Pow(corner_info_list[start][i].descriptionArray[ff] - corner_info_list[stop][j].descriptionArray[ff], 2);
                                    }
                                    feature_dist = Mathf.Sqrt(feature_dist);
                                    float hist_dist = feature_dist;
                                    //feature_dist = feature_dist / corner_info_list[start][i].descriptionArray.Length; //don't need to do this anymore because it's normalized


                                    //feature_dist = Mathf.Abs(corner_info_list[start][i].dotpitch - corner_info_list[stop][j].dotpitch);
                                    if (feature_to_visualize == feature_visualization_enum.dotpitch)
                                    {
                                        feature_dist = Mathf.Abs(corner_info_list[start][i].dotpitch - corner_info_list[stop][j].dotpitch);
                                    }

                                    if (feature_to_visualize == feature_visualization_enum.curvature)
                                    {
                                        feature_dist = Mathf.Abs(corner_info_list[start][i].curvature - corner_info_list[stop][j].curvature);
                                    }

                                    if (feature_to_visualize == feature_visualization_enum.color)
                                    {
                                        feature_dist = Mathf.Abs(corner_info_list[start][i].color - corner_info_list[stop][j].color);
                                    }

                                    if (feature_to_visualize == feature_visualization_enum.column)
                                    {
                                        feature_dist = Mathf.Abs(corner_info_list[start][i].column - corner_info_list[stop][j].column);
                                        feature_dist = feature_dist + hist_dist;
                                    }

                                    float secondary_dist = Mathf.Abs(corner_info_list[start][i].dotpitch - corner_info_list[stop][j].dotpitch);
                                    secondary_dist = 0.0f;

                                    if ((feature_dist < feature_dist_threshold) && (secondary_dist < feature_dist_threshold))
                                    {
                                        //add a new match:
                                        match_struct ms = new match_struct();
                                        ms.start_camera = start;
                                        ms.stop_camera = stop;
                                        ms.start_corner_idx = i;
                                        ms.stop_corner_idx = j;
                                        ms.start_corner = corner_info_list[start][i];
                                        ms.stop_corner = corner_info_list[stop][j];
                                        ms.value = feature_dist;
                                        match_list.Add(ms);
                                    }


                                }

                            }

                        }
                    }
                    
                }
                



            }
        }

        //Debug.Log("NUMBER OF STRAIGHT HEIGHT MATCHES: " + height_match_count);
        return match_list;
    }









    List<GameObject> debugFloorPlaneList = new List<GameObject>();

    void getFloorPlanes()
    {

        for(int i = 0; i<debugFloorPlaneList.Count; i++)
        {
            GameObject.Destroy(debugFloorPlaneList[i]);
        }
        debugFloorPlaneList.Clear();


        //clear floor info:
        for(int cc = 0; cc<camInfoList.Count; cc++)
        {
            if (floorInfo[cc].floorObject != null)
            {
                GameObject.Destroy(floorInfo[cc].floorObject);
            }
        }


        for(int cc = 0; cc<camInfoList.Count; cc++)
        {
            //int numPoints = camInfoList[cc].depth_width * camInfoList[cc].depth_height;

            byte[] depthBytes = camInfoList[cc].depthBytes;
            byte[] XYZMapBytes = camInfoList[cc].XYZMapBytes;

            List<Vector3> sampledPoints = new List<Vector3>();
            int[] ridx = getRandomSet(camInfoList[cc].depth_width * camInfoList[cc].depth_height, floorSampleSize);
            for(int i = 0; i<ridx.Length; i++)
            {
                int idx = ridx[i];
                int row = idx / camInfoList[cc].depth_width;
                int col = idx % camInfoList[cc].depth_width;
                float depth = 0.0f;

                Vector3 sampled_point = getPoint(col, row, camInfoList[cc].depth_width, camInfoList[cc].depth_height, ref depthBytes, ref XYZMapBytes, out depth);
                if (depth > 0.0f)
                {
                    sampledPoints.Add(sampled_point);
                }
                


            }


            
            int[] inside_idx;
            int[] outside_idx;
            floorInfo[cc] = new EstimateBiggestPlane.planeData();
            EstimateBiggestPlane.inlierThreshold = floorInlierThreshold;
            EstimateBiggestPlane.outlierThreshold = floorOutlierThreshold;
            EstimateBiggestPlane.ransac_iterations = floorRansacIterations;
            EstimateBiggestPlane.identify_biggest_plane(sampledPoints.ToArray(), out floorInfo[cc], out inside_idx, out outside_idx);

            //figure out if most of the points are above the plane, if yes, leave it alone, if no, flip the normal of the plane:
            int aboveCount = 0;
            int belowCount = 0;
            for(int i = 0; i<sampledPoints.Count; i++)
            {
                if(EstimateBiggestPlane.getHeight(sampledPoints[i], floorInfo[cc]) > 0.1f)
                {
                    aboveCount++;
                }
                if(EstimateBiggestPlane.getHeight(sampledPoints[i], floorInfo[cc]) < -0.1f)
                {
                    belowCount++;
                }
            }

            bool planeFlip = false;
            //Debug.Log("above count: " + aboveCount + " below count: " + belowCount + " camera: " + cc);
            if(aboveCount > belowCount)
            {
                //don't flip the plane
                //Debug.Log("above count is high, not flipping the plane: " + cc);
            }
            else
            {
                planeFlip = true;
                floorInfo[cc].normal = -floorInfo[cc].normal;
                //Debug.Log("above is low: " + aboveCount + "/" + sampledPoints.Count + ". Flipping the plane: " + cc);
            }


            camInfoList[cc].visualization.transform.up = new Vector3(0.0f, 1.0f, 0.0f);

            floorInfo[cc].floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorInfo[cc].floorObject.transform.parent = camInfoList[cc].visualization.transform;
            floorInfo[cc].floorObject.transform.localPosition = floorInfo[cc].inlierCentroid;
            floorInfo[cc].floorObject.transform.up = floorInfo[cc].normal;
            floorInfo[cc].floorObject.transform.localScale = new Vector3(1.0f, 0.001f, 1.0f);
            floorInfo[cc].floorObject.name = "floor_estimate_" + cc + "_numInliers_" + EstimateBiggestPlane.best_num_inliers + "_out_of_" + sampledPoints.Count;

            //camInfoList[cc].visualization.transform.up = floorInfo[cc].normal;
            
            camInfoList[cc].visualization.transform.rotation = Quaternion.Inverse(floorInfo[cc].floorObject.transform.rotation) * camInfoList[cc].visualization.transform.rotation;
            camInfoList[cc].visualization.transform.position = camInfoList[cc].visualization.transform.position  - floorInfo[cc].floorObject.transform.position;

            /*
            //add inliers debug spheres, ugh.
            for(int i = 0; i<inside_idx.Length; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.GetComponent<Renderer>().material.color = Color.blue;
                sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                sphere.name = "inlier_" + i;
                sphere.transform.parent = camInfoList[cc].visualization.transform;

                int idx = ridx[inside_idx[i]];
                int row = idx / camInfoList[cc].depth_width;
                int col = idx % camInfoList[cc].depth_width;
                float depth = 0.0f;

                sphere.transform.localPosition = getPoint(col, row, camInfoList[cc].depth_width, camInfoList[cc].depth_height, ref depthBytes, ref XYZMapBytes, out depth);
                debugFloorPlaneList.Add(sphere);

            }

            //add outlier debug spheres, ugh.
            for (int i = 0; i < outside_idx.Length; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.GetComponent<Renderer>().material.color = Color.red;
                sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                sphere.name = "outlier_" + i;
                sphere.transform.parent = camInfoList[cc].visualization.transform;

                int idx = ridx[outside_idx[i]];
                int row = idx / camInfoList[cc].depth_width;
                int col = idx % camInfoList[cc].depth_width;
                float depth = 0.0f;

                sphere.transform.localPosition = getPoint(col, row, camInfoList[cc].depth_width, camInfoList[cc].depth_height, ref depthBytes, ref XYZMapBytes, out depth);
                debugFloorPlaneList.Add(sphere);
            }
            */


        }
    }




    List<match_struct> filterMatchesWithRansacDense(List<match_struct> matches, out Quaternion qe, out Vector3 te)
    {
        Debug.Log("size of matches: " + matches.Count);



        RANSAC3D.debugMode = debugMode;
        RANSAC3D.inlier_threshold = RANSAC_inlier_threshold;
        RANSAC3D.RANSAC_iterations = RANSAC_iterations;
        //RANSAC3D.transform_mode = RANSAC3D.transform_mode_enum.three_d;
        RANSAC3D.transform_mode = RANSAC3D.transform_mode_enum.in_plane;

        if (denseRansacVizualization)
        {
            RANSAC3D.denseVisualizationMode = true;
        }
        else
        {
            RANSAC3D.denseVisualizationMode = false;
        }

        Vector3[] startPoints = new Vector3[matches.Count];
        Vector3[] stopPoints = new Vector3[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {

            if (RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.in_plane)
            {
                startPoints[i] = matches[i].start_corner.sphere.transform.position;
                stopPoints[i] = matches[i].stop_corner.sphere.transform.position;

            }
            else
            {
                startPoints[i] = matches[i].start_corner.point;
                stopPoints[i] = matches[i].stop_corner.point;

            }

        }



        List<Vector3> validationStartSample = new List<Vector3>();
        List<Vector3> validationStopSample = new List<Vector3>();

        sampleCamera(matches[0].start_camera, depthSampleSize, min_icp_height, out validationStartSample);
        sampleCamera(matches[0].stop_camera, depthSampleSize, min_icp_height, out validationStopSample);

        if (validationStartSample.Count < validationStopSample.Count)
        {
            List<Vector3> temp = new List<Vector3>();
            for (int i = 0; i < validationStartSample.Count; i++)
            {
                temp.Add(validationStopSample[i]);
            }
            validationStopSample = temp;
        }
        else
        {
            List<Vector3> temp = new List<Vector3>();
            for (int i = 0; i < validationStopSample.Count; i++)
            {
                temp.Add(validationStartSample[i]);
            }
            validationStartSample = temp;
        }





        int[] inliers;
        int[] keyIdx;
        RANSAC3D.ransacMatchesDense(startPoints, stopPoints, validationStartSample.ToArray(), validationStopSample.ToArray(), out keyIdx, out inliers);

        Debug.Log("Size of inliers: " + inliers.Length);

        

        for (int i = 0; i < matches.Count; i++)
        {
            matches[i].laser.SetActive(true);
            matches[i].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.red;
            matches[i].laser.transform.parent = outlierHolder.transform;
        }

        for (int i = 0; i < inliers.Length; i++)
        {
            matches[inliers[i]].laser.SetActive(true);
            matches[inliers[i]].laser.name = "inlier_" + i + matches[inliers[i]].laser.name;
            matches[inliers[i]].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.blue;
            matches[inliers[i]].laser.transform.parent = inlierHolder.transform;

        }

        //color them!
        //Debug.Log("Key idx size: " + keyIdx.Length);
        for (int i = 0; i < keyIdx.Length; i++)
        {
            matches[keyIdx[i]].laser.SetActive(true);
            matches[keyIdx[i]].laser.name = "key_" + i + matches[keyIdx[i]].laser.name;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.green;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().startWidth = 0.05f;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().endWidth = 0.05f;
            matches[keyIdx[i]].laser.transform.parent = keyHolder.transform;
            //Debug.Log("Adding key line: " + matches[keyIdx[i]].laser.name + " match number: " + keyIdx[i]);
        }

        /*
        Quaternion qe;
        Vector3 te;
        RANSAC3D.ransacTransform(startPoints, stopPoints, out qe, out te);
        */

        qe = RANSAC3D.best_quaternion;
        te = RANSAC3D.best_transform;
        return matches;

    }



    List<match_struct> filterMatchesWithRansac(List < match_struct > matches, out Quaternion qe, out Vector3 te){



        RANSAC3D.debugMode = debugMode;
        RANSAC3D.inlier_threshold = RANSAC_inlier_threshold;
        RANSAC3D.RANSAC_iterations = RANSAC_iterations;
        //RANSAC3D.transform_mode = RANSAC3D.transform_mode_enum.three_d;
        RANSAC3D.transform_mode = RANSAC3D.transform_mode_enum.in_plane;

        Vector3[] startPoints = new Vector3[matches.Count];
        Vector3[] stopPoints = new Vector3[matches.Count];
        for (int i = 0; i < matches.Count; i++) {

            if(RANSAC3D.transform_mode == RANSAC3D.transform_mode_enum.in_plane)
            {
                startPoints[i] = matches[i].start_corner.sphere.transform.position;
                stopPoints[i] = matches[i].stop_corner.sphere.transform.position;

            }
            else
            {
                startPoints[i] = matches[i].start_corner.point;
                stopPoints[i] = matches[i].stop_corner.point;

            }

        }


        int[] inliers;
        int[] keyIdx;
        RANSAC3D.ransacMatches(startPoints, stopPoints, out keyIdx, out inliers);

        for(int i = 0; i<matches.Count; i++)
        {
            matches[i].laser.SetActive(true);
            matches[i].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.red;
            matches[i].laser.transform.parent = outlierHolder.transform;
        }

        for (int i = 0; i < inliers.Length; i++)
        {
            matches[inliers[i]].laser.SetActive(true);
            matches[inliers[i]].laser.name = "inlier_" + i + matches[inliers[i]].laser.name;
            matches[inliers[i]].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.blue;
            matches[inliers[i]].laser.transform.parent = inlierHolder.transform;

        }

        //color them!
        //Debug.Log("Key idx size: " + keyIdx.Length);
        for (int i = 0; i < keyIdx.Length; i++)
        {
            matches[keyIdx[i]].laser.SetActive(true);
            matches[keyIdx[i]].laser.name = "key_" + i + matches[inliers[i]].laser.name;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().sharedMaterial.color = Color.green;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().startWidth = 0.05f;
            matches[keyIdx[i]].laser.GetComponent<LineRenderer>().endWidth = 0.05f;
            matches[keyIdx[i]].laser.transform.parent = keyHolder.transform;
            //Debug.Log("Adding key line: " + matches[keyIdx[i]].laser.name + " match number: " + keyIdx[i]);
        }

        /*
        Quaternion qe;
        Vector3 te;
        RANSAC3D.ransacTransform(startPoints, stopPoints, out qe, out te);
        */

        qe = RANSAC3D.best_quaternion;
        te = RANSAC3D.best_transform;
        return matches;

    }





    void drawMatches(List<match_struct> match_list)
    {
        for(int i = 0; i<laserList.Count; i++)
        {
            GameObject.Destroy(laserList[i]);
        }
        laserList.Clear();

        for (int i = 0; i < match_list.Count; i++)
        {
            GameObject laser = GameObject.Instantiate(laserPrefab);
            laser.GetComponent<Laser>().startObject = match_list[i].start_corner.sphere;
            laser.GetComponent<Laser>().stopObject = match_list[i].stop_corner.sphere;
            laser.transform.parent = gameObject.transform;
            //laser.name = "cam_" + match_list[i].start_camera + "_" + match_list[i].start_corner_idx + "_cam_" + match_list[i].stop_camera + "_" + match_list[i].stop_corner_idx;
            laser.name = "value_" + match_list[i].value;
            laser.SetActive(true);
            laserList.Add(laser);

            match_struct ms = match_list[i];
            ms.laser = laser;
            match_list[i] = ms;
        }
    }


    //assumes descriptor texture has been created for all cameras:
    List<match_struct> getMatches(int start, int stop)
    {




        List<match_struct> match_list = new List<match_struct>();
        for(int i = 0; i<num_matches; i++)
        {
            match_struct ms = new match_struct();
            ms.start_camera = start;
            ms.stop_camera = stop;
            ms.start_corner_idx = -1;
            ms.stop_corner_idx = -1;
            match_list.Add(ms);
        }

        RenderTexture distanceTexture = new RenderTexture(num_corners_to_extract, num_corners_to_extract, 24, RenderTextureFormat.ARGBFloat);
        distanceTexture.filterMode = FilterMode.Point;
        distanceTexture.enableRandomWrite = true;
        distanceTexture.Create();

        int cornerMatcher_kh = cornerDetectorCompute.FindKernel("cornerMatcher");
        cornerDetectorCompute.SetInt("num_corners_to_extract", num_corners_to_extract);
        cornerDetectorCompute.SetTexture(cornerMatcher_kh, "descriptor_start_tex", descriptorTexture[start]);
        cornerDetectorCompute.SetTexture(cornerMatcher_kh, "descriptor_stop_tex", descriptorTexture[stop]);
        cornerDetectorCompute.SetTexture(cornerMatcher_kh, "distance_tex", distanceTexture);
        cornerDetectorCompute.SetInt("descriptor_width", descriptor_width);
        cornerDetectorCompute.SetInt("descriptor_height", descriptor_height);

        //cornerDetectorCompute.Dispatch(cornerExtractor_kh, camInfoList[cc].depth_width / 8, camInfoList[cc].depth_height / 8, 1);
        cornerDetectorCompute.Dispatch(cornerMatcher_kh, (num_corners_to_extract / 8) + 1, (num_corners_to_extract / 8) + 1, 1);


        //find the minimum elements of distance texture
        Texture2D decTex = new Texture2D(distanceTexture.width, distanceTexture.height, TextureFormat.RGBAFloat, false);
        RenderTexture.active = distanceTexture;
        decTex.ReadPixels(new Rect(0, 0, distanceTexture.width, distanceTexture.height), 0, 0);
        decTex.Apply();

        Color[] match_values = decTex.GetPixels();



        Debug.Log("num corners: " + num_corners_to_extract + " num possible matches: " + match_values.Length);

        int min_idx = -1;
        float min_value = 0.0f;

        //copy initial set:
        for (int mm = 0; mm<num_matches; mm++)
        {
            int start_corner = mm % num_corners_to_extract;
            int stop_corner = mm / num_corners_to_extract;
            float val = match_values[mm].r;


            match_struct ms = match_list[mm];
            ms.start_corner_idx = start_corner;
            ms.stop_corner_idx = stop_corner;
            ms.start_corner = corner_info_list[start][start_corner];
            ms.stop_corner = corner_info_list[stop][stop_corner];
            ms.value = val;
            match_list[mm] = ms;

            if(val < min_value || min_idx<0)
            {
                min_idx = mm;
                min_value = val;
            }
        }

        List<int> usedStartCorners = new List<int>();
        List<int> usedStopCorners = new List<int>();

        for (int i = num_matches; i < match_values.Length; i++) {
            int start_corner = i % num_corners_to_extract;
            int stop_corner = i / num_corners_to_extract;
            float val = match_values[i].r;

            if(usedStartCorners.Contains(start_corner) || usedStopCorners.Contains(stop_corner))
            {

            }
            else
            {


                if (val > min_value)
                {
                    //Debug.Log("replacing " + min_value + " at: " + min_idx + " with: " + val);
                    match_struct ms = match_list[min_idx];
                    ms.start_corner_idx = start_corner;
                    ms.stop_corner_idx = stop_corner;
                    ms.start_corner = corner_info_list[start][start_corner];
                    ms.stop_corner = corner_info_list[stop][stop_corner];
                    ms.value = val;
                    match_list[min_idx] = ms;


                    //refind minimum value:
                    min_idx = -1;
                    for (int mm = 0; mm < match_list.Count; mm++)
                    {
                        if (match_list[mm].value < min_value || min_idx < 0)
                        {
                            min_value = match_list[mm].value;
                            min_idx = mm;
                        }
                    }
                    //Debug.Log("minimum is now: " + min_value + " at: " + min_idx);
                    //Debug.Log("start corner: " + start_corner + " stop corner: " + stop_corner + " i: " + i);

                    usedStartCorners.Add(start_corner);
                    usedStopCorners.Add(stop_corner);

                }
            }


        }
        









        /*
        for (int i = 0; i<match_values.Length; i++) {
            int start_corner = i % num_corners_to_extract;
            int stop_corner = i / num_corners_to_extract;
            float val = match_values[i].r;

            for (int mm = 0; mm < match_list.Count; mm++) {

                if(match_list[mm].start_corner_idx < 0)
                {
                    match_struct ms = match_list[mm];
                    ms.start_corner_idx = start_corner;
                    ms.stop_corner_idx = stop_corner;
                    ms.start_corner = corner_info_list[start][start_corner];
                    ms.stop_corner = corner_info_list[stop][stop_corner];
                    ms.value = val;
                    match_list[mm] = ms;

                    if (match_list[mm].value < min_value || min_idx<0)
                    {
                        min_value = val;
                        min_idx = mm;
                    }
                    break;
                }
                else
                {
                    if (match_list[mm].value < min_value && min_idx>=0)
                    {
                        min_value = match_list[mm].value;
                        min_idx = mm;
                    }
                }
            }

            if (val < min_value && min_idx >= 0)
            {
                match_struct ms = match_list[min_idx];
                ms.start_corner_idx = start_corner;
                ms.stop_corner_idx = stop_corner;
                ms.start_corner = corner_info_list[start][start_corner];
                ms.stop_corner = corner_info_list[stop][stop_corner];
                ms.value = val;
                match_list[min_idx] = ms;
            }
        }
        */


        result_display_list[start].GetComponent<Renderer>().material.mainTexture = distanceTexture;
        result_display_list[stop].GetComponent<Renderer>().material.mainTexture = distanceTexture;


        return match_list;
    }




    void clearCornerInfo()
    {
        for (int cc = 0; cc < corner_info_list.Count; cc++)
        {
            for (int i = 0; i < corner_info_list[cc].Length; i++)
            {
                GameObject.Destroy(corner_info_list[cc][i].sphere);
                GameObject.Destroy(corner_info_list[cc][i].descriptionSquare);
            }
        }
        corner_info_list.Clear();


        for (int cc = 0; cc < camInfoList.Count; cc++)
        {
            corner_struct[] cs_array = new corner_struct[num_corners_to_extract];

            for (int i = 0; i < num_corners_to_extract; i++)
            {
                corner_struct cs = new corner_struct();
                cs.x_pixel = -1;
                cs.y_pixel = -1;
                cs.score = 0.0f;
                cs.point = new Vector3();

                cs_array[i] = cs;
            }
            corner_info_list.Add(cs_array);
        }
    }

    



    public Texture2D debugTex;
    public RenderTexture debugRenderTex;


    void describeCornerPoints()
    {

        descriptor_size = descriptor_width * descriptor_height;
        
        /*
        RenderTexture description_tex = new RenderTexture(descriptor_size, num_corners_to_extract, 24, RenderTextureFormat.ARGBFloat);
        description_tex.filterMode = FilterMode.Point;
        description_tex.enableRandomWrite = true;
        description_tex.Create();
        */

        for (int cc = 0; cc < camInfoList.Count; cc++) {


            if(descriptorTexture[cc]==null || descriptorTexture[cc].width != descriptor_size || descriptorTexture[cc].height != num_corners_to_extract)
            {
                descriptorTexture[cc] = new RenderTexture(descriptor_size, num_corners_to_extract, 24, RenderTextureFormat.ARGBFloat);
                descriptorTexture[cc].filterMode = FilterMode.Point;
                descriptorTexture[cc].enableRandomWrite = true;
                descriptorTexture[cc].Create();
            }


            
            ComputeBuffer corner_x_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(int));
            ComputeBuffer corner_y_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(int));


            corner_x_buffer.SetData(corner_x_pixels[cc]);
            corner_y_buffer.SetData(corner_y_pixels[cc]);


            ComputeBuffer curvature_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(float));
            ComputeBuffer dotpitch_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(float));
            ComputeBuffer color_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(float));
            ComputeBuffer column_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(float));



            curvature_buffer.SetData(new float[num_corners_to_extract]);
            dotpitch_buffer.SetData(new float[num_corners_to_extract]);
            color_buffer.SetData(new float[num_corners_to_extract]);
            column_buffer.SetData(new float[num_corners_to_extract]);


            int cornerExtractor_kh = cornerDetectorCompute.FindKernel("cornerDescriptor");
            cornerDetectorCompute.SetInt("num_corners_to_extract", num_corners_to_extract);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "depth_tex", camInfoList[cc].depthTex);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "distortion_tex", camInfoList[cc].distortionMapTex);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "normal_tex", normalTexture[cc]);
            cornerDetectorCompute.SetInt("width", camInfoList[cc].depth_width);
            cornerDetectorCompute.SetInt("height", camInfoList[cc].depth_height);
            cornerDetectorCompute.SetInt("descriptor_size", descriptor_size);
            cornerDetectorCompute.SetInt("descriptor_width", descriptor_width);
            cornerDetectorCompute.SetInt("descriptor_height", descriptor_height);
            cornerDetectorCompute.SetInt("num_corners_to_extract", num_corners_to_extract);


            cornerDetectorCompute.SetFloat("descriptor_proximity", descriptor_proximity);
            cornerDetectorCompute.SetInt("descriptor_search_size", descriptor_search_size);
            cornerDetectorCompute.SetFloat("descriptor_min_dist", descriptor_min_dist);
            cornerDetectorCompute.SetFloat("descriptor_max_dist", descriptor_max_dist);
            cornerDetectorCompute.SetFloat("descriptor_min_dot", descriptor_min_dot);
            cornerDetectorCompute.SetFloat("descriptor_max_dot", descriptor_max_dot);




            GameObject dummy = new GameObject();
            dummy.transform.parent = camInfoList[cc].visualization.transform;
            dummy.transform.position = floorInfo[cc].floorObject.transform.position;
            dummy.transform.position = dummy.transform.position + floorInfo[cc].floorObject.transform.up;
            
            Vector3 floor_normal = dummy.transform.localPosition - floorInfo[cc].floorObject.transform.localPosition;
            floor_normal = floor_normal.normalized;
            GameObject.Destroy(dummy);


            float floor_x = floor_normal.x;
            float floor_y = floor_normal.y;
            float floor_z = floor_normal.z;
            /*
            float floor_p_x = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).x;
            float floor_p_y = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).y;
            float floor_p_z = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).z;
            */

            float floor_p_x = floorInfo[cc].floorObject.transform.localPosition.x;
            float floor_p_y = floorInfo[cc].floorObject.transform.localPosition.y;
            float floor_p_z = floorInfo[cc].floorObject.transform.localPosition.z;



            cornerDetectorCompute.SetFloat("floor_x", floor_x);
            cornerDetectorCompute.SetFloat("floor_y", floor_y);
            cornerDetectorCompute.SetFloat("floor_z", floor_z);
            cornerDetectorCompute.SetFloat("floor_p_x", floor_p_x);
            cornerDetectorCompute.SetFloat("floor_p_y", floor_p_y);
            cornerDetectorCompute.SetFloat("floor_p_z", floor_p_z);
            //Debug.Log("local floor up for shader " + cc + ": " + floor_x + "," + floor_y + "," + floor_z);
            //Debug.Log("local floor position for shader " + cc + ": " + floor_p_x + "," + floor_p_y + "," + floor_p_z);


            akplay.camInfo cameraInfo = camInfoList[cc];
            debugTex = cameraInfo.colorTex;
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "color_tex", cameraInfo.colorTex);
            cornerDetectorCompute.SetMatrix("_color_extrinsics", cameraInfo.color_extrinsics);
            cornerDetectorCompute.SetFloat("_color_cx", cameraInfo.color_cx);
            cornerDetectorCompute.SetFloat("_color_cy", cameraInfo.color_cy);
            cornerDetectorCompute.SetFloat("_color_fx", cameraInfo.color_fx);
            cornerDetectorCompute.SetFloat("_color_fy", cameraInfo.color_fy);
            cornerDetectorCompute.SetFloat("_color_k1", cameraInfo.color_k1);
            cornerDetectorCompute.SetFloat("_color_k2", cameraInfo.color_k2);
            cornerDetectorCompute.SetFloat("_color_k3", cameraInfo.color_k3);
            cornerDetectorCompute.SetFloat("_color_k4", cameraInfo.color_k4);
            cornerDetectorCompute.SetFloat("_color_k5", cameraInfo.color_k5);
            cornerDetectorCompute.SetFloat("_color_k6", cameraInfo.color_k6);
            //Debug.Log(gameObject + " setting size: " + size);
            //Debug.Log(gameObject + " setting ks: " + cameraInfo.color_k1 + " " + cameraInfo.color_k2 + " " + cameraInfo.color_k3 + " " + cameraInfo.color_k4 + " " + cameraInfo.color_k5 + " " + cameraInfo.color_k6);
            cornerDetectorCompute.SetFloat("_color_codx", cameraInfo.color_codx);
            cornerDetectorCompute.SetFloat("_color_cody", cameraInfo.color_cody);
            cornerDetectorCompute.SetFloat("_color_p1", cameraInfo.color_p1);
            cornerDetectorCompute.SetFloat("_color_p2", cameraInfo.color_p2);
            cornerDetectorCompute.SetFloat("_color_metric_radius", cameraInfo.color_radius);
            cornerDetectorCompute.SetInt("color_width", cameraInfo.color_width);
            cornerDetectorCompute.SetInt("color_height", cameraInfo.color_height);



            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "curvature_buffer", curvature_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "dotpitch_buffer", dotpitch_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "color_buffer", color_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "column_buffer", column_buffer);


            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_x_buffer", corner_x_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_y_buffer", corner_y_buffer);

            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "description_tex", descriptorTexture[cc]);

            cornerDetectorCompute.Dispatch(cornerExtractor_kh, (num_corners_to_extract/64)+1, 1, 1);
            result_display_list[cc].GetComponent<Renderer>().material.mainTexture = descriptorTexture[cc];


            float[] curvature_array = new float[num_corners_to_extract];
            float[] dotpitch_array = new float[num_corners_to_extract];
            float[] color_array = new float[num_corners_to_extract];
            float[] column_array = new float[num_corners_to_extract];

            curvature_buffer.GetData(curvature_array);
            dotpitch_buffer.GetData(dotpitch_array);
            color_buffer.GetData(color_array);
            column_buffer.GetData(column_array);


            corner_x_buffer.Dispose();
            corner_y_buffer.Dispose();
            curvature_buffer.Dispose();
            dotpitch_buffer.Dispose();
            color_buffer.Dispose();
            column_buffer.Dispose();

            //copy to float array!
            Texture2D decTex = new Texture2D(descriptorTexture[cc].width, descriptorTexture[cc].height, TextureFormat.RGBAFloat, false);
            RenderTexture.active = descriptorTexture[cc];
            decTex.ReadPixels(new Rect(0, 0, descriptorTexture[cc].width, descriptorTexture[cc].height), 0, 0);
            decTex.Apply();


            for(int i = 0; i<num_corners_to_extract; i++)
            {
                Color[] info = decTex.GetPixels(0, i, descriptor_size, 1);
                float[] info_float = new float[descriptor_size];
                for(int ff = 0; ff<info.Length; ff++)
                {
                    info_float[ff] = info[ff].r;
                }
                corner_struct cs = corner_info_list[cc][i];
                cs.descriptionArray = info_float;
                if(cs.descriptionTex == null || cs.descriptionTex.width!=descriptor_width || cs.descriptionTex.height!=descriptor_height)
                {
                    cs.descriptionTex = new Texture2D(descriptor_width, descriptor_height, TextureFormat.ARGB32, false);
                    cs.descriptionTex.filterMode = FilterMode.Point;
                    cs.descriptionTex.SetPixels(info);
                    cs.descriptionTex.Apply();
                    //cs.descriptionSquare.GetComponent<Renderer>().material.mainTexture = cs.descriptionTex;
                }

                cs.curvature = curvature_array[i];
                cs.dotpitch = dotpitch_array[i];
                cs.color = color_array[i];
                cs.column = column_array[i];

                corner_info_list[cc][i] = cs;

            }

        }

        
    }


    public bool do_corner_visualization = false;
    void updateCornerVisualization()
    {
        if (do_corner_visualization)
        {
            for (int cc = 0; cc < camInfoList.Count; cc++)
            {
                for (int i = 0; i < corner_info_list[cc].Length; i++)
                {
                    corner_struct cs = corner_info_list[cc][i];

                    if (feature_to_visualize == feature_visualization_enum.spin_histogram)
                    {
                        cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));
                        cs.descriptionSquare.GetComponent<Renderer>().material.mainTexture = cs.descriptionTex;
                    }

                    if (feature_to_visualize == feature_visualization_enum.curvature)
                    {
                        cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color"));
                        Color c = Color.HSVToRGB(cs.curvature / feature_max, 1.0f, 1.0f);
                        cs.descriptionSquare.GetComponent<Renderer>().material.color = c;
                    }

                    if (feature_to_visualize == feature_visualization_enum.dotpitch)
                    {
                        cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color"));
                        Color c = Color.HSVToRGB(cs.dotpitch / feature_max, 1.0f, 1.0f);
                        cs.descriptionSquare.GetComponent<Renderer>().material.color = c;
                    }

                    if (feature_to_visualize == feature_visualization_enum.color)
                    {
                        cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color"));
                        Color c = Color.HSVToRGB(cs.color / feature_max, 1.0f, 1.0f);
                        cs.descriptionSquare.GetComponent<Renderer>().material.color = c;
                    }

                    if (feature_to_visualize == feature_visualization_enum.column)
                    {
                        cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color"));
                        Color c = Color.HSVToRGB(cs.column / feature_max, 1.0f, 1.0f);
                        cs.descriptionSquare.GetComponent<Renderer>().material.color = c;
                    }


                }
            }

        }
    }

    void getNormalTexture()
    {
        int numCameras = camInfoList.Count;

        for (int cc = 0; cc < numCameras; cc++)
        {
            if (resultTexture[cc] == null || resultTexture[cc].width != camInfoList[cc].depthTex.width || resultTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                resultTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24);
                resultTexture[cc].enableRandomWrite = true;
                resultTexture[cc].Create();
            }

            if (normalTexture[cc] == null || normalTexture[cc].width != camInfoList[cc].depthTex.width || normalTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                normalTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24);
                normalTexture[cc].enableRandomWrite = true;
                normalTexture[cc].Create();
            }

            int normal_kh = radiusFeatureDetectorCompute.FindKernel("getNormal");
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "depth_for_normal_tex", camInfoList[cc].depthTex);
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "distortion_for_normal_tex", camInfoList[cc].distortionMapTex);
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "normal_result_tex", normalTexture[cc]);
            radiusFeatureDetectorCompute.SetFloat("normal_multiplier", normal_multiplier);
            radiusFeatureDetectorCompute.SetInt("normal_filter_size", normal_filter_size);
            radiusFeatureDetectorCompute.Dispatch(normal_kh, camInfoList[cc].depth_width / 8, camInfoList[cc].depth_height / 8, 1);
        }
    }



    List<GameObject> cornerHolder = new List<GameObject>();

    void placeCornerPoints()
    {
        /*
        //put spheres in their places
        for (int i = 0; i < sphereList.Count; i++)
        {
            for (int j = 0; j < sphereList[i].Count; j++)
            {
                GameObject.Destroy(sphereList[i][j].startSphere);
                GameObject.Destroy(sphereList[i][j].currentSphere);
            }
            sphereList[i].Clear();
        }
        sphereList.Clear();
        */


        //clear corner holders:
        for(int cc = 0; cc<cornerHolder.Count; cc++)
        {
            
            foreach(Transform t in cornerHolder[cc].transform)
            {
                GameObject.Destroy(t.gameObject);
            }
            GameObject.Destroy(cornerHolder[cc]);
        }
        cornerHolder.Clear();
        


        for (int cc = 0; cc < camInfoList.Count; cc++)
        {
            //List<sphereStruct> sphereListForCamera = new List<sphereStruct>();
            GameObject holder = new GameObject();
            holder.transform.parent = camInfoList[cc].visualization.transform;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localPosition = new Vector3();


            cornerHolder.Add(holder);
            for (int i = 0; i < num_corners_to_extract; i++)
            {
                corner_struct cs = corner_info_list[cc][i];
                if(cs.x_pixel >= 0)
                {
                    Vector3 point = cs.point;
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    //sphere.transform.localScale = (new Vector3(sphere_size, sphere_size, sphere_size))/max_corner_score*corner_scores[cc][i];
                    sphere.transform.localScale = (new Vector3(sphere_size, sphere_size, sphere_size));
                    sphere.GetComponent<Renderer>().material.color = Color.HSVToRGB(1.0f / (float)camInfoList.Count * (float)cc, 1.0f, 1.0f);
                    //sphere.transform.parent = camInfoList[cc].visualization.transform;
                    sphere.transform.parent = holder.transform;
                    sphere.transform.localPosition = point;

                    sphere.name = "cam_" + cc + "_corner_" + i + "_x_" + cs.x_pixel + "_y_" + cs.y_pixel + "_score_" + cs.score;

                    cs.sphere = sphere;

                    cs.descriptionSquare = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cs.descriptionSquare.transform.localScale = new Vector3(0.05f, 0.05f, 0.001f);
                    cs.descriptionSquare.transform.parent = cs.sphere.transform;
                    cs.descriptionSquare.transform.localPosition = new Vector3(0.0f, 0.1f / cs.sphere.transform.lossyScale.y, 0.0f);
                    cs.descriptionSquare.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));



                    corner_info_list[cc][i] = cs;
                }


                /*
                if (corner_x_pixels[cc][i] >= 0)
                {
                    Vector3 point = corner_points[cc][i];
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    //sphere.transform.localScale = (new Vector3(sphere_size, sphere_size, sphere_size))/max_corner_score*corner_scores[cc][i];
                    sphere.transform.localScale = (new Vector3(sphere_size, sphere_size, sphere_size)) ;
                    sphere.GetComponent<Renderer>().material.color = Color.HSVToRGB(1.0f/(float)camInfoList.Count * (float)cc, 1.0f, 1.0f);
                    sphere.transform.parent = camInfoList[cc].visualization.transform;
                    sphere.transform.localPosition = point;

                    sphere.name = "cam_" + cc + "_corner_" + i + "_x_" + corner_x_pixels[cc][i] + "_y_" + corner_y_pixels[cc][i] + "_score_" + corner_scores[cc][i];


                    sphereStruct sphereInfo = new sphereStruct();
                    sphereInfo.startSphere = sphere;
                    //sphereInfo.currentSphere = sphere2;
                    //sphereInfo.startPixel = new Vector2(random_x, random_y);
                    //sphereInfo.currentPixel = new Vector2(random_x, random_y);


                    sphereListForCamera.Add(sphereInfo);
                }
                */
            }

            //sphereList.Add(sphereListForCamera);
        }
    }

    void extractCornerPoints()
    {
        int numCameras = camInfoList.Count;
        int num_non_zero = 0;




        corner_x_pixels.Clear();
        corner_y_pixels.Clear();
        corner_scores.Clear();
        corner_points.Clear();

        for (int cc = 0; cc < numCameras; cc++)
        {
            int[] corner_x_pixel_array = new int[num_corners_to_extract];
            int[] corner_y_pixel_array = new int[num_corners_to_extract];
            float[] corner_score_array = new float[num_corners_to_extract];
            Vector3[] corner_point_array = new Vector3[num_corners_to_extract];

            for (int i = 0; i < num_corners_to_extract; i++)
            {
                corner_x_pixel_array[i] = -1;
                corner_y_pixel_array[i] = -1;
                corner_score_array[i] = 0.0f;
                corner_point_array[i] = new Vector3();
            }

            corner_x_pixels.Add(corner_x_pixel_array);
            corner_y_pixels.Add(corner_y_pixel_array);
            corner_scores.Add(corner_score_array);
            corner_points.Add(corner_point_array);
        }


        for (int cc = 0; cc < numCameras; cc++)
        {
            ComputeBuffer corner_x_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(int));
            ComputeBuffer corner_y_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(int));
            ComputeBuffer corner_score_buffer = new ComputeBuffer(num_corners_to_extract, sizeof(float));
            ComputeBuffer corner_point_buffer = new ComputeBuffer(num_corners_to_extract, 3 * sizeof(float));



            ComputeBuffer max_corner_score_buffer = new ComputeBuffer(1, sizeof(float));
            float[] temp = new float[1];
            temp[0] = max_corner_score;
            max_corner_score_buffer.SetData(temp);


            ComputeBuffer num_non_zero_buffer = new ComputeBuffer(1, sizeof(int));
            int[] temp2 = new int[1];
            temp2[0] = 0; ;
            num_non_zero_buffer.SetData(temp2);

            corner_x_buffer.SetData(corner_x_pixels[cc]);
            corner_y_buffer.SetData(corner_y_pixels[cc]);
            corner_score_buffer.SetData(corner_scores[cc]);
            corner_point_buffer.SetData(corner_points[cc]);

            int cornerExtractor_kh = cornerDetectorCompute.FindKernel("cornerExtractor");
            cornerDetectorCompute.SetInt("num_corners_to_extract", num_corners_to_extract);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "depth_tex", camInfoList[cc].depthTex);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "distortion_tex", camInfoList[cc].distortionMapTex);
            cornerDetectorCompute.SetTexture(cornerExtractor_kh, "corner_tex_for_extractor", cornerTexture[cc]);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_x_buffer", corner_x_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_y_buffer", corner_y_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_score_buffer", corner_score_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "corner_point_buffer", corner_point_buffer);


            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "max_corner_score_buffer", max_corner_score_buffer);
            cornerDetectorCompute.SetBuffer(cornerExtractor_kh, "num_non_zero_buffer", num_non_zero_buffer);

            //cornerDetectorCompute.Dispatch(cornerExtractor_kh, camInfoList[cc].depth_width / 8, camInfoList[cc].depth_height / 8, 1);
            cornerDetectorCompute.Dispatch(cornerExtractor_kh, camInfoList[cc].depth_width, camInfoList[cc].depth_height, 1);

            corner_x_buffer.GetData(corner_x_pixels[cc]);
            corner_y_buffer.GetData(corner_y_pixels[cc]);
            corner_score_buffer.GetData(corner_scores[cc]);
            corner_point_buffer.GetData(corner_points[cc]);

            num_non_zero_buffer.GetData(temp2);
            num_non_zero = temp2[0];

            corner_x_buffer.Dispose();
            corner_y_buffer.Dispose();
            corner_score_buffer.Dispose();
            corner_point_buffer.Dispose();
            num_non_zero_buffer.Dispose();
            max_corner_score_buffer.Dispose();

        }


        for(int i = 0; i<num_corners_to_extract; i++)
        {
            for(int cc = 0; cc<camInfoList.Count; cc++)
            {
                corner_struct cs = corner_info_list[cc][i];
                cs.x_pixel = corner_x_pixels[cc][i];
                cs.y_pixel = corner_y_pixels[cc][i];
                cs.score = corner_scores[cc][i];
                cs.point = corner_points[cc][i];
                corner_info_list[cc][i] = cs;
            }
        }

        
        /*
        for (int i = 0; i < num_corners_to_extract; i++)
        {
            Debug.Log("corner " + i + ": " + corner_x_pixels[0][i]
                                                                  + "," + corner_y_pixels[0][i]
                                                                  + "," + corner_scores[0][i]
                                                                  + "," + corner_points[0][i]
                                                                  + ", max_score: " + max_corner_score
                                                                  + ", num_non_zero: " + num_non_zero);
        }
        */
        
        
    }

    void getCornerTexture()
    {
        int numCameras = camInfoList.Count;

        for (int cc = 0; cc < numCameras; cc++)
        {
            if (resultTexture[cc] == null || resultTexture[cc].width != camInfoList[cc].depthTex.width || resultTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                resultTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24);
                resultTexture[cc].enableRandomWrite = true;
                resultTexture[cc].Create();
            }

            if (cornerTexture[cc] == null || cornerTexture[cc].width != camInfoList[cc].depthTex.width || cornerTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                cornerTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24, RenderTextureFormat.ARGBFloat);
                cornerTexture[cc].enableRandomWrite = true;
                cornerTexture[cc].Create();
            }

            ComputeBuffer max_corner_score_buffer = new ComputeBuffer(1, sizeof(float));
            float[] zero = new float[1];
            zero[0] = 0.0f;
            max_corner_score_buffer.SetData(zero);







            GameObject dummy = new GameObject();
            dummy.transform.parent = camInfoList[cc].visualization.transform;
            dummy.transform.position = floorInfo[cc].floorObject.transform.position;
            dummy.transform.position = dummy.transform.position + floorInfo[cc].floorObject.transform.up;

            Vector3 floor_normal = dummy.transform.localPosition - floorInfo[cc].floorObject.transform.localPosition;
            floor_normal = floor_normal.normalized;
            GameObject.Destroy(dummy);


            float floor_x = floor_normal.x;
            float floor_y = floor_normal.y;
            float floor_z = floor_normal.z;
            /*
            float floor_p_x = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).x;
            float floor_p_y = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).y;
            float floor_p_z = camInfoList[cc].visualization.transform.InverseTransformPoint(floorInfo[cc].floorObject.transform.position).z;
            */

            float floor_p_x = floorInfo[cc].floorObject.transform.localPosition.x;
            float floor_p_y = floorInfo[cc].floorObject.transform.localPosition.y;
            float floor_p_z = floorInfo[cc].floorObject.transform.localPosition.z;


            cornerDetectorCompute.SetFloat("floor_x", floor_x);
            cornerDetectorCompute.SetFloat("floor_y", floor_y);
            cornerDetectorCompute.SetFloat("floor_z", floor_z);
            cornerDetectorCompute.SetFloat("floor_p_x", floor_p_x);
            cornerDetectorCompute.SetFloat("floor_p_y", floor_p_y);
            cornerDetectorCompute.SetFloat("floor_p_z", floor_p_z);





            //int cornerDetector_kh = cornerDetectorCompute.FindKernel("cornerWalker");
            int cornerDetector_kh = cornerDetectorCompute.FindKernel("multiCornerWalker");

            cornerDetectorCompute.SetTexture(cornerDetector_kh, "depth_tex", camInfoList[cc].depthTex);
            cornerDetectorCompute.SetTexture(cornerDetector_kh, "distortion_tex", camInfoList[cc].distortionMapTex);
            cornerDetectorCompute.SetTexture(cornerDetector_kh, "corner_tex", cornerTexture[cc]);
            cornerDetectorCompute.SetTexture(cornerDetector_kh, "result_tex", resultTexture[cc]);



            cornerDetectorCompute.SetInt("search_size", search_size);
            cornerDetectorCompute.SetFloat("proximity", proximity);
            cornerDetectorCompute.SetFloat("result_multiplier", result_multiplier);

            cornerDetectorCompute.SetInt("width", camInfoList[cc].depth_width);
            cornerDetectorCompute.SetInt("height", camInfoList[cc].depth_height);

            cornerDetectorCompute.SetInt("num_iterations", iterations);
            cornerDetectorCompute.SetInt("num_points", numSpheres);

            cornerDetectorCompute.SetInt("num_iterations", iterations);
            cornerDetectorCompute.SetInt("num_points", numSpheres);
            cornerDetectorCompute.SetInt("constellation_size", constellation_size);

            cornerDetectorCompute.SetBuffer(cornerDetector_kh, "max_corner_score_buffer", max_corner_score_buffer);

            /*
            cornerDetectorCompute.SetFloat("gravity_x", radius * Mathf.Sin(theta * Mathf.Deg2Rad) * Mathf.Cos(phi * Mathf.Deg2Rad));
            cornerDetectorCompute.SetFloat("gravity_y", radius * Mathf.Sin(theta * Mathf.Deg2Rad) * Mathf.Sin(phi * Mathf.Deg2Rad));
            cornerDetectorCompute.SetFloat("gravity_z", radius * Mathf.Cos(theta * Mathf.Deg2Rad));
            */

            cornerDetectorCompute.Dispatch(cornerDetector_kh, camInfoList[cc].depth_width / 8, camInfoList[cc].depth_height / 8, 1);
            //result_display_list[cc].GetComponent<Renderer>().material.mainTexture = resultTexture[cc];
            //result_display_list[cc].GetComponent<Renderer>().material.mainTexture = cornerTexture[cc];

            float[] temp = new float[1];
            max_corner_score_buffer.GetData(temp);
            max_corner_score = temp[0];

        }


    }



    void getFeatures()
    {
        int numCameras = camInfoList.Count;

        for (int cc = 0; cc < numCameras; cc++)
        {

            if (resultTexture[cc] == null || resultTexture[cc].width != camInfoList[cc].depthTex.width || resultTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                resultTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24);
                resultTexture[cc].enableRandomWrite = true;
                resultTexture[cc].Create();
            }

            if (normalTexture[cc] == null || normalTexture[cc].width != camInfoList[cc].depthTex.width || normalTexture[cc].height != camInfoList[cc].depthTex.height)
            {
                normalTexture[cc] = new RenderTexture(camInfoList[cc].depth_width, camInfoList[cc].depth_height, 24);
                normalTexture[cc].enableRandomWrite = true;
                normalTexture[cc].Create();
            }


            int normal_kh = radiusFeatureDetectorCompute.FindKernel("getNormal");
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "depth_for_normal_tex", camInfoList[cc].depthTex);
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "distortion_for_normal_tex", camInfoList[cc].distortionMapTex);
            radiusFeatureDetectorCompute.SetTexture(normal_kh, "normal_result_tex", normalTexture[cc]);
            radiusFeatureDetectorCompute.SetFloat("normal_multiplier", normal_multiplier);
            radiusFeatureDetectorCompute.SetInt("normal_filter_size", normal_filter_size);
            radiusFeatureDetectorCompute.Dispatch(normal_kh, camInfoList[cc].depth_width / 8, camInfoList[cc].depth_height / 8, 1);

            
            //int radiusFeatureDetector_kh = radiusFeatureDetectorCompute.FindKernel("getFeatures");
            //int radiusFeatureDetector_kh = radiusFeatureDetectorCompute.FindKernel("getNormalDiff");
            int radiusFeatureDetector_kh = radiusFeatureDetectorCompute.FindKernel("cornerWalker");
            //Debug.Log("radius feature detector: " + radiusFeatureDetector_kh);

            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "depth_tex", camInfoList[cc].depthTex);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "distortion_tex", camInfoList[cc].distortionMapTex);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "normal_tex", normalTexture[cc]);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "result_tex", resultTexture[cc]);

            radiusFeatureDetectorCompute.SetInt("search_size", search_size);
            radiusFeatureDetectorCompute.SetFloat("proximity", proximity);
            radiusFeatureDetectorCompute.SetFloat("result_multiplier", result_multiplier);

            radiusFeatureDetectorCompute.SetInt("width", camInfoList[cc].depth_width);
            radiusFeatureDetectorCompute.SetInt("height", camInfoList[cc].depth_height);

            radiusFeatureDetectorCompute.SetInt("num_iterations", iterations);
            radiusFeatureDetectorCompute.SetInt("num_points", numSpheres);



            
            radiusFeatureDetectorCompute.SetFloat("gravity_x", radius*Mathf.Sin(theta*Mathf.Deg2Rad)*Mathf.Cos(phi * Mathf.Deg2Rad));
            radiusFeatureDetectorCompute.SetFloat("gravity_y", radius * Mathf.Sin(theta * Mathf.Deg2Rad) * Mathf.Sin(phi * Mathf.Deg2Rad));
            radiusFeatureDetectorCompute.SetFloat("gravity_z", radius * Mathf.Cos(theta * Mathf.Deg2Rad));

            radiusFeatureDetectorCompute.Dispatch(radiusFeatureDetector_kh, camInfoList[cc].depth_width/8, camInfoList[cc].depth_height/8, 1);

            /*
            Debug.Log("depth width: " + camInfoList[cc].depth_width);
            Debug.Log("depth height: " + camInfoList[cc].depth_height);
            Debug.Log("result width: " + resultTexture[cc].width);
            Debug.Log("result height: " + resultTexture[cc].height);
            */

            result_display_list[cc].GetComponent<Renderer>().material.mainTexture = resultTexture[cc];
        }
        
    }


	




    void getCameraInfo()
    {
        int numCameras = camInfoList.Count;
        infoStruct[] infoArray = new infoStruct[numCameras];
        for (int cc = 0; cc < numCameras; cc++)
        {
            infoArray[cc].color_cx = camInfoList[cc].color_cx;
            infoArray[cc].color_cx = camInfoList[cc].color_cy;
            infoArray[cc].color_fx = camInfoList[cc].color_fx;
            infoArray[cc].color_fy = camInfoList[cc].color_fy;

            infoArray[cc].color_k1 = camInfoList[cc].color_k1;
            infoArray[cc].color_k2 = camInfoList[cc].color_k2;
            infoArray[cc].color_k3 = camInfoList[cc].color_k3;
            infoArray[cc].color_k4 = camInfoList[cc].color_k4;
            infoArray[cc].color_k5 = camInfoList[cc].color_k5;
            infoArray[cc].color_k6 = camInfoList[cc].color_k6;

            infoArray[cc].color_p1 = camInfoList[cc].color_p1;
            infoArray[cc].color_p2 = camInfoList[cc].color_p2;

            infoArray[cc].color_codx = camInfoList[cc].color_codx;
            infoArray[cc].color_cody = camInfoList[cc].color_cody;

            infoArray[cc].color_radius = camInfoList[cc].color_radius;


            infoArray[cc].depth_cx = camInfoList[cc].depth_cx;
            infoArray[cc].depth_cx = camInfoList[cc].depth_cy;
            infoArray[cc].depth_fx = camInfoList[cc].depth_fx;
            infoArray[cc].depth_fy = camInfoList[cc].depth_fy;

            infoArray[cc].depth_k1 = camInfoList[cc].depth_k1;
            infoArray[cc].depth_k2 = camInfoList[cc].depth_k2;
            infoArray[cc].depth_k3 = camInfoList[cc].depth_k3;
            infoArray[cc].depth_k4 = camInfoList[cc].depth_k4;
            infoArray[cc].depth_k5 = camInfoList[cc].depth_k5;
            infoArray[cc].depth_k6 = camInfoList[cc].depth_k6;

            infoArray[cc].depth_p1 = camInfoList[cc].depth_p1;
            infoArray[cc].depth_p2 = camInfoList[cc].depth_p2;

            infoArray[cc].depth_codx = camInfoList[cc].depth_codx;
            infoArray[cc].depth_cody = camInfoList[cc].depth_cody;

            infoArray[cc].depth_radius = camInfoList[cc].depth_radius;



            infoArray[cc].depthCameraToWorld = camInfoList[cc].visualization.transform.localToWorldMatrix;
            infoArray[cc].worldToDepthCamera = camInfoList[cc].visualization.transform.worldToLocalMatrix;

            infoArray[cc].camera_x = camInfoList[cc].visualization.transform.position.x;
            infoArray[cc].camera_y = camInfoList[cc].visualization.transform.position.y;
            infoArray[cc].camera_z = camInfoList[cc].visualization.transform.position.z;

            infoArray[cc].color_extrinsic = camInfoList[cc].color_extrinsics;

        }
        camInfoBuffer.SetData(infoArray);
    }


    public static int[] getRandomSet(int N, int set_size)
    {

        set_size = (int)Mathf.Min(set_size, N);

        List<int> idx_list = new List<int>();

        int counter = 1;
        int first = (int)(Random.value * ((float)N));
        idx_list.Add(first);

        bool go = true;
        while (go)
        {
            int idx = (int)(Random.value * ((float)N));
            if (idx_list.Contains(idx))
            {

            }
            else
            {
                idx_list.Add(idx);
                counter++;
            }

            if (counter == set_size)
            {
                go = false;
            }
        }

        return idx_list.ToArray();
    }


}


