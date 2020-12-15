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

public static class Graph {


    static float[,] distMatrix;
    static bool[,] validMatrix;
    //static bool[,] visitedMatrix;
    static List<List<int>> nodePathList;
    static float[] nodeDistList;
    static bool[] nodeActiveList;
    static bool[] nodeDestinationList;
    static int callDepth = 0;
    static int N;


    public static void initialize(int tempN)
    {
        N = tempN;
        distMatrix = new float[N,N];
        validMatrix = new bool[N, N];
        nodeDistList = new float[N];
        nodePathList = new List<List<int>>();
        nodeActiveList = new bool[N];
        nodeDestinationList = new bool[N];

        for(int i = 0; i<N; i++)
        {
            for(int j = 0; j<N; j++)
            {
                distMatrix[i, j] = 0.0f;
                validMatrix[i, j] = false;
                //visitedMatrix[i, j] = false;
            }
        }

        for(int i = 0; i<N; i++)
        {
            nodePathList.Add(new List<int>());
            nodeDistList[i] = float.PositiveInfinity;
            nodeActiveList[i] = true;
            nodeDestinationList[i] = false;
        }

    }

    public static void resetSearch()
    {
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                validMatrix[i, j] = false;
                //visitedMatrix[i, j] = false;
            }
        }

        for (int i = 0; i < N; i++)
        {
            nodePathList[i] = new List<int>();
            nodeDistList[i] = float.PositiveInfinity;
        }
    }

    public static void addDistance(int start, int stop, float dist)
    {
        //resetSearch();

        if(start != stop)
        {
            distMatrix[start, stop] = dist;
            validMatrix[start, stop] = true;

            distMatrix[stop, start] = dist;
            validMatrix[stop, start] = true;
        }
    }



    static float getNeighborDist(int start, int stop)
    {
        if (validMatrix[start, stop])
        {
            return distMatrix[start, stop];
        }
        else
        {
            return float.PositiveInfinity;
        }

    }

    static List<int> getNeighborList(int position, List<int> pathSoFar)
    {
        List<int> neighborList = new List<int>();

        for (int i = 0; i < N; i++) {
            if(i != position && !pathSoFar.Contains(i) && validMatrix[i,position])
            {
                neighborList.Add(i);
            }
        }
        
        return neighborList;
    }

    public static string intListToString(List<int> arr)
    {
        string output = "[";
        for (int i = 0; i < arr.Count; i++)
        {
            output += arr[i];
            if (i < arr.Count - 1)
            {
                output += ",";
            }
        }
        output += "]";
        return output;
    }

    public static string intListToString(int[] arr)
    {
        string output = "[";
        for(int i = 0; i<arr.Length; i++)
        {
            output += arr[i];
            if (i < arr.Length - 1)
            {
                output += ",";
            }
        }
        output += "]";
        return output;
    }

    static void dumpValidMatrix()
    {
        string output = "[";
        for(int i = 0; i<N; i++)
        {
            for(int j = 0; j<N; j++)
            {
                output += validMatrix[i, j];
                if (j < N - 1)
                {
                    output += ",";
                }

            }
            output += "\n";
        }
        output += "]";
        Debug.Log("valid matrix: " + output);
    }

    public static void getShortestDistance(int position, int destination, List<int> pathSoFar, out float finalDist, out List<int> path, out bool foundDestination)
    {
        if (!nodeActiveList[position])
        {

            path = nodePathList[position];
            finalDist = nodeDistList[position];
            foundDestination = nodeDestinationList[position];
            //Debug.Log("Checking node: " + position + " and it's dead with: " + foundDestination + " " + finalDist + " path: " + intListToString(path.ToArray()));
            return;
        }




        
        callDepth++;
        if(callDepth > 1000)
        {
            //Debug.Log("Reached final call depth!");
            path = new List<int>();
            finalDist = float.PositiveInfinity;
            foundDestination = false;
            return;
        }
        

        //Debug.Log("getting shortest distance from: " + position + " to: " + destination + ". Path tried so far: " + intListToString(pathSoFar.ToArray()));
        //dumpValidMatrix();

        path = new List<int>();
        foundDestination = false;

        //if you've reached the end
        if (position == destination)
        {
            //Debug.Log("reached the end!");
            finalDist = 0;
            path.Add(position);
            foundDestination = true;

            nodePathList[position] = path;
            nodeDistList[position] = finalDist;
            nodeActiveList[position] = true;
            nodeDestinationList[position] = true;
            return;
        }

        nodeActiveList[position] = false;

        //if you are before the end:
        List<int> neighborList = getNeighborList(position, pathSoFar);
        
        //Debug.Log("got neighborlist: " + intListToString(neighborList.ToArray()));

        pathSoFar.Add(position);


        //get neighborlist at position:
        int min_idx = -1;
        float min_dist = 0;
        float path_dist = 0.0f;
        List<int> bestPath = new List<int>();
        for (int i = 0; i < neighborList.Count; i++)
        {
            List<int> tempPath = new List<int>();
            float dist = 0;
            bool tempFoundDestination = false;

            if(neighborList[i] != position)
            {
                List<int> pathCopy = new List<int>();
                for (int qq = 0; qq < pathSoFar.Count; qq++)
                {
                    pathCopy.Add(pathSoFar[qq]);
                }
                getShortestDistance(neighborList[i], destination, pathCopy, out dist, out tempPath, out tempFoundDestination);
                //Debug.Log("Path from: " + neighborList[i] + " to: " + destination + " is: " + tempFoundDestination + " dist: " + dist + " through path: " + intListToString(tempPath.ToArray()));
            }

            
            if (tempFoundDestination)
            {
                foundDestination = true;
                
                float potential_dist = dist + getNeighborDist(position, neighborList[i]);
                if (potential_dist < min_dist || min_idx < 0)
                {
                    min_idx = i;
                    min_dist = potential_dist;
                    path_dist = dist;
                    bestPath = tempPath;
                }
            }

        }

        if (foundDestination)
        {
            //finalDist = min_dist + getNeighborDist(position, neighborList[min_idx]);
            finalDist = path_dist + getNeighborDist(position, neighborList[min_idx]);
            bestPath.Add(position);
            path = bestPath;

            //Debug.Log("Found path to destination from: " + position + " setting node path to: " + intListToString(bestPath.ToArray()));

            nodePathList[position] = path;
            nodeDistList[position] = finalDist;
            nodeDestinationList[position] = true;
            nodeActiveList[position] = false;

        }
        else
        {
            finalDist = min_dist;
            nodeActiveList[position] = true;
            //nodeActiveList[position] = false;
            //nodeDestinationList[position] = false;
        }



        List<int> tpath = new List<int>();
        for (int qq = 0; qq < path.Count; qq++)
        {
            tpath.Add(path[qq]);
        }

        path = tpath;


    }







}
