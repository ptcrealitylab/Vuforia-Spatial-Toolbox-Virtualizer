/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//RANSAC class for unity
//written by Hisham Bedri, Reality Lab, 2019

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;


public static class RANSAC3D {

    public static int RANSAC_iterations = 200;
    public static float inlier_threshold = 0.1f;
    public static int best_inliers = 0;
    public static int[] best_start_idx;
    public static int[] best_stop_idx;

    
    public enum transform_mode_enum { in_plane, three_d };
    public static transform_mode_enum transform_mode = transform_mode_enum.three_d;

    public static bool debugMode = false;
    public static bool denseVisualizationMode = false;
    

    public static Quaternion best_quaternion;
    public static Vector3 best_transform;
    
    public static void ransacMatches(Vector3[] startPoints, Vector3[] stopPoints, out int[] key_idx, out int[] inlier_idx)
    {

        //test in plane:
        Vector3[] testStartX = new Vector3[5];
        Vector3[] testStopX = new Vector3[testStartX.Length];
        Quaternion testQ = Quaternion.AngleAxis(30.0f, Vector3.up);
        Vector3 testT = new Vector3(Random.value, 0.0f, Random.value);
        for(int i = 0; i<testStartX.Length; i++)
        {
            testStartX[i] = new Vector3(Random.value, Random.value, Random.value);
            testStopX[i] = testQ * testStartX[i] + testT;
        }
        Quaternion recoQ = new Quaternion();
        Vector3 recoT = new Vector3();
        estimateInPlaneTransform(testStartX, testStopX, out recoQ, out recoT);

        //Debug.Log("********************");
        //Debug.Log("********************");
        //Debug.Log("test for in plane transform. real Q: " + testQ + " recovered: " + recoQ + " real T: " + testT + " recovered: " + recoT);






        
        key_idx = new int[4];
        if (transform_mode == transform_mode_enum.three_d)
        {
            key_idx = new int[4];
        }
        if (transform_mode == transform_mode_enum.in_plane)
        {
            key_idx = new int[2];
        }


        inlier_idx = new int[0];

        Vector3[] projectedStopPoints = new Vector3[stopPoints.Length];

        int best_inlier_matches = 0;
        int best_iteration = -1;
        best_inliers = 0;

        for (int i = 0; i<RANSAC_iterations; i++)
        {
            int[] randomSet = getFour(startPoints.Length);
            if (transform_mode == transform_mode_enum.three_d)
            {
                randomSet = getFour(startPoints.Length);
            }
            if (transform_mode == transform_mode_enum.in_plane)
            {
                randomSet = getTwo(startPoints.Length);
            }
            

            if (debugMode)
            {
                if (i < 1)
                {
                    Debug.Log("***** seeding random set ****");
                    randomSet[0] = 0;
                    randomSet[1] = 1;
                    randomSet[2] = 2;
                    randomSet[3] = 3;
                }

            }

            
            

            //key_idx = randomSet; //FAIL?!?!




            Vector3[] startSubset = slice(startPoints, randomSet);
            Vector3[] stopSubset = slice(stopPoints, randomSet);

            Quaternion qr = Quaternion.identity;
            Vector3 tr = new Vector3();

            if (transform_mode == transform_mode_enum.three_d)
            {
                estimateTransform(startSubset, stopSubset, out qr, out tr);
            }
            if (transform_mode == transform_mode_enum.in_plane)
            {
                estimateInPlaneTransform(startSubset, stopSubset, out qr, out tr);
            }


            

            stopPoints.CopyTo(projectedStopPoints, 0);
            //project the points and count inliers.
            for (int pp = 0; pp < projectedStopPoints.Length; pp++)
            {
                //projectedStopPoints[pp] = Quaternion.Inverse(qr) * (projectedStopPoints[pp] - tr);
                projectedStopPoints[pp] = Quaternion.Inverse(qr) * (stopPoints[pp] - tr);
            }
            int[] random_inlier_idx = new int[0];
            int inliers = countInlierMatches(startPoints, projectedStopPoints, out random_inlier_idx);
            //Debug.Log("size of projected stop points: " + projectedStopPoints.Length + " start point 1: " + startPoints[0] + " projectedstop at 1: " + projectedStopPoints[0]);
            //Debug.Log("found " + inliers + " at iteration: " + i + " with set: [" + randomSet[0] + ", " + randomSet[1] + ", " + randomSet[2] + ", " + randomSet[3] + "]");
            if (inliers > best_inlier_matches)
            {
                //Debug.Log("found " + inliers + " inliers at iteration: " + i);
                best_inlier_matches = inliers;

                best_inliers = inliers;
                key_idx = randomSet;
                inlier_idx = random_inlier_idx;
                best_quaternion = qr;
                best_transform = tr;
                best_iteration = i;
            }

            if (debugMode)
            {
                if (i < 1)
                {
                    Debug.Log("** random set best inliers: " + inliers + " ****");
                    float angle = 0.0f;
                    Vector3 axis = new Vector3();
                    qr.ToAngleAxis(out angle, out axis);
                    Debug.Log("quaternion: angle: " + angle + " axis: " + axis + " translation: " + tr);
                }

            }

        }

        Debug.Log("RANSAC: Best number of inliers: " + best_inliers + " at iteration: " + best_iteration);
        string output_key = "";
        output_key += "[";
        for(int i = 0; i<key_idx.Length; i++)
        {
            output_key += key_idx[i] + ",";
        }
        output_key += "]";
        //Debug.Log("RANSAC: Best key idx: " + output_key);
        //Debug.Log("RANSAC: total number of matches: " + startPoints.Length);

        //re-estimate QR, TR using inlier set:

        Vector3[] startFinalSubset = slice(startPoints, inlier_idx);
        Vector3[] stopFinalSubset = slice(stopPoints, inlier_idx);


        //Debug.Log("best quaternion and transform before: " + best_quaternion + " " + best_transform);
        if (transform_mode == transform_mode_enum.three_d)
        {
            estimateTransform(startFinalSubset, stopFinalSubset, out best_quaternion, out best_transform);
        }
        if (transform_mode == transform_mode_enum.in_plane)
        {
            estimateInPlaneTransform(startFinalSubset, stopFinalSubset, out best_quaternion, out best_transform);
        }
        
        //Debug.Log("best quaternion and transform after final adjustment: " + best_quaternion + " " + best_transform);
    }



    static GameObject findObject(string name)
    {
        GameObject result = null;
        Object[] objects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
        for(int i = 0; i<objects.Length; i++)
        {
            if( ((GameObject)objects[i]).name == name)
            {
                result = ((GameObject)objects[i]);
            }
        }
        return result;
    }

    static void clearObjects(GameObject parent)
    {
        foreach(Transform t in parent.transform)
        {
            GameObject.Destroy(t.gameObject);
        }
    }


    public static void ransacMatchesDense(Vector3[] startPoints, Vector3[] stopPoints, Vector3[] validationStartPoints, Vector3[] validationStopPoints, out int[] key_idx, out int[] inlier_idx)
    {

        GameObject validationStartHolder;
        GameObject[] validationStartSphereArray = new GameObject[0];
        GameObject validationStopHolder;
        GameObject[] validationStopSphereArray;

        if (denseVisualizationMode)
        {

            validationStartHolder = new GameObject();
            validationStartSphereArray = new GameObject[0];
            validationStopHolder = new GameObject();
            validationStopSphereArray = new GameObject[0];





            validationStartHolder = findObject("validation_start_holder");
            if (validationStartHolder == null)
            {
                validationStartHolder = new GameObject();
            }
            clearObjects(validationStartHolder);

            validationStartSphereArray = new GameObject[validationStartPoints.Length];
            validationStartHolder.name = "validation_start_holder";
            for (int i = 0; i < validationStartPoints.Length; i++)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                point.name = "validation_start_" + i;
                point.transform.position = validationStartPoints[i];
                point.transform.parent = validationStartHolder.transform;
                point.GetComponent<Renderer>().material.color = Color.yellow;
                validationStartSphereArray[i] = point;
            }

            validationStopHolder = findObject("validation_stop_holder");
            if (validationStopHolder == null)
            {
                validationStopHolder = new GameObject();
            }
            clearObjects(validationStopHolder);

            validationStopSphereArray = new GameObject[validationStopPoints.Length];
            validationStopHolder.name = "validation_stop_holder";
            for (int i = 0; i < validationStopPoints.Length; i++)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                point.name = "validation_stop_" + i;
                point.transform.position = validationStopPoints[i];
                point.transform.parent = validationStopHolder.transform;
                point.GetComponent<Renderer>().material.color = Color.blue;
                validationStopSphereArray[i] = point;
            }


            //test in plane:
            Vector3[] testStartX = new Vector3[5];
            Vector3[] testStopX = new Vector3[testStartX.Length];
            Quaternion testQ = Quaternion.AngleAxis(30.0f, Vector3.up);
            Vector3 testT = new Vector3(Random.value, 0.0f, Random.value);
            for (int i = 0; i < testStartX.Length; i++)
            {
                testStartX[i] = new Vector3(Random.value, Random.value, Random.value);
                testStopX[i] = testQ * testStartX[i] + testT;
            }
            Quaternion recoQ = new Quaternion();
            Vector3 recoT = new Vector3();
            estimateInPlaneTransform(testStartX, testStopX, out recoQ, out recoT);

            //Debug.Log("********************");
            //Debug.Log("********************");
            //Debug.Log("test for in plane transform. real Q: " + testQ + " recovered: " + recoQ + " real T: " + testT + " recovered: " + recoT);

        }








        key_idx = new int[4];
        if (transform_mode == transform_mode_enum.three_d)
        {
            key_idx = new int[4];
        }
        if (transform_mode == transform_mode_enum.in_plane)
        {
            key_idx = new int[2];
        }

        inlier_idx = new int[0];


        int best_inlier_matches = 0;
        int best_iteration = -1;
        best_inliers = 0;
        float[] best_dist_array = new float[0];

        Vector3[] projectedStopPoints = new Vector3[0];
        int[] random_inlier_idx = new int[0];
        Vector3[] bestStopPoints = new Vector3[0];

        string[] best_dstring_array = new string[0];

        bool first_time = true;

        for (int i = 0; i < RANSAC_iterations; i++)
        {
            int[] randomSet = getFour(startPoints.Length);
            if (transform_mode == transform_mode_enum.three_d)
            {
                randomSet = getFour(startPoints.Length);
            }
            if (transform_mode == transform_mode_enum.in_plane)
            {
                randomSet = getTwo(startPoints.Length);
            }


            if (debugMode)
            {
                if (i < 1)
                {
                    Debug.Log("***** seeding random set ****");
                    randomSet[0] = 0;
                    randomSet[1] = 1;
                    randomSet[2] = 2;
                    randomSet[3] = 3;
                }

            }




            //key_idx = randomSet; //FAIL?!?!




            Vector3[] startSubset = slice(startPoints, randomSet);
            Vector3[] stopSubset = slice(stopPoints, randomSet);

            Quaternion qr = Quaternion.identity;
            Vector3 tr = new Vector3();

            if (transform_mode == transform_mode_enum.three_d)
            {
                estimateTransform(startSubset, stopSubset, out qr, out tr);
            }
            if (transform_mode == transform_mode_enum.in_plane)
            {
                estimateInPlaneTransform(startSubset, stopSubset, out qr, out tr);
            }




            projectedStopPoints = new Vector3[validationStopPoints.Length];
            validationStopPoints.CopyTo(projectedStopPoints, 0);
            //project the points and count inliers.
            for (int pp = 0; pp < projectedStopPoints.Length; pp++)
            {
                //projectedStopPoints[pp] = Quaternion.Inverse(qr) * (projectedStopPoints[pp] - tr);
                projectedStopPoints[pp] = Quaternion.Inverse(qr) * (validationStopPoints[pp] - tr);
            }
            random_inlier_idx = new int[0];
            float[] dist_array = new float[0];
            string[] dstring_array = new string[0];
            int inliers = countInlierMatchesDense(validationStartPoints, projectedStopPoints, out random_inlier_idx, out dist_array, out dstring_array);

            

            //Debug.Log("size of projected stop points: " + projectedStopPoints.Length + " start point 1: " + startPoints[0] + " projectedstop at 1: " + projectedStopPoints[0]);
            //Debug.Log("found " + inliers + " at iteration: " + i + " with set: [" + randomSet[0] + ", " + randomSet[1] + ", " + randomSet[2] + ", " + randomSet[3] + "]");
            if (inliers > best_inlier_matches || first_time)
            {
                first_time = false;
                //Debug.Log("found " + inliers + " inliers at iteration: " + i);
                best_inlier_matches = inliers;

                best_inliers = inliers;
                key_idx = randomSet;
                inlier_idx = random_inlier_idx;
                best_quaternion = qr;
                best_transform = tr;
                best_iteration = i;
                bestStopPoints = projectedStopPoints;
                best_dist_array = dist_array;
                best_dstring_array = dstring_array;
            }

            if (debugMode)
            {
                if (i < 1)
                {
                    Debug.Log("** random set best inliers: " + inliers + " ****");
                    float angle = 0.0f;
                    Vector3 axis = new Vector3();
                    qr.ToAngleAxis(out angle, out axis);
                    Debug.Log("quaternion: angle: " + angle + " axis: " + axis + " translation: " + tr);
                }

            }

        }



        if (denseVisualizationMode)
        {
            List<int> stuff = new List<int>();
            for (int i = 0; i < inlier_idx.Length; i++)
            {
                stuff.Add(inlier_idx[i]);
            }

            GameObject validationProjectedStopHolder = findObject("validation_projected_stop_holder");
            if (validationProjectedStopHolder == null)
            {
                validationProjectedStopHolder = new GameObject();
            }
            clearObjects(validationProjectedStopHolder);

            GameObject[] validationProjectedStopSphereArray = new GameObject[projectedStopPoints.Length];
            validationProjectedStopHolder.name = "validation_projected_stop_holder";
            for (int i = 0; i < bestStopPoints.Length; i++)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                point.name = "validation_projected_stop_" + i + "_" + best_dist_array[i];
                point.transform.position = bestStopPoints[i];
                point.transform.parent = validationProjectedStopHolder.transform;
                point.GetComponent<Renderer>().material.color = Color.green;
                validationProjectedStopSphereArray[i] = point;

                if (stuff.Contains(i))
                {
                    //point.GetComponent<Renderer>().material.color = Color.white;
                    //point.name = point.name + best_dstring_array[i];
                }

            }

            if(validationStartSphereArray != null)
            {
                
                for (int i = 0; i < validationStartSphereArray.Length; i++)
                {
                    if (stuff.Contains(i))
                    {
                        GameObject point = validationStartSphereArray[i];
                        point.GetComponent<Renderer>().material.color = Color.white;
                        point.name = point.name + best_dstring_array[i];
                    }
                }

            }
        }



        Debug.Log("best inliers using validation set: " + best_inliers + " out of: " + validationStartPoints.Length);




        Debug.Log("RANSAC: Best number of inliers: " + best_inliers + " at iteration: " + best_iteration);
        string output_key = "";
        output_key += "[";
        for (int i = 0; i < key_idx.Length; i++)
        {
            output_key += key_idx[i] + ",";
        }
        output_key += "]";
        //Debug.Log("RANSAC: Best key idx: " + output_key);
        //Debug.Log("RANSAC: total number of matches: " + startPoints.Length);




        //re-estimate QR, TR using inlier set:

        projectedStopPoints = new Vector3[stopPoints.Length];
        stopPoints.CopyTo(projectedStopPoints, 0);
        //project the points and count inliers.
        for (int pp = 0; pp < projectedStopPoints.Length; pp++)
        {
            //projectedStopPoints[pp] = Quaternion.Inverse(qr) * (projectedStopPoints[pp] - tr);
            projectedStopPoints[pp] = Quaternion.Inverse(best_quaternion) * (stopPoints[pp] - best_transform);
        }
        random_inlier_idx = new int[0];
        float[] ddist = new float[0];
        string[] dstring = new string[0];
        int dinliers = countInlierMatchesDense(startPoints, projectedStopPoints, out random_inlier_idx, out ddist, out dstring);
        


        Vector3[] startFinalSubset = slice(startPoints, random_inlier_idx);
        Vector3[] stopFinalSubset = slice(stopPoints, random_inlier_idx);

        Quaternion meh_quaternion = Quaternion.identity;
        Vector3 meh_transform = new Vector3();
        //Debug.Log("best quaternion and transform before: " + best_quaternion + " " + best_transform);
        if (transform_mode == transform_mode_enum.three_d)
        {
            estimateTransform(startFinalSubset, stopFinalSubset, out meh_quaternion, out meh_transform);
        }
        if (transform_mode == transform_mode_enum.in_plane)
        {
            estimateInPlaneTransform(startFinalSubset, stopFinalSubset, out meh_quaternion, out meh_transform);
        }

        inlier_idx = random_inlier_idx;

        //Debug.Log("best quaternion and transform after final adjustment: " + best_quaternion + " " + best_transform);


    }






    static int countInlierMatches(Vector3[] startPoints, Vector3[] projectedStopPoints, out int[] random_inlier_idx)
    {
        int inliers = 0;
        List<int> inlier_list = new List<int>();
        for(int i = 0; i<startPoints.Length; i++)
        {
            if (debugMode)
            {
                Debug.Log("RANSAC: inlier distance " + i + " is: " + (startPoints[i] - projectedStopPoints[i]).magnitude);
            }

            if ((startPoints[i] - projectedStopPoints[i]).magnitude < inlier_threshold)
            {
                inlier_list.Add(i);
                inliers++;
            }
        }

        random_inlier_idx = inlier_list.ToArray();
        return inliers;
    }

    static int countInlierMatchesDense(Vector3[] startPoints, Vector3[] projectedStopPoints, out int[] random_inlier_idx, out float[] dist_array, out string[] debug_array)
    {
        int inliers = 0;
        List<int> inlier_list = new List<int>();
        dist_array = new float[startPoints.Length];
        debug_array = new string[startPoints.Length];
        for (int i = 0; i < startPoints.Length; i++)
        {
            //find closest stop point:
            int min_idx = -1;
            float min_dist = 0.0f;
            for(int j = 0; j<projectedStopPoints.Length; j++)
            {
                float dist = (startPoints[i] - projectedStopPoints[j]).magnitude;
                if(dist < min_dist || min_idx < 0)
                {
                    min_idx = j;
                    min_dist = dist;
                }
            }

            if(min_dist < inlier_threshold)
            {
                inlier_list.Add(i);
                inliers++;
            }

            dist_array[i] = min_dist;
            if (denseVisualizationMode)
            {
                debug_array[i] = "mdist_" + min_dist + "_from_" + i + "_to_" + min_idx;
            }

        }

        random_inlier_idx = inlier_list.ToArray();
        return inliers;
    }



    public static void ransacTransform(Vector3[] startPoints, Vector3[] stopPoints, out Quaternion qe, out Vector3 te)
    {
        best_start_idx = new int[4];
        best_stop_idx = new int[4];
        qe = new Quaternion();
        te = new Vector3();


        for (int i = 0; i < RANSAC_iterations; i++) {

            Vector3[] projectedStopPoints = new Vector3[stopPoints.Length];
            stopPoints.CopyTo(projectedStopPoints, 0);

            int inliers = 0;

            int[] startIdx = new int[0];
            int[] stopIdx = new int[0];
            if(transform_mode == transform_mode_enum.three_d)
            {
                startIdx = getFour(startPoints.Length);
                stopIdx = getFour(stopPoints.Length);
            }
            if (transform_mode == transform_mode_enum.in_plane)
            {
                startIdx = getTwo(startPoints.Length);
                stopIdx = getTwo(stopPoints.Length);
            }


            Vector3[] startSubset = slice(startPoints, startIdx);
            Vector3[] stopSubset = slice(stopPoints, stopIdx);
            Quaternion qr = Quaternion.identity;
            Vector3 tr = new Vector3();

            if(transform_mode == transform_mode_enum.three_d)
            {
                estimateTransform(startSubset, stopSubset, out qr, out tr);
            }
            if(transform_mode == transform_mode_enum.in_plane)
            {
                estimateInPlaneTransform(startSubset, stopSubset, out qr, out tr);
            }
            

            //project the points and count inliers.
            for(int pp = 0; pp<projectedStopPoints.Length; pp++)
            {
                projectedStopPoints[pp] = Quaternion.Inverse(qr) * (projectedStopPoints[pp] - tr);
            }

            inliers = countInliers(startPoints, projectedStopPoints);

            if(inliers > best_inliers)
            {
                best_inliers = inliers;
                best_start_idx = startIdx;
                best_stop_idx = stopIdx;
                qe = qr;
                te = tr;
            }

        }
    }

    public static int countInliers(Vector3[] startPoints, Vector3[] stopPoints)
    {
        int inliers = 0;

        List<int> usedStopIdx = new List<int>();
        for(int i = 0; i<startPoints.Length; i++)
        {
            Vector3 start = startPoints[i];

            for(int j = 0; j<stopPoints.Length; j++)
            {
                if (!usedStopIdx.Contains(j))
                {
                    float dist = (start - stopPoints[j]).magnitude;
                    if (dist < inlier_threshold)
                    {
                        inliers++;
                        usedStopIdx.Add(j);
                        break;
                    }
                }
            }

        }

        return inliers;
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
        for(int i = 0; i<N; i++)
        {
            //Debug.Log("placing in B " + i);
            B.put((2*i + 0), 0, ((double)stop[i].x));
            B.put((2*i + 1), 0, ((double)stop[i].z));
        }

        //Debug.Log("B: " + B.dump());


        Mat A = new Mat(N * 2, 4, CvType.CV_64F, new Scalar(0.0f));
        for(int i = 0; i<N; i++)
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
        }




        

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

    public static Vector3[] slice(Vector3[] set, int[] sampleIdx)
    {
        Vector3[] result = new Vector3[sampleIdx.Length];
        for(int i = 0; i<sampleIdx.Length; i++)
        {
            result[i] = set[sampleIdx[i]]; //FAIL FAIL FAIL FAIL FAIL, OMG HOW DID YOU MISS THIS. //FIXED NOW.
        }
        return result;
    }


    public static int[] getTwo(int N)
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

            if (counter == 2)
            {
                go = false;
            }
        }

        return idx_list.ToArray();
    }


    public static int[] getFour(int N)
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

            if (counter == 4)
            {
                go = false;
            }
        }

        return idx_list.ToArray();
    }
}

