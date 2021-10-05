/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
//azure kinect to unity bridge
//written by Hisham Bedri, Reality Lab, 2019

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleJSON;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using AOT;
// using SocketIO;
using System.Threading;

//using OpenCVForUnity;

public class akplay : MonoBehaviour {


    public bool verbose = false;

    const string dllName = "AKPlugin119";

    static string filePath;
    static ReaderWriterLock locker = new ReaderWriterLock();

    private int lastDay = System.DateTime.Now.Day;

    //Plugin entry point
    #region
    [DllImport(dllName, EntryPoint = "enumerateDevices")]
    public static extern int enumerateDevices();

    [DllImport(dllName, EntryPoint = "getSerial")]
    public static extern int getSerial(int cameraIndex);

    [DllImport(dllName, EntryPoint = "openDevice")]
    public static extern int openDevice(int cameraIndex);

    [DllImport(dllName, EntryPoint = "startDevice")]
    public static extern int startDevice(int cameraIndex);

    [DllImport(dllName, EntryPoint = "startDeviceWithConfiguration")]
    public static extern int startDeviceWithConfiguration(int cameraIndex, int color_format, int color_resolution, int depth_mode, int camera_fps, bool synchronized_images_only, int depth_delay_off_color_usec, int wired_sync_mode, int subordinate_delay_of_master_usec, bool disable_streaming_indicator);

    [DllImport(dllName, EntryPoint = "startAllDevicesWithConfiguration")]
    public static extern int startAllDevicesWithConfiguration(int color_format, int color_resolution, int depth_mode, int camera_fps, bool synchronized_images_only, int depth_delay_off_color_usec, int wired_sync_mode, int subordinate_delay_of_master_usec, bool disable_streaming_indicator);

    [DllImport(dllName, EntryPoint = "registerBuffer")]
    public static extern int registerBuffer(int cameraIndex, IntPtr resultColorPointer, IntPtr resultDepthPointer, IntPtr resultSkeletonPointer);

    [DllImport(dllName, EntryPoint = "getLatestCapture")]
    public static extern int getLatestCapture(int cameraIndex, IntPtr colorBuffer, IntPtr depthBuffer, IntPtr skeletonBuffer);

    [DllImport(dllName, EntryPoint = "getLatestCaptureForAllCameras")]
    public static extern int getLatestCaptureForAllCameras();

    [DllImport(dllName, EntryPoint = "getCalibration")]
    //public static extern void getCalibration(int cameraIndex, int color_resolution, int depth_mode, float[] color_rotation, float[] color_translation, float[] color_intrinsics, float[] depth_rotation, float[] depth_translation, float[] depth_intrinsics);
    public static extern void getCalibration(int cameraIndex, int color_resolution, int depth_mode, IntPtr color_rotation, IntPtr color_translation, IntPtr color_intrinsics, IntPtr depth_rotation, IntPtr depth_translation, IntPtr depth_intrinsics);

    [DllImport(dllName, EntryPoint = "getXYZMap")]
    //public static extern void getCalibration(int cameraIndex, int color_resolution, int depth_mode, float[] color_rotation, float[] color_translation, float[] color_intrinsics, float[] depth_rotation, float[] depth_translation, float[] depth_intrinsics);
    public static extern int getXYZMap(int cameraIndex, IntPtr XYZMap);

    [DllImport(dllName, EntryPoint = "stopDevice")]
    public static extern int stopDevice(int cameraIndex);

    [DllImport(dllName, EntryPoint = "closeDevice")]
    public static extern int closeDevice(int cameraIndex);

    [DllImport(dllName, EntryPoint = "getFrame")]
    public static extern int getFrame(int cameraIndex, IntPtr resultColorPointer, IntPtr resultDepthPtr);

    [DllImport(dllName, EntryPoint = "cleanUp")]
    public static extern int cleanUp();

    [DllImport(dllName, EntryPoint = "setPrimaryTrackerIndex")]
    public static extern void setPrimaryTrackerIndex(int primaryTrackerIndex);

    [DllImport(dllName, EntryPoint = "setConfiguration")]
    public static extern void setConfiguration(int cameraIndex, int color_format, int color_resolution, int depth_mode, int camera_fps, bool synchronized_images_only, int depth_delay_off_color_usec, int wired_sync_mode, int subordinate_delay_of_master_usec, bool disable_streaming_indicator);

    [DllImport(dllName, EntryPoint = "startAllDevices")]
    public static extern int startAllDevices();

    [DllImport(dllName, EntryPoint = "getColorWidth")]
    public static extern int getColorWidth(int cameraIndex);

    [DllImport(dllName, EntryPoint = "getColorHeight")]
    public static extern int getColorHeight(int cameraIndex);

    [DllImport(dllName, EntryPoint = "getDepthWidth")]
    public static extern int getDepthWidth(int cameraIndex);

    [DllImport(dllName, EntryPoint = "getDepthHeight")]
    public static extern int getDepthHeight(int cameraIndex);




    [DllImport(dllName, EntryPoint = "doStuff")]
    public static extern int doStuff(IntPtr resultColorPtr);

    #endregion


    public GameObject[] visualizationArray;
    public GameObject visualizationPrefab;
    public Dictionary<uint, SkeletonVis>[] skeletonVisArray;
    public GameObject jointPrefab;
    public GameObject boringJointPrefab;
    public GameObject bonePrefab;
    public GameObject humanMarkerPrefab;
    public Shader AK_pointCloudShader;
    public ObjectPositionSender objectPositionSender;
    public MIRController mirController;

    public GameObject lineRendererPrefab;
    private Dictionary<uint, LineRenderer> lineRenderers = new Dictionary<uint, LineRenderer>();
    private List<LineRenderer> defunctLineRenderers = new List<LineRenderer>();


    private bool showTrackedSkeletons = true;
    private bool showTrackedLines = true;

    public bool camerasReady = false;

    public bool broadcastSkeletonData = false;

    public int primaryTrackerIndex = 3;

    //these enums are for configuration and match the definition in k4atypes.h
    //descriptions are copy-pasted from there
    #region
    public enum k4a_image_format_t
    {
        K4A_IMAGE_FORMAT_COLOR_MJPG = 0,
        K4A_IMAGE_FORMAT_COLOR_NV12,
        K4A_IMAGE_FORMAT_COLOR_YUY2,
        K4A_IMAGE_FORMAT_COLOR_BGRA32,
        K4A_IMAGE_FORMAT_DEPTH16,
        K4A_IMAGE_FORMAT_IR16,
        K4A_IMAGE_FORMAT_CUSTOM
    }

    public enum k4a_color_resolution_t
    {
        K4A_COLOR_RESOLUTION_OFF = 0, //**< Color camera will be turned off with this setting */
        K4A_COLOR_RESOLUTION_720P,    //**< 1280 * 720  16:9 */
        K4A_COLOR_RESOLUTION_1080P,   //**< 1920 * 1080 16:9 */
        K4A_COLOR_RESOLUTION_1440P,   //**< 2560 * 1440 16:9 */
        K4A_COLOR_RESOLUTION_1536P,   //**< 2048 * 1536 4:3  */
        K4A_COLOR_RESOLUTION_2160P,   //**< 3840 * 2160 16:9 */
        K4A_COLOR_RESOLUTION_3072P   //**< 4096 * 3072 4:3  */
    }

    public enum k4a_depth_mode_t
    {
        K4A_DEPTH_MODE_OFF = 0,        //**< Depth sensor will be turned off with this setting. */
        K4A_DEPTH_MODE_NFOV_2X2BINNED, //**< Depth captured at 320x288. Passive IR is also captured at 320x288. */
        K4A_DEPTH_MODE_NFOV_UNBINNED,  //**< Depth captured at 640x576. Passive IR is also captured at 640x576. */
        K4A_DEPTH_MODE_WFOV_2X2BINNED, //**< Depth captured at 512x512. Passive IR is also captured at 512x512. */
        K4A_DEPTH_MODE_WFOV_UNBINNED,  //**< Depth captured at 1024x1024. Passive IR is also captured at 1024x1024. */
        K4A_DEPTH_MODE_PASSIVE_IR     //**< Passive IR only, captured at 1024x1024. */
    }

    public enum k4a_fps_t
    {
        K4A_FRAMES_PER_SECOND_5 = 0, //**< 5 FPS */
        K4A_FRAMES_PER_SECOND_15,    //**< 15 FPS */
        K4A_FRAMES_PER_SECOND_30    //**< 30 FPS */
    }

    public enum k4a_wired_sync_mode_t
    {
        K4A_WIRED_SYNC_MODE_STANDALONE, //**< Neither 'Sync In' or 'Sync Out' connections are used. */
        K4A_WIRED_SYNC_MODE_MASTER,     //**< The 'Sync Out' jack is enabled and synchronization data it driven out the
                                        //connected wire.*/
        K4A_WIRED_SYNC_MODE_SUBORDINATE //**< The 'Sync In' jack is used for synchronization and 'Sync Out' is driven for the
                                        //next device in the chain. 'Sync Out' is a mirror of 'Sync In' for this mode.
                                        //*/
    }

    public enum k4abt_joint_id_t : int {
        K4ABT_JOINT_PELVIS = 0,
        K4ABT_JOINT_SPINE_NAVEL, // sdk has this as naval
        K4ABT_JOINT_SPINE_CHEST,
        K4ABT_JOINT_NECK,
        K4ABT_JOINT_CLAVICLE_LEFT,
        K4ABT_JOINT_SHOULDER_LEFT,
        K4ABT_JOINT_ELBOW_LEFT,
        K4ABT_JOINT_WRIST_LEFT,
        K4ABT_JOINT_HAND_LEFT,
        K4ABT_JOINT_HANDTIP_LEFT,
        K4ABT_JOINT_THUMB_LEFT,
        K4ABT_JOINT_CLAVICLE_RIGHT,
        K4ABT_JOINT_SHOULDER_RIGHT,
        K4ABT_JOINT_ELBOW_RIGHT,
        K4ABT_JOINT_WRIST_RIGHT,
        K4ABT_JOINT_HAND_RIGHT,
        K4ABT_JOINT_HANDTIP_RIGHT,
        K4ABT_JOINT_THUMB_RIGHT,
        K4ABT_JOINT_HIP_LEFT,
        K4ABT_JOINT_KNEE_LEFT,
        K4ABT_JOINT_ANKLE_LEFT,
        K4ABT_JOINT_FOOT_LEFT,
        K4ABT_JOINT_HIP_RIGHT,
        K4ABT_JOINT_KNEE_RIGHT,
        K4ABT_JOINT_ANKLE_RIGHT,
        K4ABT_JOINT_FOOT_RIGHT,
        K4ABT_JOINT_HEAD,
        K4ABT_JOINT_NOSE,
        K4ABT_JOINT_EYE_LEFT,
        K4ABT_JOINT_EAR_LEFT,
        K4ABT_JOINT_EYE_RIGHT,
        K4ABT_JOINT_EAR_RIGHT,
        K4ABT_JOINT_COUNT
    }

    public static readonly Dictionary<k4abt_joint_id_t, bool> boringJoints = new Dictionary<k4abt_joint_id_t, bool>()
    {
        {k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_HEAD, true },
        {k4abt_joint_id_t.K4ABT_JOINT_NOSE, true },
        {k4abt_joint_id_t.K4ABT_JOINT_EYE_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_EAR_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_EYE_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_EAR_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_PELVIS, true },
        {k4abt_joint_id_t.K4ABT_JOINT_FOOT_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_FOOT_RIGHT, true },

        {k4abt_joint_id_t.K4ABT_JOINT_HAND_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_THUMB_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_HANDTIP_LEFT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_HAND_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_THUMB_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_HANDTIP_RIGHT, true },
        {k4abt_joint_id_t.K4ABT_JOINT_SPINE_NAVEL, true },
    };

    public static readonly Dictionary<k4abt_joint_id_t, k4abt_joint_id_t> jointConnections = new Dictionary<k4abt_joint_id_t, k4abt_joint_id_t>() {
        { k4abt_joint_id_t.K4ABT_JOINT_HANDTIP_LEFT, k4abt_joint_id_t.K4ABT_JOINT_HAND_LEFT },
        // { k4abt_joint_id_t.K4ABT_JOINT_THUMB_LEFT, k4abt_joint_id_t.K4ABT_JOINT_HAND_LEFT},
        { k4abt_joint_id_t.K4ABT_JOINT_HAND_LEFT, k4abt_joint_id_t.K4ABT_JOINT_WRIST_LEFT },
        { k4abt_joint_id_t.K4ABT_JOINT_WRIST_LEFT, k4abt_joint_id_t.K4ABT_JOINT_ELBOW_LEFT },
        { k4abt_joint_id_t.K4ABT_JOINT_ELBOW_LEFT, k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_LEFT },
        // { k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_LEFT, k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_LEFT },
        // { k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_LEFT, k4abt_joint_id_t.K4ABT_JOINT_NECK },
        { k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_LEFT, k4abt_joint_id_t.K4ABT_JOINT_NECK },

        { k4abt_joint_id_t.K4ABT_JOINT_HANDTIP_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_HAND_RIGHT },
        // { k4abt_joint_id_t.K4ABT_JOINT_THUMB_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_HAND_RIGHT},
        { k4abt_joint_id_t.K4ABT_JOINT_HAND_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_WRIST_RIGHT },
        { k4abt_joint_id_t.K4ABT_JOINT_WRIST_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_ELBOW_RIGHT },
        { k4abt_joint_id_t.K4ABT_JOINT_ELBOW_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_RIGHT },
        // { k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_RIGHT },
        // { k4abt_joint_id_t.K4ABT_JOINT_CLAVICLE_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_NECK },
        { k4abt_joint_id_t.K4ABT_JOINT_SHOULDER_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_NECK },

        // { k4abt_joint_id_t.K4ABT_JOINT_EAR_LEFT, k4abt_joint_id_t.K4ABT_JOINT_EYE_LEFT },
        // { k4abt_joint_id_t.K4ABT_JOINT_EYE_LEFT, k4abt_joint_id_t.K4ABT_JOINT_NOSE },

        // { k4abt_joint_id_t.K4ABT_JOINT_EAR_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_EYE_RIGHT },
        // { k4abt_joint_id_t.K4ABT_JOINT_EYE_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_NOSE },

        // { k4abt_joint_id_t.K4ABT_JOINT_NOSE, k4abt_joint_id_t.K4ABT_JOINT_HEAD },
        // { k4abt_joint_id_t.K4ABT_JOINT_HEAD, k4abt_joint_id_t.K4ABT_JOINT_NECK },

        { k4abt_joint_id_t.K4ABT_JOINT_NECK, k4abt_joint_id_t.K4ABT_JOINT_SPINE_CHEST },
        { k4abt_joint_id_t.K4ABT_JOINT_SPINE_CHEST, k4abt_joint_id_t.K4ABT_JOINT_SPINE_NAVEL },
        { k4abt_joint_id_t.K4ABT_JOINT_SPINE_NAVEL, k4abt_joint_id_t.K4ABT_JOINT_PELVIS },

        { k4abt_joint_id_t.K4ABT_JOINT_FOOT_LEFT, k4abt_joint_id_t.K4ABT_JOINT_ANKLE_LEFT },
        { k4abt_joint_id_t.K4ABT_JOINT_ANKLE_LEFT, k4abt_joint_id_t.K4ABT_JOINT_KNEE_LEFT },
        { k4abt_joint_id_t.K4ABT_JOINT_KNEE_LEFT, k4abt_joint_id_t.K4ABT_JOINT_HIP_LEFT },
        { k4abt_joint_id_t.K4ABT_JOINT_HIP_LEFT, k4abt_joint_id_t.K4ABT_JOINT_PELVIS },

        { k4abt_joint_id_t.K4ABT_JOINT_FOOT_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_ANKLE_RIGHT },
        { k4abt_joint_id_t.K4ABT_JOINT_ANKLE_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_KNEE_RIGHT },
        { k4abt_joint_id_t.K4ABT_JOINT_KNEE_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_HIP_RIGHT },
        { k4abt_joint_id_t.K4ABT_JOINT_HIP_RIGHT, k4abt_joint_id_t.K4ABT_JOINT_PELVIS },
    };


    public enum k4abt_joint_confidence_level : int
    {
        K4ABT_JOINT_CONFIDENCE_NONE = 0,
        K4ABT_JOINT_CONFIDENCE_LOW,
        K4ABT_JOINT_CONFIDENCE_MEDIUM,
        K4ABT_JOINT_CONFIDENCE_HIGH,
        K4ABT_JOINT_CONFIDENCE_COUNT,
    }

    public struct AKJoint
    {
        public Vector3 position;
        public Quaternion orientation;
        public k4abt_joint_confidence_level confidence_level;
    }

    public struct AKSkeleton
    {
        public uint id;
        public AKJoint[] joints;
    }

    /* C#/c interop?????
    public struct k4abt_joint_t {
        public float x;
        public float y;
        public float z;
        public fixed float q[4];
    }
    
    public struct k4abt_skeleton_t {
      public fixed k4abt_joint_t joints[K4ABT_JOINT_COUNT];
    }
    */
    #endregion

    const int MAX_SKELETONS = 32; // should be impossible
    const uint MAX_BODY_IDS = 256; // Complete guess

    public k4a_color_resolution_t color_resolution = k4a_color_resolution_t.K4A_COLOR_RESOLUTION_1536P;
    public k4a_depth_mode_t depth_mode = k4a_depth_mode_t.K4A_DEPTH_MODE_NFOV_UNBINNED;
    public k4a_fps_t fps_mode = k4a_fps_t.K4A_FRAMES_PER_SECOND_15;

    public struct camInfo
    {
        /*
        public float depth_fx;
        public float depth_fy;
        public float depth_cx;
        public float depth_cy;
        public float color_fx;
        public float color_fy;
        public float color_cx;
        public float color_cy;
        public Matrix4x4 color_extrinsic;
        public Texture2D depthTexture;
        public Texture2D colorTexture;
        public byte[] depthTextureBytes;
        public Texture2D registeredTexture;
        public Matrix4x4 worldToCamera;
        public Matrix4x4 cameraToWorld;
        */

        public int serial;

        public int color_width;
        public int color_height;
        public int depth_width;
        public int depth_height;

        public GameObject colorCube;
        public GameObject depthCube;
        public GameObject distortionMapCube;

        public Texture2D colorTex;
        public Texture2D depthTex;
        public Texture2D distortionMapTex;

        public byte[] colorBytes;
        public byte[] depthBytes;

        public byte[] skeletonBytes;
        public AKSkeleton[] skeletons;

        public float[] XYZMap;
        public byte[] XYZMapBytes;

        public GCHandle colorHandle;
        public GCHandle depthHandle;
        public GCHandle skeletonHandle;

        //public GameObject registeredCube;

        public GameObject visualization;



        public float color_fx;
        public float color_fy;
        public float color_cx;
        public float color_cy;
        public float color_k1;
        public float color_k2;
        public float color_k3;
        public float color_k4;
        public float color_k5;
        public float color_k6;
        public float color_p1;
        public float color_p2;
        public float color_codx;
        public float color_cody;
        public float color_radius;
        public Matrix4x4 color_extrinsics;

        public float depth_fx;
        public float depth_fy;
        public float depth_cx;
        public float depth_cy;
        public float depth_k1;
        public float depth_k2;
        public float depth_k3;
        public float depth_k4;
        public float depth_k5;
        public float depth_k6;
        public float depth_p1;
        public float depth_p2;
        public float depth_codx;
        public float depth_cody;
        public float depth_radius;


    }

    public List<camInfo> camInfoList = new List<camInfo>();
    int numCameras = 0;



    // Use this for debug callback
    void OnEnable()
    {
        RegisterDebugCallback(OnDebugCallback);
    }

    public Texture2D jpgTex;

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    static extern void RegisterDebugCallback(debugCallback cb);


    int color_width = 0;
    int color_height = 0;
    int depth_width = 640;
    int depth_height = 576;

    void updateResolution()
    {
        if (verbose)
        {
            superDebug("inside update resolution");
            superDebug("color resolution int: " + (int)color_resolution);
        }

        if(color_resolution == k4a_color_resolution_t.K4A_COLOR_RESOLUTION_1080P)
        {
            if (verbose)
            {
                superDebug("1080p found");
            }
            color_width = 1920;
            color_height = 1080;
            //depth_width = color_width;
            //depth_height = color_height;
        }

        if (color_resolution == k4a_color_resolution_t.K4A_COLOR_RESOLUTION_720P)
        {
            if (verbose)
            {
                superDebug("720p found");
            }
            color_width = 1280;
            color_height = 720;
            //depth_width = color_width;
            //depth_height = color_height;
        }

        if (color_resolution == k4a_color_resolution_t.K4A_COLOR_RESOLUTION_2160P)
        {
            if (verbose)
            {
                superDebug("2160p found");
            }
            color_width = 3840;
            color_height = 2160;
            //depth_width = color_width;
            //depth_height = color_height;
        }
    }

    public float slider = 1.0f;


    public class SkeletonVis
    {
        public uint id;
        public GameObject[] joints;
        public GameObject[] bones;
        public GameObject humanMarker;
        public bool seen = true;
        public float coalesceSqrMag = COALESCE_RANGE;
        public uint coalescenceId = 0;
        // 37 is bad, 40-50 is iffy, 64 is perfect
        public int score = 0;
        public uint colorIndex = 0;

        public SkeletonVis(uint _id, GameObject jointPrefab, GameObject boringJointPrefab, GameObject bonePrefab, GameObject humanMarkerPrefab)
        {
            id = _id;
            joints = new GameObject[(int)k4abt_joint_id_t.K4ABT_JOINT_COUNT];
            bones = new GameObject[jointConnections.Count];
            for (int j = 0; j < (int)k4abt_joint_id_t.K4ABT_JOINT_COUNT; j++)
            {
                if (boringJoints.ContainsKey((k4abt_joint_id_t)j))
                {
                    joints[j] = GameObject.Instantiate(boringJointPrefab);
                }
                else
                {
                    joints[j] = GameObject.Instantiate(jointPrefab);
                }
            }
            for (int j = 0; j < jointConnections.Count; j++)
            {
                bones[j] = GameObject.Instantiate(bonePrefab);
            }
            humanMarker = GameObject.Instantiate(humanMarkerPrefab);
            SetActive(true);
        }

        public void Remove()
        {
            foreach (GameObject joint in joints)
            {
                Destroy(joint);
            }
            foreach (GameObject bone in bones)
            {
                Destroy(bone);
            }
            Destroy(humanMarker);
        }

        public void SetActive(bool active)
        {
            for (int j = 0; j < joints.Length; j++)
            {
                var joint = joints[j];
                if (boringJoints.ContainsKey((k4abt_joint_id_t)j))
                {
                    joint.SetActive(false);
                } else
                {
                    joint.SetActive(active);
                }
            }
            foreach (GameObject bone in bones)
            {
                bone.SetActive(active);
            }
            humanMarker.SetActive(active);

            if (active)
            {
                UpdateMarkerColor();
            }
        }

        public static readonly UnityEngine.Color[] markerColors = {
          /* pastels
            new UnityEngine.Color(0x4e / 255.0f, 0x79 / 255.0f, 0xa7 / 255.0f, 1),
            new UnityEngine.Color(0xf2 / 255.0f, 0x8e / 255.0f, 0x2c / 255.0f, 1),
            new UnityEngine.Color(0xe1 / 255.0f, 0x57 / 255.0f, 0x59 / 255.0f, 1),
            new UnityEngine.Color(0x76 / 255.0f, 0xb7 / 255.0f, 0xb2 / 255.0f, 1),
            new UnityEngine.Color(0x59 / 255.0f, 0xa1 / 255.0f, 0x4f / 255.0f, 1),
            new UnityEngine.Color(0xed / 255.0f, 0xc9 / 255.0f, 0x49 / 255.0f, 1),
            new UnityEngine.Color(0xaf / 255.0f, 0x7a / 255.0f, 0xa1 / 255.0f, 1),
            new UnityEngine.Color(0xff / 255.0f, 0x9d / 255.0f, 0xa7 / 255.0f, 1),
            new UnityEngine.Color(0x9c / 255.0f, 0x75 / 255.0f, 0x5f / 255.0f, 1),
            new UnityEngine.Color(0xba / 255.0f, 0xb0 / 255.0f, 0xab / 255.0f, 1)
          */
          /* Blues
          new UnityEngine.Color(0.27059f, 0.53725f, 1, 1),
          new UnityEngine.Color(0.24314f, 0.56863f, 1, 1),
          new UnityEngine.Color(0.21569f, 0.60000f, 1, 1),
          new UnityEngine.Color(0.18824f, 0.63137f, 1, 1),
          new UnityEngine.Color(0.16471f, 0.67059f, 1, 1),
          new UnityEngine.Color(0.13725f, 0.71373f, 1, 1),
          new UnityEngine.Color(0.10980f, 0.75686f, 1, 1),
          new UnityEngine.Color(0.08235f, 0.80392f, 1, 1),
          new UnityEngine.Color(0.05490f, 0.85490f, 1, 1),
          new UnityEngine.Color(0.02745f, 0.90980f, 1, 1),
          new UnityEngine.Color(0.00000f, 0.96863f, 1, 1),
          */
          // Better blues
          new UnityEngine.Color(0.70588f, 0.75686f, 1.00000f, 1),
          new UnityEngine.Color(0.64314f, 0.70980f, 1.00000f, 1),
          new UnityEngine.Color(0.58039f, 0.67451f, 1.00000f, 1),
          new UnityEngine.Color(0.51765f, 0.65098f, 1.00000f, 1),
          new UnityEngine.Color(0.45490f, 0.65098f, 1.00000f, 1),
          new UnityEngine.Color(0.39216f, 0.67451f, 1.00000f, 1),
          new UnityEngine.Color(0.32941f, 0.72157f, 1.00000f, 1),
          new UnityEngine.Color(0.26667f, 0.78431f, 1.00000f, 1),
          new UnityEngine.Color(0.20392f, 0.86275f, 1.00000f, 1),
          new UnityEngine.Color(0.14118f, 0.93725f, 1.00000f, 1),
          new UnityEngine.Color(0.07843f, 1.00000f, 0.99216f, 1),
          new UnityEngine.Color(0.01569f, 1.00000f, 0.93725f, 1),
          new UnityEngine.Color(0.00000f, 1.00000f, 0.90196f, 1),
          new UnityEngine.Color(0.00000f, 1.00000f, 0.88235f, 1),
          /* Red-yellows
          new UnityEngine.Color(1.00000f, 0.36863f, 0.36863f, 1),
          new UnityEngine.Color(1.00000f, 0.36471f, 0.31765f, 1),
          new UnityEngine.Color(1.00000f, 0.36863f, 0.27059f, 1),
          new UnityEngine.Color(1.00000f, 0.38039f, 0.21961f, 1),
          new UnityEngine.Color(1.00000f, 0.40000f, 0.16863f, 1),
          new UnityEngine.Color(1.00000f, 0.42353f, 0.12157f, 1),
          new UnityEngine.Color(1.00000f, 0.45490f, 0.07059f, 1),
          new UnityEngine.Color(1.00000f, 0.49412f, 0.01961f, 1),
          new UnityEngine.Color(1.00000f, 0.55294f, 0.00000f, 1),
          new UnityEngine.Color(1.00000f, 0.62353f, 0.00000f, 1),
          new UnityEngine.Color(1.00000f, 0.69412f, 0.00000f, 1),
          new UnityEngine.Color(1.00000f, 0.76078f, 0.00000f, 1),
          new UnityEngine.Color(1.00000f, 0.83137f, 0.00000f, 1),
          new UnityEngine.Color(1.00000f, 0.90196f, 0.00000f, 1),
           */
        };

        public bool coalesced()
        {
            return coalescenceId != 0;
        }

        public void Reset()
        {
            coalesceSqrMag = COALESCE_RANGE;
            coalescenceId = 0;
            seen = false;
        }

        private void UpdateMarkerColor()
        {
            // humanMarker.GetComponent<MeshRenderer>().material.SetColor("_Color", markerColors[colorIndex % markerColors.Length]);
        }
    }


    void adjustVisualizationArray(int numCameras)
    {
        //adjust the size of the visualization array if necessary:
        if (visualizationArray.Length != numCameras)
        {
            GameObject[] vizArrayTemp = new GameObject[numCameras];
            Dictionary<uint, SkeletonVis>[] skelVisArrayTemp = new Dictionary<uint, SkeletonVis>[numCameras];

            if (visualizationArray.Length < numCameras)
            {
                for (int i = 0; i < visualizationArray.Length; i++)
                {
                    vizArrayTemp[i] = visualizationArray[i];
                    skelVisArrayTemp[i] = skeletonVisArray[i];
                }
                for (int i = visualizationArray.Length; i < numCameras; i++)
                {
                    vizArrayTemp[i] = GameObject.Instantiate(visualizationPrefab);
                    vizArrayTemp[i].name = "Visualization_" + i;


                    skelVisArrayTemp[i] = new Dictionary<uint, SkeletonVis>();
                }
                visualizationArray = vizArrayTemp;
                skeletonVisArray = skelVisArrayTemp;
            }
            else
            {
                for (int i = 0; i < numCameras; i++)
                {
                    vizArrayTemp[i] = visualizationArray[i];
                    skelVisArrayTemp[i] = skeletonVisArray[i];
                }
                visualizationArray = vizArrayTemp;
                skeletonVisArray = skelVisArrayTemp;
            }
        }
    }



    // Use this for initialization
    void Start () {

        filePath = Application.dataPath + "/AKPlugin_result.txt";
        System.IO.File.WriteAllText(filePath, "");

        if (verbose)
        {
            superDebug("Enumerating devices...");
        }

        // superDebug("DEATH COMES: " + TestKinect_main());
        setPrimaryTrackerIndex(primaryTrackerIndex);

        int numCameras = enumerateDevices();
        for (int i = 0; i < numCameras; i++)
        {
            int result = -1;

            //open device
            result = openDevice(i);
            if (verbose)
            {
                superDebug("Opening device: " + i + " result: " + result);
                superDebug("serial number for camera " + i + ": " + getSerial(i));
            }

        }

        //adds visualization prefabs based on number of cameras!
        adjustVisualizationArray(numCameras);

        float step = 0.15f;
        for (int i = 0; i < numCameras; i++)
        {

            //set kinect configuration
            setConfiguration(i, (int)k4a_image_format_t.K4A_IMAGE_FORMAT_COLOR_BGRA32,
                                         (int)color_resolution,
                                         (int)depth_mode,
                                         (int)fps_mode,
                                         true,
                                         0,
                                         (int)k4a_wired_sync_mode_t.K4A_WIRED_SYNC_MODE_STANDALONE,
                                         0,
                                         false);

            camInfo ci = new camInfo();
            
            ci.serial = getSerial(i);
            Debug.Log("Camera " + i + ": serial- " + ci.serial);

            ci.color_width = getColorWidth(i);
            ci.color_height = getColorHeight(i);
            ci.depth_width = getDepthWidth(i);
            ci.depth_height = getDepthHeight(i);

            


            ci.colorCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ci.depthCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ci.distortionMapCube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            ci.colorTex = new Texture2D(ci.color_width, ci.color_height, TextureFormat.BGRA32, false);
            ci.depthTex = new Texture2D(ci.depth_width, ci.depth_height, TextureFormat.R16, false);
            ci.depthTex.filterMode = FilterMode.Point;
            ci.distortionMapTex = new Texture2D(ci.depth_width, ci.depth_height, TextureFormat.RGFloat, false);
            ci.distortionMapTex.filterMode = FilterMode.Point;


            ci.colorBytes = new byte[ci.color_width * ci.color_height * 4];
            ci.depthBytes = new byte[ci.depth_width * ci.depth_height * 2];
            // position quaternion confidence_level
            const uint SIZEOF_K4ABT_JOINT_T = sizeof(float) * (3 + 4) + sizeof(int);
            const uint SIZEOF_K4ABT_BODY_T = sizeof(int) + SIZEOF_K4ABT_JOINT_T * (int)k4abt_joint_id_t.K4ABT_JOINT_COUNT;
            ci.skeletonBytes = new byte[SIZEOF_K4ABT_BODY_T * MAX_SKELETONS];
            ci.skeletons = new AKSkeleton[MAX_SKELETONS];
            for (int ji = 0; ji < MAX_SKELETONS; ji++)
            {
                ci.skeletons[ji].joints = new AKJoint[(int)k4abt_joint_id_t.K4ABT_JOINT_COUNT];
            }
            if (verbose)
            {
                superDebug("setting color bytes length for camera: " + i + " to: " + ci.colorBytes.Length);
                superDebug("setting color width and height for camera: " + i + " to: " + ci.color_width + " " + ci.color_height);
                superDebug("standard multiplication: " + ci.color_width * ci.color_height * 4);
                superDebug("setting depth bytes length for camera: " + i + " to: " + ci.depthBytes.Length);
                superDebug("setting depth width and height for camera: " + i + " to: " + ci.depth_width + " " + ci.depth_height);
            }

            ci.colorHandle = GCHandle.Alloc(ci.colorBytes, GCHandleType.Pinned);
            ci.depthHandle = GCHandle.Alloc(ci.depthBytes, GCHandleType.Pinned);
            ci.skeletonHandle = GCHandle.Alloc(ci.skeletonBytes, GCHandleType.Pinned);

            ci.XYZMap = new float[ci.depth_width * ci.depth_height * 2];
            ci.XYZMapBytes = new byte[ci.depth_width * ci.depth_height * 8];


            ci.visualization = visualizationArray[i];
            ci.visualization.transform.position = new Vector3(i*3.0f, 0.0f, 0.0f);
            ci.visualization.GetComponent<AK_visualization>().colorTex = ci.colorTex;
            ci.visualization.GetComponent<AK_visualization>().depthTex = ci.depthTex;
            ci.visualization.GetComponent<AK_visualization>().XYMap = ci.distortionMapTex;

            //ci.visualization.GetComponent<AK_visualization>().mat =  new Material(AK_pointCloudShader);





            ci.colorCube.name = "ColorCube_" + i;
            ci.colorCube.layer = LayerMask.NameToLayer("Debug");
            ci.colorCube.transform.parent = gameObject.transform;
            ci.colorCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.colorCube.transform.localPosition = new Vector3(i * step, 0.0f, 0.0f);




            //camInfoList[i].colorCube.GetComponent<Renderer>().material.mainTexture = camInfoList[i].colorTexture;

            ci.depthCube.name = "DepthCube_" + i;
            ci.depthCube.layer = LayerMask.NameToLayer("Debug");
            ci.depthCube.transform.parent = gameObject.transform;
            ci.depthCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.depthCube.transform.localPosition = new Vector3(i * step, -step, 0.0f);
            ci.depthCube.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            //camInfoList[i].depthCube.GetComponent<Renderer>().material.mainTexture = camInfoList[i].depthTexture;
            ci.depthCube.GetComponent<Renderer>().material.SetFloat("_Distance", 0.1f);

            ci.distortionMapCube.name = "undistortedDepthCube_" + i;
            ci.distortionMapCube.layer = LayerMask.NameToLayer("Debug");
            ci.distortionMapCube.transform.parent = gameObject.transform;
            ci.distortionMapCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.distortionMapCube.transform.localPosition = new Vector3(i * step, -2 * step, 0.0f);
            ci.distortionMapCube.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            ci.distortionMapCube.GetComponent<Renderer>().material.SetFloat("_Distance", 0.1f);

            ci.colorTex.wrapMode = TextureWrapMode.Clamp;
            ci.depthTex.wrapMode = TextureWrapMode.Clamp;
            ci.distortionMapTex.wrapMode = TextureWrapMode.Clamp;

            ci.colorCube.GetComponent<Renderer>().material.mainTexture = ci.colorTex;
            ci.depthCube.GetComponent<Renderer>().material.mainTexture = ci.depthTex;
            ci.distortionMapCube.GetComponent<Renderer>().material.mainTexture = ci.distortionMapTex;





            ci.color_fx = 0;
            ci.color_fy = 0;
            ci.color_cx = 0;
            ci.color_cy = 0;
            ci.color_k1 = 0;
            ci.color_k2 = 0;
            ci.color_k3 = 0;
            ci.color_k4 = 0;
            ci.color_k5 = 0;
            ci.color_k6 = 0;
            ci.color_p1 = 0;
            ci.color_p2 = 0;
            ci.color_codx = 0;
            ci.color_cody = 0;
            ci.color_radius = 0;
            ci.color_extrinsics = new Matrix4x4();


            ci.depth_fx = 0;
            ci.depth_fy = 0;
            ci.depth_cx = 0;
            ci.depth_cy = 0;
            ci.depth_k1 = 0;
            ci.depth_k2 = 0;
            ci.depth_k3 = 0;
            ci.depth_k4 = 0;
            ci.depth_k5 = 0;
            ci.depth_k6 = 0;
            ci.depth_p1 = 0;
            ci.depth_p2 = 0;
            ci.depth_codx = 0;
            ci.depth_cody = 0;
            ci.depth_radius = 0;



            camInfoList.Add(ci);
        }
        if (verbose)
        {
            Debug.Log("finished setting up cam info list, with count: " + camInfoList.Count);
        }









        //updateResolution();

        //superDebug("setting color resolution to: " + color_width + " " + color_height);
        //superDebug("setting depth resolution to: " + depth_width + " " + depth_height);





        /*
        float step = 0.15f;
        for (int i = 0; i<numCameras; i++)
        {
            camInfo ci = new camInfo();
            ci.colorCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ci.depthCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ci.undistortedDepthCube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            ci.colorTex = new Texture2D(color_width,color_height,TextureFormat.BGRA32, false);
            ci.depthTex = new Texture2D(depth_width, depth_height, TextureFormat.R16, false);
            ci.depthTex.filterMode = FilterMode.Point;
            ci.undistortedDepthTex = new Texture2D(depth_width, depth_height, TextureFormat.RGFloat, false);
            ci.undistortedDepthTex.filterMode = FilterMode.Point;


            ci.colorBytes = new byte[color_width * color_height * 4];
            ci.depthBytes = new byte[depth_width * depth_height * 2];
            superDebug("setting color bytes length for camera: " + i +" to: " + ci.colorBytes.Length);
            superDebug("standard multiplication: " + color_width * color_height * 4);
            superDebug("setting depth bytes length for camera: " + i + " to: " + ci.depthBytes.Length);
            ci.colorHandle = GCHandle.Alloc(ci.colorBytes, GCHandleType.Pinned);
            ci.depthHandle = GCHandle.Alloc(ci.depthBytes, GCHandleType.Pinned);

            ci.XYZMap = new float[depth_width * depth_height * 2];
            ci.XYZMapBytes = new byte[depth_width * depth_height * 8];


            ci.visualization = visualizationArray[i];
            ci.visualization.GetComponent<AK_visualization>().colorTex = ci.colorTex;
            ci.visualization.GetComponent<AK_visualization>().depthTex = ci.depthTex;
            ci.visualization.GetComponent<AK_visualization>().XYMap = ci.undistortedDepthTex;

            //ci.visualization.GetComponent<AK_visualization>().mat =  new Material(AK_pointCloudShader);





            ci.colorCube.name = "ColorCube_" + i;
            ci.colorCube.transform.parent = gameObject.transform;
            ci.colorCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.colorCube.transform.localPosition = new Vector3(i * step, 0.0f, 0.0f);




            //camInfoList[i].colorCube.GetComponent<Renderer>().material.mainTexture = camInfoList[i].colorTexture;

            ci.depthCube.name = "DepthCube_" + i;
            ci.depthCube.transform.parent = gameObject.transform;
            ci.depthCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.depthCube.transform.localPosition = new Vector3(i * step, -step, 0.0f);
            ci.depthCube.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            //camInfoList[i].depthCube.GetComponent<Renderer>().material.mainTexture = camInfoList[i].depthTexture;
            ci.depthCube.GetComponent<Renderer>().material.SetFloat("_Distance", 0.1f);

            ci.undistortedDepthCube.name = "undistortedDepthCube_" + i;
            ci.undistortedDepthCube.transform.parent = gameObject.transform;
            ci.undistortedDepthCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            ci.undistortedDepthCube.transform.localPosition = new Vector3(i * step, -2 * step, 0.0f);
            ci.undistortedDepthCube.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            ci.undistortedDepthCube.GetComponent<Renderer>().material.SetFloat("_Distance", 0.1f);

            ci.colorCube.GetComponent<Renderer>().material.mainTexture = ci.colorTex;
            ci.depthCube.GetComponent<Renderer>().material.mainTexture = ci.depthTex;
            ci.undistortedDepthCube.GetComponent<Renderer>().material.mainTexture = ci.undistortedDepthTex;

            

            camInfoList.Add(ci);
        }
        */

        





        //register the buffers:
        for(int i = 0; i<camInfoList.Count; i++)
        {
            if (verbose)
            {
                superDebug("Attempting to register buffer for camera: " + i);
            }
            registerBuffer(i, camInfoList[i].colorHandle.AddrOfPinnedObject(), camInfoList[i].depthHandle.AddrOfPinnedObject(), camInfoList[i].skeletonHandle.AddrOfPinnedObject());

        }




        //get some calibration info:
        for (int i = 0; i < camInfoList.Count; i++)
        {
            float[] color_rotation = new float[9];
            float[] color_translation = new float[3];
            float[] color_intrinsics = new float[15];

            float[] depth_rotation = new float[9];
            float[] depth_translation = new float[3];
            float[] depth_intrinsics = new float[15];

            GCHandle color_rotation_h = GCHandle.Alloc(color_rotation, GCHandleType.Pinned);
            GCHandle color_translation_h = GCHandle.Alloc(color_translation, GCHandleType.Pinned);
            GCHandle color_intrinsics_h = GCHandle.Alloc(color_intrinsics, GCHandleType.Pinned);

            GCHandle depth_rotation_h = GCHandle.Alloc(depth_rotation, GCHandleType.Pinned);
            GCHandle depth_translation_h = GCHandle.Alloc(depth_translation, GCHandleType.Pinned);
            GCHandle depth_intrinsics_h = GCHandle.Alloc(depth_intrinsics, GCHandleType.Pinned);



            getCalibration(i,
                (int)color_resolution,
                (int)k4a_depth_mode_t.K4A_DEPTH_MODE_NFOV_UNBINNED,
                color_rotation_h.AddrOfPinnedObject(),
                color_translation_h.AddrOfPinnedObject(),
                color_intrinsics_h.AddrOfPinnedObject(),
                depth_rotation_h.AddrOfPinnedObject(),
                depth_translation_h.AddrOfPinnedObject(),
                depth_intrinsics_h.AddrOfPinnedObject());

            color_rotation_h.Free();
            color_translation_h.Free();
            color_intrinsics_h.Free();
            depth_rotation_h.Free();
            depth_translation_h.Free();
            depth_intrinsics_h.Free();

            if (verbose)
            {
                superDebug("color_rotation " + i + ": " + dumpArray(color_rotation));
                superDebug("color_translation " + i + ": " + dumpArray(color_translation));
                superDebug("color_intrinsics " + i + ": " + dumpArray(color_intrinsics));
                superDebug("depth_rotation " + i + ": " + dumpArray(depth_rotation));
                superDebug("depth_translation " + i + ": " + dumpArray(depth_translation));
                superDebug("depth_intrinsics " + i + ": " + dumpArray(depth_intrinsics));
            }


            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_cx = color_intrinsics[0];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_cy = color_intrinsics[1];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_fx = color_intrinsics[2];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_fy = color_intrinsics[3];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k1 = color_intrinsics[4];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k2 = color_intrinsics[5];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k3 = color_intrinsics[6];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k4 = color_intrinsics[7];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k5 = color_intrinsics[8];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_k6 = color_intrinsics[9];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_codx = color_intrinsics[10];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_cody = color_intrinsics[11];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_p2 = color_intrinsics[12];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_p1 = color_intrinsics[13];
            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_metric_radius = color_intrinsics[14];


            Matrix4x4 colorExtrinsics = new Matrix4x4();

            colorExtrinsics.SetColumn(0, new Vector4(color_rotation[0], color_rotation[1], color_rotation[2]));

            colorExtrinsics.SetColumn(1, new Vector4(color_rotation[3], color_rotation[4], color_rotation[5]));
            colorExtrinsics.SetColumn(2, new Vector4(color_rotation[6], color_rotation[7], color_rotation[8]));
            colorExtrinsics = colorExtrinsics.transpose; //turns out it was row major hahaha

            colorExtrinsics.SetColumn(3, new Vector4(color_translation[0] / 1000.0f, color_translation[1] / 1000.0f, color_translation[2] / 1000.0f, 1.0f));

            

            camInfoList[i].visualization.GetComponent<AK_visualization>().cameraInfo.color_extrinsics = colorExtrinsics;

            camInfo ci = camInfoList[i];
            ci.color_cx = color_intrinsics[0];
            ci.color_cx = color_intrinsics[0];
            ci.color_cy = color_intrinsics[1];
            ci.color_fx = color_intrinsics[2];
            ci.color_fy = color_intrinsics[3];
            ci.color_k1 = color_intrinsics[4];
            ci.color_k2 = color_intrinsics[5];
            ci.color_k3 = color_intrinsics[6];
            ci.color_k4 = color_intrinsics[7];
            ci.color_k5 = color_intrinsics[8];
            ci.color_k6 = color_intrinsics[9];
            ci.color_codx = color_intrinsics[10];
            ci.color_cody = color_intrinsics[11];
            ci.color_p2 = color_intrinsics[12];
            ci.color_p1 = color_intrinsics[13];
            ci.color_radius = color_intrinsics[14];
            ci.color_extrinsics = colorExtrinsics;

            ci.depth_cx = depth_intrinsics[0];
            ci.depth_cx = depth_intrinsics[0];
            ci.depth_cy = depth_intrinsics[1];
            ci.depth_fx = depth_intrinsics[2];
            ci.depth_fy = depth_intrinsics[3];
            ci.depth_k1 = depth_intrinsics[4];
            ci.depth_k2 = depth_intrinsics[5];
            ci.depth_k3 = depth_intrinsics[6];
            ci.depth_k4 = depth_intrinsics[7];
            ci.depth_k5 = depth_intrinsics[8];
            ci.depth_k6 = depth_intrinsics[9];
            ci.depth_codx = depth_intrinsics[10];
            ci.depth_cody = depth_intrinsics[11];
            ci.depth_p2 = depth_intrinsics[12];
            ci.depth_p1 = depth_intrinsics[13];
            ci.depth_radius = depth_intrinsics[14];


            camInfoList[i] = ci;

            if (verbose)
            {
                superDebug("Attempting to get XYZMap");
            }

            GCHandle XYZMap_h = GCHandle.Alloc(camInfoList[i].XYZMap, GCHandleType.Pinned);
            getXYZMap(i, XYZMap_h.AddrOfPinnedObject());
            Buffer.BlockCopy(camInfoList[i].XYZMap, 0, camInfoList[i].XYZMapBytes, 0, camInfoList[i].XYZMapBytes.Length);

            /*
            for (int jj = 0; jj < 1000; jj++) {
                int row = (int)Mathf.Floor(jj / depth_width);
                int col = (int)jj % depth_width;
                UnityEngine.Color c = new UnityEngine.Color(camInfoList[i].XYZMap[2*jj] / slider, camInfoList[i].XYZMap[2*jj + 1] / slider, 0.0f);
                camInfoList[i].undistortedDepthTex.SetPixel(col, row, c);

                Debug.Log("Index: " + jj + " xyzmap row: " + row + " " + col + " xval: " + camInfoList[i].XYZMap[2 * jj] + " yval: " + camInfoList[i].XYZMap[2 * jj + 1]);
            }
            */

            camInfoList[i].distortionMapTex.LoadRawTextureData(camInfoList[i].XYZMapBytes);
            camInfoList[i].distortionMapTex.Apply();

        }



        //start all the camera threads:
        //see here for more details on the parameters: https://microsoft.github.io/Azure-Kinect-Sensor-SDK/master/structk4a__device__configuration__t.html
        if (verbose)
        {
            superDebug("Attempting to start all the camera threads");
        }

        startAllDevices(); //this assumes, the devices have been enumerated, opened, the buffers registered, and the configuration set... in that order

        /*
        startAllDevicesWithConfiguration((int)k4a_image_format_t.K4A_IMAGE_FORMAT_COLOR_BGRA32,
                                         (int)color_resolution,
                                         (int)k4a_depth_mode_t.K4A_DEPTH_MODE_NFOV_UNBINNED,
                                         (int)k4a_fps_t.K4A_FRAMES_PER_SECOND_30,
                                         true,
                                         0,
                                         (int)k4a_wired_sync_mode_t.K4A_WIRED_SYNC_MODE_STANDALONE,
                                         0,
                                         false);
                                         */






        /*
        for (int i = 0; i<10; i++)
        {
            //do some jpg bandwidth tests!
            string jpgPath = "F:/RealityBeast/test/ak_play/Assets/bigjpg.jpg";
            byte[] bytes = System.IO.File.ReadAllBytes(jpgPath);
            jpgTex = new Texture2D(2, 2);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Reset();
            sw.Start();
            jpgTex.LoadImage(bytes);
            sw.Stop();
            Debug.Log("time to load 4k jpg: " + (double)sw.ElapsedTicks / (double)System.TimeSpan.TicksPerMillisecond);

        }
        */







        /*
        byte[] colorFreezeBytes = new byte[(int)2160 * 3840 * 4];
        GCHandle colorHandle = GCHandle.Alloc(colorFreezeBytes, GCHandleType.Pinned);
        
        int result = doStuff(colorHandle.AddrOfPinnedObject());
        Debug.Log("result: " + result);


        GameObject debug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Texture2D imTex = new Texture2D(3840, 2160, TextureFormat.BGRA32, false);
        imTex.LoadRawTextureData(colorFreezeBytes);
        imTex.Apply();
        debug.GetComponent<Renderer>().material.mainTexture = imTex;
        */
    }


    string dumpArray(float[] arr)
    {
        string output = "[";
        for(int i=0; i<arr.Length; i++)
        {

            output += arr[i];
            if (i != arr.Length - 1)
            {
                output += ",";
            }
        }
        output += "]";
        return output;
    }

    public float fps = 30.0f;
    float lastTime = 0.0f;


    // Update is called once per frame
    void Update () {
        //Debug.Log("************* setting cameras ready to true");
        camerasReady = true;

        if (System.DateTime.Now.Day != lastDay)
        {
            lastDay = System.DateTime.Now.Day;
            ResetLines();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetLines();
        }

        if (Input.GetKeyDown(KeyCode.S) && visualizationArray.Length > 0)
        {
            ToggleVisualizations();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleTracking();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleLines();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleLines();
        }

        if ((Time.time-lastTime) > (1.0f / fps))
        {
            lastTime = Time.time;


            //return;
            //superDebug("Attempting to get latest capture for all cameras");
            System.Diagnostics.Stopwatch sw2 = new System.Diagnostics.Stopwatch();
            sw2.Reset();
            sw2.Start();


            getLatestCaptureForAllCameras();


            sw2.Stop();
            //superDebug("Unity: getting latest capture in: " + sw2.ElapsedTicks / System.TimeSpan.TicksPerMillisecond + " ms. Size of color buffer: " + camInfoList[0].colorBytes.Length + " size of depth buffer: " + camInfoList[0].depthBytes.Length);
            //Debug.Log("Time to retreive frame: " + sw2.ElapsedTicks / System.TimeSpan.TicksPerMillisecond + " ms");


            //for(int i = 0; i<1; i++)
            for (int i = 0; i < camInfoList.Count; i++)
            {
                int result = -1;
                //get frame

                
                //superDebug("attempting to get frame: " + i);
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

                
                //sw.Reset();
                //sw.Start();


                //result = getFrame(i, camInfoList[i].colorHandle.AddrOfPinnedObject(), camInfoList[i].depthHandle.AddrOfPinnedObject());
                //superDebug("Unity: getting latest capture, size of color buffer: " + camInfoList[i].colorBytes.Length + " size of depth buffer: " + camInfoList[i].depthBytes.Length);
                //result = getLatestCapture(i, camInfoList[i].colorHandle.AddrOfPinnedObject(), camInfoList[i].depthHandle.AddrOfPinnedObject());

                //sw.Stop();
                //Debug.Log("Time to retreive frame " + i + ": " + sw.ElapsedTicks / System.TimeSpan.TicksPerMillisecond + " ms");
                

                //superDebug("Copying textures over: " + i);
                //copy it over


                sw.Reset();
                sw.Start();
                camInfoList[i].colorTex.LoadRawTextureData(camInfoList[i].colorBytes);
                sw.Stop();
                //Debug.Log("Time to load raw color " + i + ": " + (double)sw.ElapsedTicks / (double)System.TimeSpan.TicksPerMillisecond + " ms");

                sw.Reset();
                sw.Start();
                camInfoList[i].colorTex.Apply();
                sw.Stop();
                //Debug.Log("Time to apply raw color " + i + ": " + (double)sw.ElapsedTicks / (double)System.TimeSpan.TicksPerMillisecond + " ms");

                sw.Reset();
                sw.Start();
                camInfoList[i].depthTex.LoadRawTextureData(camInfoList[i].depthBytes);
                sw.Stop();
                //Debug.Log("Time to load raw depth " + i + ": " + (double)sw.ElapsedTicks / (double)System.TimeSpan.TicksPerMillisecond + " ms");

                sw.Reset();
                sw.Start();
                camInfoList[i].depthTex.Apply();
                sw.Stop();
                //Debug.Log("Time to apply raw depth " + i + ": " + (double)sw.ElapsedTicks / (double)System.TimeSpan.TicksPerMillisecond + " ms");

                //superDebug("getting frame for device: " + i + " result: " + result);

                /*
                colorTex.LoadRawTextureData(colorBytes);
                colorTex.Apply();
                camInfoList[i].colorCube.GetComponent<Renderer>().material.mainTexture = colorTex;
                depthTex.LoadRawTextureData(depthBytes);
                depthTex.Apply();
                camInfoList[i].depthCube.GetComponent<Renderer>().material.mainTexture = depthTex;
                */



                //resize on a compute shader:

                ReadSkeletons(i);
            }
            HideExtraSkeletonVisualizations();
            AddPointsToHumanMarkerLines();
        }
    }

    private void ReadSkeletons(int i)
    {
        var reader = new BinaryReader(new MemoryStream(camInfoList[i].skeletonBytes));
        int skelI = 0;
        foreach (KeyValuePair<uint, SkeletonVis> entry in skeletonVisArray[i]) {
            entry.Value.Reset();
        }
        for (skelI = 0; skelI < MAX_SKELETONS; skelI++)
        {
            uint id = reader.ReadUInt32();
            if (id == 0)
            {
                break;
            }

            camInfoList[i].skeletons[skelI].id = id;

            for (int j = 0; j < (int)k4abt_joint_id_t.K4ABT_JOINT_COUNT; j++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                float qw = reader.ReadSingle();
                float qx = reader.ReadSingle();
                float qy = reader.ReadSingle();
                float qz = reader.ReadSingle();
                int confidence = reader.ReadInt32();

                const float mmToM = 0.001f;
                camInfoList[i].skeletons[skelI].joints[j].position = new Vector3(x * mmToM, -y * mmToM, z * mmToM);
                camInfoList[i].skeletons[skelI].joints[j].orientation = new Quaternion(qw, qx, qy, qz);
                camInfoList[i].skeletons[skelI].joints[j].confidence_level = (k4abt_joint_confidence_level)confidence;
            }
            if (!skeletonVisArray[i].ContainsKey(id))
            {
                skeletonVisArray[i].Add(id, new SkeletonVis(id, jointPrefab, boringJointPrefab, bonePrefab, humanMarkerPrefab));
            }
            skeletonVisArray[i][id].seen = true;
            updateSkeletonVis(camInfoList[i].visualization, camInfoList[i].skeletons[skelI], skeletonVisArray[i][id]);
        }

        var unseenSkeletons = new List<uint>();

        foreach (KeyValuePair<uint, SkeletonVis> entry in skeletonVisArray[i])
        {
            if (!entry.Value.seen)
            {
                entry.Value.Remove();
                unseenSkeletons.Add(entry.Key);
            }
        }

        foreach (uint id in unseenSkeletons)
        {
            skeletonVisArray[i].Remove(id);
        }

        if (broadcastSkeletonData)
        {
            SendSkeletonData();
        }
    }

    private const float COALESCE_RANGE = 0.75f * 0.75f;

    private void HideExtraSkeletonVisualizations() {
        if (skeletonVisArray == null)
        {
            return;
        }
        // For each skeletonVis check other camera's skeletonVis joints for
        // intersection and hide all with lower score
        // After each check mark all considered skeletons as coalesced
        for (int i = 0; i < skeletonVisArray.Length; i++)
        {
            var mainSkelVis = skeletonVisArray[i];
            Dictionary<uint, List<SkeletonVis>> allCoalescences = new Dictionary<uint, List<SkeletonVis>>();

            foreach (KeyValuePair<uint, SkeletonVis> entry in skeletonVisArray[i])
            {
                var mainSV = entry.Value;
                if (mainSV.coalesced())
                {
                    continue;
                }

                mainSV.coalescenceId = mainSV.id;
                allCoalescences[mainSV.id] = new List<SkeletonVis>() { mainSV };

                for (int j = i + 1; j < skeletonVisArray.Length; j++)
                {
                    foreach (KeyValuePair<uint, SkeletonVis> otherEntry in skeletonVisArray[j])
                    {
                        var otherSV = otherEntry.Value;

                        var diff = mainSV.joints[(int)k4abt_joint_id_t.K4ABT_JOINT_PELVIS].transform.position -
                            otherSV.joints[(int)k4abt_joint_id_t.K4ABT_JOINT_PELVIS].transform.position;

                        var sqrMag = diff.sqrMagnitude;

                        if (otherSV.coalesceSqrMag < sqrMag)
                        {
                            continue;
                        }

                        otherSV.coalesceSqrMag = sqrMag;
                        otherSV.coalescenceId = mainSV.id;

                        allCoalescences[mainSV.id].Add(otherSV);
                    }
                }
                
            }

            foreach (KeyValuePair<uint, List<SkeletonVis>> entry in allCoalescences)
            {
                var coalescenceId = entry.Key;
                var coalescence = entry.Value;
                if (coalescence.Count == 0)
                {
                    continue;
                }

                uint colorIndex = 0;
                foreach (var skelVis in coalescence)
                {
                    if (skelVis.colorIndex != 0 && skelVis.coalescenceId == coalescenceId)
                    {
                        colorIndex = skelVis.colorIndex;
                    }
                }
                if (colorIndex == 0)
                {
                    colorIndex = MAX_BODY_IDS * (uint)i + coalescenceId;
                    coalescence[0].colorIndex = colorIndex;
                }

                var highestScoreI = 0;
                for (int c = 1; c < coalescence.Count; c++)
                {
                    var otherSV = coalescence[c];
                    // otherSV may have been stolen by a more-conflicting (closer) coalescence
                    if (otherSV.coalescenceId != coalescenceId)
                    {
                        continue;
                    }
                    otherSV.colorIndex = colorIndex;
                    if (otherSV.score > coalescence[highestScoreI].score)
                    {
                        highestScoreI = c;
                    }
                }

                for (int c = 0; c < coalescence.Count; c++)
                {
                    if (c == highestScoreI)
                    {
                        coalescence[c].SetActive(true);
                    } else
                    {
                        coalescence[c].SetActive(false);
                    }
                }
            }
        }
    }

    private void AddPointsToHumanMarkerLines()
    {
        Dictionary<uint, LineRenderer> activeLineRenderers = new Dictionary<uint, LineRenderer>();

        foreach (var skelVisArr in skeletonVisArray)
        {
            foreach (var entry in skelVisArr)
            {
                var skelVis = entry.Value;
                if (!skelVis.humanMarker.activeSelf)
                {
                    continue;
                }

                if (!lineRenderers.ContainsKey(skelVis.colorIndex))
                {
                    var lrGO = GameObject.Instantiate(lineRendererPrefab);
                    lineRenderers[skelVis.colorIndex] = lrGO.GetComponent<LineRenderer>();
                    // var color = SkeletonVis.markerColors[skelVis.colorIndex % SkeletonVis.markerColors.Length];
                    var hue = 140 + (skelVis.colorIndex * 6.7f % 30.0f);
                    var color = UnityEngine.Color.HSVToRGB(hue / 360.0f, 0.8f, 1.0f);
                    color.a = 0.5f;
                    lineRenderers[skelVis.colorIndex].material.SetColor("_Color", color);
                }
                var lr = lineRenderers[skelVis.colorIndex];
                lr.positionCount += 1;
                var linePos = skelVis.humanMarker.transform.position;
                // linePos.y = 0.1f;
                lr.SetPosition(lr.positionCount - 1, linePos);
                activeLineRenderers[skelVis.colorIndex] = lr;

                if (!showTrackedLines)
                {
                    lr.gameObject.SetActive(showTrackedLines);
                }
                if (!showTrackedSkeletons)
                {
                    skelVis.SetActive(showTrackedSkeletons);
                }
            }
        }
        
        foreach (var entry in lineRenderers) {
            if (!activeLineRenderers.ContainsKey(entry.Key))
            {
                defunctLineRenderers.Add(entry.Value);
            }
        }
        lineRenderers = activeLineRenderers;
    }

    private void updateSkeletonVis(GameObject cameraVis, AKSkeleton skeleton, SkeletonVis vis)
    {
        vis.score = 0;

        const float jointSize = 0.02f;
        for (int j = 0; j < (int)k4abt_joint_id_t.K4ABT_JOINT_COUNT; j++)
        {
            vis.joints[j].transform.parent = cameraVis.transform;
            vis.joints[j].transform.localPosition = skeleton.joints[j].position;
            vis.score += (int)skeleton.joints[j].confidence_level;
        }

        int boneI = 0;
        foreach (var connection in jointConnections)
        {
            float correction = jointSize * 2;
            var bone = vis.bones[boneI];
            var jointAPos = vis.joints[(int)connection.Key].transform.position;
            var jointBPos = vis.joints[(int)connection.Value].transform.position;
            var avgPosition = jointAPos + jointBPos;
            avgPosition.Scale(new Vector3(0.5f, 0.5f, 0.5f));
            var diff = jointBPos - jointAPos;

            if (boringJoints.ContainsKey(connection.Key))
            {
                correction -= jointSize;
                avgPosition -= diff.normalized * jointSize;
            }
            if (boringJoints.ContainsKey(connection.Value))
            {
                correction -= jointSize;
                avgPosition += diff.normalized * jointSize;
            }
            bone.transform.position = avgPosition;
            bone.transform.up = diff;
            float boneSize = 0.016f;
            bone.transform.localScale = new Vector3(
                boneSize,
                Mathf.Max((jointBPos - jointAPos).magnitude / 2.0f - correction, 0.01f),
                boneSize);
            boneI += 1;
        }

        var head = vis.joints[(int)k4abt_joint_id_t.K4ABT_JOINT_HEAD];
        vis.humanMarker.transform.position = head.transform.position;
        vis.humanMarker.transform.position = vis.humanMarker.transform.position + new Vector3(0, 0.4f, 0);

        for (int j = 0; j < (int)k4abt_joint_id_t.K4ABT_JOINT_COUNT; j++)
        {
            vis.joints[j].transform.parent = null;
        }
        // Debug.Log("score: " + vis.score);
    }

    public void ResetLines() {
        foreach (var defunctLine in defunctLineRenderers)
        {
        GameObject.Destroy(defunctLine.gameObject);
        }
        defunctLineRenderers.Clear();
        mirController.Clear();
    }

    public void ToggleVisualizations()
    {
        var newActive = !visualizationArray[0].activeSelf;
        SetShowVisualizations(newActive);
    }

    public void SetShowVisualizations(bool newActive)
    {
        foreach (var vis in visualizationArray)
        {
            vis.SetActive(newActive);
        }
    }

    public void ToggleTracking()
    {
        showTrackedLines = showTrackedSkeletons;
        ToggleSkeletons();
        ToggleLines();
    }

    public void ToggleSkeletons()
    {
        SetShowSkeletons(!showTrackedSkeletons);
    }

    public void SetShowSkeletons(bool newShowTrackedSkeletons)
    {
        showTrackedSkeletons = newShowTrackedSkeletons;
        foreach (var svArray in skeletonVisArray)
        {
            foreach (var entry in svArray)
            {
                entry.Value.SetActive(showTrackedSkeletons);
            }
        }
    }

    public void ToggleLines()
    {
        SetShowLines(!showTrackedLines);
    }

    public void SetShowLines(bool newShow)
    {
        showTrackedLines = newShow;
        foreach (var entry in lineRenderers)
        {
            entry.Value.gameObject.SetActive(showTrackedLines);
        }
        foreach (var lr in defunctLineRenderers)
        {
            lr.gameObject.SetActive(showTrackedLines);
        }
        mirController.SetActive(showTrackedLines);
    }

    private void SendSkeletonData()
    {
        if (!objectPositionSender || primaryTrackerIndex >= skeletonVisArray.Length)
        {
            return;
        }

        Dictionary<uint, SkeletonVis> skelVisses = skeletonVisArray[primaryTrackerIndex];
        JSONArray skeletons = new JSONArray();
        foreach (var entry in skelVisses)
        {
            uint id = entry.Key;
            SkeletonVis sv = entry.Value;
            JSONObject skeleton = new JSONObject();
            skeleton.Add("id", id);
            JSONArray joints = new JSONArray();
            foreach (var jointGO in sv.joints)
            {
                JSONObject joint = new JSONObject();
                joint.Add("x", jointGO.transform.position.x);
                joint.Add("y", jointGO.transform.position.y);
                joint.Add("z", jointGO.transform.position.z);
                joints.Add(joint);
            }
            skeleton.Add("joints", joints);
            skeletons.Add(skeleton);
        }

        if (mirController.Connected())
        {
            JSONObject skeleton = new JSONObject();
            skeleton.Add("id", "mir");
            JSONArray joints = new JSONArray();

            JSONObject joint = new JSONObject();
            joint.Add("x", mirController.currentPos.x);
            joint.Add("y", mirController.currentPos.y);
            joint.Add("z", mirController.currentPos.z);
            joint.Add("qw", mirController.currentOri.w);
            joint.Add("qx", mirController.currentOri.x);
            joint.Add("qy", mirController.currentOri.y);
            joint.Add("qz", mirController.currentOri.z);
            joints.Add(joint);

            skeleton.Add("joints", joints);
            skeletons.Add(skeleton);
        }

        objectPositionSender.SendSkeleton(skeletons);
    }

    private void OnApplicationQuit()
    {
        superDebug("cleaning up!");
        cleanUp();
    }

    void superDebug(string message)
    {
        Debug.Log("Unity-" + message);
        try
        {
            locker.AcquireWriterLock(int.MaxValue);
            System.IO.File.AppendAllText(filePath, message);
            System.IO.File.AppendAllText(filePath, "\n");
            /*
            var fStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            byte[] info = new System.Text.UTF8Encoding(true).GetBytes(debug_string);
            fStream.Write(info, 0, info.Length);
            info = new System.Text.UTF8Encoding(true).GetBytes("\n");
            fStream.Write(info, 0, info.Length);
            */
        }
        finally
        {
            locker.ReleaseWriterLock();
        }
    }

    //Create string param callback delegate
    delegate void debugCallback(IntPtr request, int color, int size);
    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(debugCallback))]
    static void OnDebugCallback(IntPtr request, int color, int size)
    {
        //Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // string filePath = Application.dataPath + "/ZedPluginLog.txt";
        //Debug.Log("filepath: " + filePath);
        //System.IO.File.AppendAllText(filePath, debug_string);
        //System.IO.File.AppendAllText(filePath, "\n");

        try
        {
            locker.AcquireWriterLock(int.MaxValue);
            System.IO.File.AppendAllText(filePath, debug_string);
            System.IO.File.AppendAllText(filePath, "\n");
            /*
            var fStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            byte[] info = new System.Text.UTF8Encoding(true).GetBytes(debug_string);
            fStream.Write(info, 0, info.Length);
            info = new System.Text.UTF8Encoding(true).GetBytes("\n");
            fStream.Write(info, 0, info.Length);
            */
        }
        finally
        {
            locker.ReleaseWriterLock();
        }



        //Add Specified Color
        debug_string =
            String.Format("{0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(),
            ">",
            debug_string,
            "</color>"
            );


        UnityEngine.Debug.Log(debug_string);
    }
}
