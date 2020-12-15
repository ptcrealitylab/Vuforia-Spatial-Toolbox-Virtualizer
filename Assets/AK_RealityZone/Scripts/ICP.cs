/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//ICP class for unity
//written by Hisham Bedri, Reality Lab, 2019

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;


public static class ICP
{
    static ComputeShader icp_shader;
    static GameObject debugCube;

    static RenderTexture rt;
    static float result_scale = 1.0f;
    static float neighbor_threshold = 0.1f;

    public static bool in_plane_only = false;

    public static void initializeICPClass(ComputeShader ICP, GameObject debugCubeToLoad, float result_scale_to_load, float neighbor_threshold_to_load)
    {
        icp_shader = ICP;
        debugCube = debugCubeToLoad;
        result_scale = result_scale_to_load;
        neighbor_threshold = neighbor_threshold_to_load;
    }


    public static void ICP_iteration(Vector3[] startPoints, Vector3[] stopPoints, out Quaternion Qe, out Vector3 Te, out Vector3[] projectedStopPoints, out int[] match_idx_array)
    {
        //Debug.Log("ICP iteration!");
        int N = startPoints.Length;
        if(stopPoints.Length != N)
        {
            Debug.Log("ICP ITERATION CANT WORK BECAUSE START POINT LENGHT AND STOP POINT LENGTH ARE NOT THE SAME");
        }


        Qe = Quaternion.identity;
        Te = new Vector3();
        projectedStopPoints = new Vector3[stopPoints.Length];

        ComputeBuffer start_point_buffer = new ComputeBuffer(startPoints.Length, 3 * sizeof(float));
        ComputeBuffer stop_point_buffer = new ComputeBuffer(stopPoints.Length, 3 * sizeof(float));
        start_point_buffer.SetData(startPoints);
        stop_point_buffer.SetData(stopPoints);

        int matchImage_kh = icp_shader.FindKernel("matchImage");
        icp_shader.SetBuffer(matchImage_kh, "start_point_buffer", start_point_buffer);
        icp_shader.SetBuffer(matchImage_kh, "stop_point_buffer", stop_point_buffer);
        icp_shader.SetFloat("result_scale", result_scale);


        if (rt == null || rt.width != N || rt.width != N)
        {
            rt = new RenderTexture(N, N, 24, RenderTextureFormat.ARGBFloat);
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Point;
            rt.Create();
        }

        icp_shader.SetTexture(matchImage_kh, "match_tex", rt);

        icp_shader.Dispatch(matchImage_kh, ((int)(N / 8)) + 1, ((int)(N / 8)) + 1, 1);

        debugCube.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));
        debugCube.GetComponent<Renderer>().material.mainTexture = rt;
        




        //match points using matchImage:
        int matchPoints_kh = icp_shader.FindKernel("matchPoints");
        ComputeBuffer match_idx_buffer = new ComputeBuffer(startPoints.Length, sizeof(int));
        icp_shader.SetBuffer(matchPoints_kh, "start_point_buffer", start_point_buffer);
        icp_shader.SetBuffer(matchPoints_kh, "stop_point_buffer", stop_point_buffer);
        icp_shader.SetBuffer(matchPoints_kh, "match_idx_buffer", match_idx_buffer);
        icp_shader.SetTexture(matchPoints_kh, "match_tex", rt);
        icp_shader.SetFloat("neighbor_threshold", neighbor_threshold);
        icp_shader.SetInt("num_points", startPoints.Length);

        icp_shader.Dispatch(matchPoints_kh, (startPoints.Length / 64) + 1, 1, 1);

        match_idx_array = new int[startPoints.Length];
        match_idx_buffer.GetData(match_idx_array);

        List<Vector3> startMatchList = new List<Vector3>();
        List<Vector3> stopMatchList = new List<Vector3>();
        List<Vector2> matchIdxList = new List<Vector2>();
        for(int i = 0; i<startPoints.Length; i++)
        {
            if(match_idx_array[i] >= 0)
            {
                startMatchList.Add(startPoints[i]);
                stopMatchList.Add(stopPoints[match_idx_array[i]]);
                matchIdxList.Add(new Vector2(i, match_idx_array[i]));
            }
        }

        /*
        Debug.Log("Found Neighbor matches: " + startMatchList.Count);
        for(int i = 0; i<startMatchList.Count; i++)
        {
            Debug.Log("Point " + i + ": dist: " + (startMatchList[i] - stopMatchList[i]).magnitude + " midx: " + matchIdxList[i] + " p1: " + startMatchList[i] + " p2: " + stopMatchList[i]);
        }
        */

        if (ICP.in_plane_only)
        {
            estimateInPlaneTransform(startMatchList.ToArray(), stopMatchList.ToArray(), out Qe, out Te);
        }
        else
        {
            estimateTransform(startMatchList.ToArray(), stopMatchList.ToArray(), out Qe, out Te);
        }



        //Debug.Log("estimated Q: " + Qe + " Estimated Te: " + Te);

        //reproject stop points:
        Matrix4x4 projection = Matrix4x4.TRS(Te, Qe, new Vector3(1.0f, 1.0f, 1.0f));
        //projection = Matrix4x4.TRS(new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity, new Vector3(1.0f, 1.0f, 1.0f));
        //Debug.Log("projection matrix: " + projection);

        int projectPoints_kh = icp_shader.FindKernel("projectPoints");
        ComputeBuffer reprojected_stop_point_buffer = new ComputeBuffer(stopPoints.Length, 3 * sizeof(float));
        icp_shader.SetBuffer(projectPoints_kh, "stop_point_buffer", stop_point_buffer);
        icp_shader.SetBuffer(projectPoints_kh, "reprojected_stop_point_buffer", reprojected_stop_point_buffer);
        icp_shader.SetMatrix("matFromStopToStart", projection.inverse);

        icp_shader.Dispatch(projectPoints_kh, (stopPoints.Length / 64) + 1, 1, 1);

        reprojected_stop_point_buffer.GetData(projectedStopPoints);

        start_point_buffer.Dispose();
        stop_point_buffer.Dispose();
        reprojected_stop_point_buffer.Dispose();
        match_idx_buffer.Dispose();
    }



    public static void estimateTransform(Vector3[] start, Vector3[] stop, out Quaternion qr, out Vector3 tr)
    {
        qr = new Quaternion();
        tr = new Vector3();


        int N = start.Length;

        Mat x_start = new Mat(3, N, CvType.CV_64F, new Scalar(0.0));
        Mat x_stop = new Mat(3, N, CvType.CV_64F, new Scalar(0.0));

        Mat x_start_centroid = new Mat(3, 1, CvType.CV_64F, new Scalar(0.0));
        Mat x_stop_centroid = new Mat(3, 1, CvType.CV_64F, new Scalar(0.0));

        Mat x_start_m = new Mat(3, N, CvType.CV_64F, new Scalar(0.0));
        Mat x_stop_m = new Mat(3, N, CvType.CV_64F, new Scalar(0.0));

        double val = 0.0f;
        for (int i = 0; i < N; i++)
        {
            x_start.put(0, i, start[i].x);
            x_start.put(1, i, start[i].y);
            x_start.put(2, i, start[i].z);


            val = (double)start[i].x + x_start_centroid.get(0, 0)[0];
            x_start_centroid.put(0, 0, val);
            val = (double)start[i].y + x_start_centroid.get(1, 0)[0];
            x_start_centroid.put(1, 0, val);
            val = (double)start[i].z + x_start_centroid.get(2, 0)[0];
            x_start_centroid.put(2, 0, val);



            x_stop.put(0, i, stop[i].x);
            x_stop.put(1, i, stop[i].y);
            x_stop.put(2, i, stop[i].z);

            val = (double)stop[i].x + x_stop_centroid.get(0, 0)[0];
            x_stop_centroid.put(0, 0, val);
            val = (double)stop[i].y + x_stop_centroid.get(1, 0)[0];
            x_stop_centroid.put(1, 0, val);
            val = (double)stop[i].z + x_stop_centroid.get(2, 0)[0];
            x_stop_centroid.put(2, 0, val);

        }

        //find centroids:
        x_start_centroid = x_start_centroid / ((double)N);
        x_stop_centroid = x_stop_centroid / ((double)N);

        //find offset values:
        for (int i = 0; i < N; i++)
        {
            x_start_m.put(0, i, x_start.get(0, i)[0] - x_start_centroid.get(0, 0)[0]);
            x_start_m.put(1, i, x_start.get(1, i)[0] - x_start_centroid.get(1, 0)[0]);
            x_start_m.put(2, i, x_start.get(2, i)[0] - x_start_centroid.get(2, 0)[0]);

            x_stop_m.put(0, i, x_stop.get(0, i)[0] - x_stop_centroid.get(0, 0)[0]);
            x_stop_m.put(1, i, x_stop.get(1, i)[0] - x_stop_centroid.get(1, 0)[0]);
            x_stop_m.put(2, i, x_stop.get(2, i)[0] - x_stop_centroid.get(2, 0)[0]);
        }

        Mat H = x_start_m * x_stop_m.t();

        Mat S = new Mat();
        Mat U = new Mat();
        Mat Vt = new Mat();
        Core.SVDecomp(H, S, U, Vt, 4);

        double det = Core.determinant(Vt * U.t());
        Mat D = new Mat(3, 3, CvType.CV_64F, new Scalar(0.0));
        //D.put(0, 0, 1.0);
        //D.put(1, 1, det);
        //D.put(2, 2, 1.0);


        //faiiiiiiil:
        D.put(0, 0, 1.0);
        D.put(1, 1, 1.0);
        D.put(2, 2, det);


        Mat Rhat = Vt.t() * D * U.t();
        Mat That_all = x_stop - Rhat * x_start;

        Mat That = new Mat(3, 1, CvType.CV_64F, new Scalar(0.0));
        for (int i = 0; i < N; i++)
        {
            val = That.get(0, 0)[0] + That_all.get(0, i)[0];
            That.put(0, 0, val);

            val = That.get(1, 0)[0] + That_all.get(1, i)[0];
            That.put(1, 0, val);

            val = That.get(2, 0)[0] + That_all.get(2, i)[0];
            That.put(2, 0, val);
        }
        That = That / ((double)N);

        Vector3 right = new Vector3((float)Rhat.get(0, 0)[0], (float)Rhat.get(1, 0)[0], (float)Rhat.get(2, 0)[0]);
        Vector3 up = new Vector3((float)Rhat.get(0, 1)[0], (float)Rhat.get(1, 1)[0], (float)Rhat.get(2, 1)[0]);
        Vector3 forward = new Vector3((float)Rhat.get(0, 2)[0], (float)Rhat.get(1, 2)[0], (float)Rhat.get(2, 2)[0]);

        qr = Quaternion.LookRotation(forward, up);
        tr.x = (float)That.get(0, 0)[0];
        tr.y = (float)That.get(1, 0)[0];
        tr.z = (float)That.get(2, 0)[0];

    }


    public static void estimateInPlaneTransform(Vector3[] start, Vector3[] stop, out Quaternion qr, out Vector3 tr)
    {
        qr = Quaternion.identity;
        tr = new Vector3();


        int N = start.Length;
        /*
        Debug.Log("N: " + N);
        for(int i = 0; i<N; i++)
        {
            Debug.Log("i " + i + ": " + start[i] + ", " + stop[i]);
        }
        */


        Mat B = new Mat(N * 2, 1, CvType.CV_64F, new Scalar(0.0));
        for (int i = 0; i < N; i++)
        {
            //Debug.Log("placing in B " + i);
            B.put((2 * i + 0), 0, ((double)stop[i].x));
            B.put((2 * i + 1), 0, ((double)stop[i].z));
        }

        //Debug.Log("B: " + B.dump());


        Mat A = new Mat(N * 2, 4, CvType.CV_64F, new Scalar(0.0f));
        for (int i = 0; i < N; i++)
        {
            //Debug.Log("placing in A " + i);
            //row 1: [X -Z 1 0]
            A.put(2 * i + 0, 0, start[i].x);
            A.put(2 * i + 0, 1, -start[i].z);
            A.put(2 * i + 0, 2, 1);
            A.put(2 * i + 0, 3, 0);

            //row 2: [Z X 0 1]
            A.put(2 * i + 1, 0, start[i].z);
            A.put(2 * i + 1, 1, start[i].x);
            A.put(2 * i + 1, 2, 0);
            A.put(2 * i + 1, 3, 1);
        }
        //Debug.Log("A: " + A.dump());


        /*
        Mat B = new Mat(N * 2, 1, CvType.CV_64F, new Scalar(0.0));
        for (int i = 0; i < N; i++)
        {
            B.put(2 * N + 0, 0, start[i].x);
            B.put(2 * N + 1, 0, start[i].z);
        }


        Mat A = new Mat(N * 2, 4, CvType.CV_64F, new Scalar(0.0f));
        for (int i = 0; i < N; i++)
        {
            //row 1: [X -Z 1 0]
            A.put(2 * N + 0, 0, stop[i].x);
            A.put(2 * N + 0, 1, -stop[i].z);
            A.put(2 * N + 0, 2, 1);
            A.put(2 * N + 0, 3, 0);

            //row 2: [Z X 0 1]
            A.put(2 * N + 0, 0, stop[i].z);
            A.put(2 * N + 0, 1, stop[i].x);
            A.put(2 * N + 0, 2, 0);
            A.put(2 * N + 0, 3, 1);
        }
        */



        try
        {
            Mat X = (A.t() * A).inv() * A.t() * B;

            double cos_theta = X.get(0, 0)[0];
            double sin_theta = X.get(1, 0)[0];
            double Tx = X.get(2, 0)[0];
            double Tz = X.get(3, 0)[0];

            Matrix4x4 in_plane_matrix = new Matrix4x4();
            in_plane_matrix.SetColumn(0, new Vector4((float)cos_theta, 0, (float)sin_theta, 0));
            in_plane_matrix.SetColumn(1, new Vector4(0, 1, 0, 0));
            in_plane_matrix.SetColumn(2, new Vector4(-(float)sin_theta, 0, (float)cos_theta, 0));
            in_plane_matrix.SetColumn(3, new Vector4((float)Tx, 0, (float)Tz, 1));

            /*
            Debug.Log("A: " + A.dump());
            Debug.Log("B: " + B.dump());
            Debug.Log("X: " + X.dump());
            Debug.Log("inplane matrix: " + in_plane_matrix);
            */

            Vector3 up_vec = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 forward_vec = new Vector3(-(float)sin_theta, 0.0f, (float)cos_theta);
            if (forward_vec != Vector3.zero && up_vec != Vector3.zero)
            {
                qr = Quaternion.LookRotation(forward_vec, up_vec);
                tr = new Vector3((float)Tx, 0.0f, (float)Tz);

            }
        }
        catch
        {
            Debug.Log("Problem inverting to get in-plane trasnform!");
            Debug.Log("Matrix A: " + A.dump());
            Debug.Log("Matrix B: " + B.dump());
            Debug.Log("Start points length: " + start.Length);
        }

    }

}
