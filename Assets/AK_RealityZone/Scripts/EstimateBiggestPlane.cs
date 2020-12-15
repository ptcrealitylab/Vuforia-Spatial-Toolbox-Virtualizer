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
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;

public static class EstimateBiggestPlane {





    public static float inlierThreshold = 0.001f;
    public static float outlierThreshold = 0.01f;
    public static int ransac_iterations = 500;
    public static int best_num_inliers = 0;

    public struct planeData
    {
        public Vector3 point;
        public Vector3 normal;
        public int numInliers;
        public Vector3 inlierCentroid;
        public GameObject floorObject; //representative object
    }


    public static float getHeight(Vector3 point, planeData pd)
    {
        Vector3 diff = point - pd.point;
        float height = Vector3.Dot(diff, pd.normal.normalized);
        return height;
    }

    public static void identify_biggest_plane(Vector3[] points, out planeData pd, out int[] insideIdx, out int[] remainingIdx)
    {
        List<Vector3> remainingList = new List<Vector3>();


        pd = new planeData();

        List<planeData> planeIterationList = new List<planeData>();
        List<int> inlierCountIterationList = new List<int>();


        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        for (int iter = 0; iter < ransac_iterations; iter++)
        {
            int[] idx_array = getThree(points.Length);
            //find the plane:

            sw.Reset();
            sw.Start();
            estimatePlane(points, idx_array, out pd);
            sw.Stop();

            //Debug.Log("idx array: " + idx_array[0] + " " + idx_array[1] + " " + idx_array[2]);
            //Debug.Log("plane: " + pd.normal + " point " + pd.point);



            //Debug.Log("Time to estimate plane with: " + points.Length + " " + sw.ElapsedMilliseconds + " ms");
            //find the inliers:
            int[] inlier_idx;
            int[] outlier_idx;
            sw.Reset();
            sw.Start();
            findInliers(pd, points, out inlier_idx, out outlier_idx);
            sw.Stop();
            //Debug.Log("Time to find inliers: " + inlier_idx.Length + " " + sw.ElapsedMilliseconds + " ms");
            //recalculate the plane:
            //estimatePlane(points, inlier_idx, out pd);

            planeIterationList.Add(pd);
            inlierCountIterationList.Add(inlier_idx.Length);
        }


        //select best plane:
        int best_idx = 0;
        int best_count = inlierCountIterationList[0];
        for (int i = 0; i < planeIterationList.Count; i++)
        {
            if (inlierCountIterationList[i] > best_count)
            {
                best_idx = i;
                best_count = inlierCountIterationList[i];
            }
        }


        //print out the best inlier iteration list:
        string output = "";
        for(int i = 0; i< inlierCountIterationList.Count; i++)
        {
            output += "plane " + i + ": " + inlierCountIterationList[i] + "\n";
        }
        //Debug.Log(output);



        planeData bestPlane = planeIterationList[best_idx];
        pd = bestPlane;


        int[] inlier_idx_array;
        int[] outlier_idx_array;
        findInliers(bestPlane, points, out inlier_idx_array, out outlier_idx_array);
        insideIdx = inlier_idx_array;
        remainingIdx = outlier_idx_array;

        //Debug.Log("best plane is: " + best_idx + " new inliers: " + inlier_idx_array.Length);


        Vector3 inlierCentroid = new Vector3(0.0f, 0.0f, 0.0f);
        //insidePoints = new Vector3[inlier_idx_array.Length];
        for (int rr = 0; rr < inlier_idx_array.Length; rr++)
        {
            //insidePoints[rr] = points[inlier_idx_array[rr]];
            inlierCentroid = inlierCentroid + points[inlier_idx_array[rr]];
        }
        inlierCentroid = inlierCentroid / inlier_idx_array.Length;
        pd.inlierCentroid = inlierCentroid;
        best_num_inliers = inlier_idx_array.Length;

        /*
        remainingPoints = new Vector3[outlier_idx_array.Length];
        for (int rr = 0; rr < remainingPoints.Length; rr++)
        {
            remainingPoints[rr] = points[outlier_idx_array[rr]];
        }
        */


    }


    static void findInliers(planeData pd, Vector3[] points, out int[] inlierIdxArray, out int[] outlierIdxArray)
    {
        List<int> inlierList = new List<int>();
        List<int> outlierList = new List<int>();


        for (int i = 0; i < points.Length; i++)
        {
            float dist = Vector3.Dot((points[i] - pd.point), pd.normal);
            if (Mathf.Abs(dist) < inlierThreshold)
            {
                inlierList.Add(i);
            }

            if (Mathf.Abs(dist) > outlierThreshold)
            {
                outlierList.Add(i);
            }
        }



        inlierIdxArray = inlierList.ToArray();
        outlierIdxArray = outlierList.ToArray();


    }



    static void estimatePlane(Vector3[] points, int[] idx_array, out planeData pd)
    {
        pd = new planeData();
        Vector3[] point_array = new Vector3[idx_array.Length];
        for (int pp = 0; pp < point_array.Length; pp++)
        {
            point_array[pp] = points[idx_array[pp]];
            //Debug.Log("point array of pp: " + point_array[pp]);
        }
        Vector3 p0 = point_array[0];



        pd.point = p0;

        //estimate normal:
        Mat A = new Mat(idx_array.Length, 3, CvType.CV_64F, new Scalar(0.0));
        for (int pp = 0; pp < idx_array.Length; pp++)
        {
            A.put(pp, 0, (point_array[pp].x - p0.x));
            A.put(pp, 1, (point_array[pp].y - p0.y));
            A.put(pp, 2, (point_array[pp].z - p0.z));
        }


        Mat S = new Mat();
        Mat U = new Mat();
        Mat Vt = new Mat();
        Core.SVDecomp(A, S, U, Vt, 4);

        //Debug.Log("A: " + A.dump());
        //Debug.Log("U: " + U.dump());
        //Debug.Log("S: " + S.dump());
        //Debug.Log("Vt: " + Vt.dump());

        double Nx = Vt.get(2, 0)[0];
        double Ny = Vt.get(2, 1)[0];
        double Nz = Vt.get(2, 2)[0];

        pd.normal = new Vector3((float)Nx, (float)Ny, (float)Nz);



    }


    static int[] getThree(int N)
    {
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

            if (counter == 3)
            {
                go = false;
            }
        }

        return idx_list.ToArray();
    }

}
