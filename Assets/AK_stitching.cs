using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AK_stitching : MonoBehaviour {

    //external settings
    public float cubeWidth = 2.54f;
    public int numVoxels = 10;
    public bool[] maskArray;
    public int normalFilterSize=3;

    public float flying_pixel_tolerance = 0.1f;
    public float depth_debug_val = 0.0f;
    public int discontinuity_delta = 2;
    public float max_depth_discontinuity = 0.3f;

    //references:
    public GameObject AK_receiver;
    List<akplay.camInfo> camInfoList;
    public ComputeShader textureCubeCompute;
    public ComputeShader voxelCompute;

    //debugging:
    public GameObject debugCube;
    public RenderTexture debugTexture;
    public int test_idx = 0;
    public enum debugOutputModeEnum { none, color, depth, normal, distortion, position };
    public debugOutputModeEnum debugOutputMode = debugOutputModeEnum.color;
    public float distortion_multiplier = 1.0f;
    public float distortion_dimension = 1.0f; //positive is x, negative is y
    public float position_multiplier = 1.0f;


    //internal stuff
    uint[] voxelBytes;
    RenderTexture color_tex_cube;
    RenderTexture depth_tex_cube;
    RenderTexture distortion_tex_cube;
    RenderTexture normal_tex_cube;
    ComputeBuffer voxelBuffer;
    ComputeBuffer camInfoBuffer;

    bool render_ready = false;

    



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
    



    // Use this for initialization
    void Start () {

	}

    bool firstFrame = true;
	// Update is called once per frame
	void Update () {
        if (firstFrame && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            Debug.Log("cameras ready. running setup! " + AK_receiver.GetComponent<akplay>().camerasReady);
            firstFrame = false;
            setup();
        }

        if(firstFrame == false)
        {
            render_ready = false;
            getCameraInfo();
            makeTextureCubes();
            loadVoxels();
            render_ready = true;

        }


    }

    void setup()
    {
        //get camera info:
        Debug.Log("setup: " + AK_receiver.GetComponent<akplay>().camInfoList.Count);
        camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;

        //smart Cleanup
        takeDown();

        //setup texture cubes
        setupTextureCubes();

    }

    void setupTextureCubes()
    {
        Debug.Log("setting up texture cubes, cam info list count: " + camInfoList.Count);
        //camInfoBuffer = new ComputeBuffer(camInfoList.Count, 268);  //3 matrices and 19 floats: 3*64 + 19*4 = 268
        camInfoBuffer = new ComputeBuffer(camInfoList.Count, 328);  //3 matrices and 19 floats: 3*64 + 34*4 = 328

        depth_tex_cube = new RenderTexture(camInfoList[0].depth_width, camInfoList[0].depth_height, 24, RenderTextureFormat.RFloat);
        depth_tex_cube.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        depth_tex_cube.volumeDepth = camInfoList.Count;
        depth_tex_cube.enableRandomWrite = true;
        depth_tex_cube.Create();

        distortion_tex_cube = new RenderTexture(camInfoList[0].depth_width, camInfoList[0].depth_height, 24, RenderTextureFormat.RGFloat);
        distortion_tex_cube.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        distortion_tex_cube.volumeDepth = camInfoList.Count;
        distortion_tex_cube.enableRandomWrite = true;
        distortion_tex_cube.Create();

        color_tex_cube = new RenderTexture(camInfoList[0].color_width, camInfoList[0].color_height, 24);
        color_tex_cube.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        color_tex_cube.volumeDepth = camInfoList.Count;
        color_tex_cube.enableRandomWrite = true;
        color_tex_cube.Create();

        normal_tex_cube = new RenderTexture(camInfoList[0].depth_width, camInfoList[0].depth_height, 24, RenderTextureFormat.ARGBFloat);
        normal_tex_cube.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        normal_tex_cube.volumeDepth = camInfoList.Count;
        normal_tex_cube.enableRandomWrite = true;
        normal_tex_cube.Create();

        voxelBuffer = new ComputeBuffer(numVoxels * numVoxels * numVoxels, sizeof(int));

    }

    void takeDown()
    {
        //Debug.Log("taking stuff down");
        //smart cleanup:
        if (depth_tex_cube == null || depth_tex_cube.width > 0)
        {
            RenderTexture.DestroyImmediate(depth_tex_cube, true);
        }
        if (distortion_tex_cube == null || distortion_tex_cube.width > 0)
        {
            RenderTexture.DestroyImmediate(distortion_tex_cube, true);
        }
        if (color_tex_cube == null || color_tex_cube.width > 0)
        {
            RenderTexture.DestroyImmediate(color_tex_cube, true);
        }
        if (normal_tex_cube == null || normal_tex_cube.width > 0)
        {
            RenderTexture.DestroyImmediate(normal_tex_cube, true);
        }

        //Debug.Log("handling take down of caminfobuffer");
        if (camInfoBuffer != null)
        {
            //Debug.Log("disposing of cam info buffer because its not null");
            camInfoBuffer.Dispose();
        }
        if (voxelBuffer != null)
        {
            voxelBuffer.Dispose();
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


            //camInfo[cc].cameraActive = 1.0f;

            if (maskArray[cc])
            {
                infoArray[cc].cameraActive = 1.0f;
            }
            else
            {
                infoArray[cc].cameraActive = 0.0f;
            }


        }
        //Debug.Log("cam info buffer: " + camInfoBuffer);
        //Debug.Log("cam info: " + camInfo);
        /*
        unsafe
        {
            Debug.Log("size of cam info struct: " + sizeof(cameraInfo));
        }
        Deug.Log("stride of caminfo buffer: " + camInfoBuffer.stride);
        */
        camInfoBuffer.SetData(infoArray);
    }



    void makeTextureCubes()
    {
        int numCameras = camInfoList.Count;

        int tex_cube_color_kh = textureCubeCompute.FindKernel("CSColor");
        int tex_cube_depth_kh = textureCubeCompute.FindKernel("CSDepth");
        int tex_cube_distortion_kh = textureCubeCompute.FindKernel("CSDistortion");
        int tex_cube_normal_kh = textureCubeCompute.FindKernel("CSNormal");

        for(int i = 0; i<numCameras; i++)
        {

            //handle color:
            textureCubeCompute.SetInt("color_idx", i);
            textureCubeCompute.SetTexture(tex_cube_color_kh, "color_cube", color_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_color_kh, "color_tex", camInfoList[i].colorTex);
            textureCubeCompute.Dispatch(tex_cube_color_kh, camInfoList[0].color_width / 8, camInfoList[0].color_height / 8, 1);

            //handle depth:
            textureCubeCompute.SetInt("depth_idx", i);
            textureCubeCompute.SetTexture(tex_cube_depth_kh, "depth_cube", depth_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_depth_kh, "depth_tex", camInfoList[i].depthTex);
            textureCubeCompute.Dispatch(tex_cube_depth_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);

            //handle distortion:
            textureCubeCompute.SetInt("distortion_idx", i);
            textureCubeCompute.SetTexture(tex_cube_distortion_kh, "distortion_cube", distortion_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_distortion_kh, "distortion_tex", camInfoList[i].distortionMapTex);
            textureCubeCompute.Dispatch(tex_cube_distortion_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);

            //handle normal:
            textureCubeCompute.SetInt("normal_idx", i);
            textureCubeCompute.SetInt("_filter_size", normalFilterSize);
            textureCubeCompute.SetMatrix("depthToWorld", camInfoList[i].visualization.transform.localToWorldMatrix); //i guess this was necessary for some reason... hmmmm.
            textureCubeCompute.SetMatrix("worldToDepth", camInfoList[i].visualization.transform.worldToLocalMatrix);
            textureCubeCompute.SetTexture(tex_cube_normal_kh, "normal_cube", normal_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_normal_kh, "depth_cube_for_normal_cube", depth_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_normal_kh, "distortion_cube_for_normal_cube", distortion_tex_cube);
            //textureCubeCompute.SetTexture(tex_cube_normal_kh, "depth_tex_for_normal_cube", depthCubeArray[i].GetComponent<Renderer>().material.mainTexture);
            textureCubeCompute.Dispatch(tex_cube_normal_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);
        }


        if(debugOutputMode == debugOutputModeEnum.none)
        {
            debugCube.SetActive(false);
        }
        else
        {
            debugCube.SetActive(true);

            int debug_width = 0;
            int debug_height = 0;

            if (debugOutputMode == debugOutputModeEnum.color)
            {
                debug_width = color_tex_cube.width;
                debug_height = color_tex_cube.height;

            }
            if (debugOutputMode == debugOutputModeEnum.depth)
            {
                debug_width = depth_tex_cube.width;
                debug_height = depth_tex_cube.height;

            }
            if (debugOutputMode == debugOutputModeEnum.normal)
            {
                debug_width = depth_tex_cube.width;
                debug_height = depth_tex_cube.height;
            }

            if (debugOutputMode == debugOutputModeEnum.distortion)
            {
                debug_width = depth_tex_cube.width;
                debug_height = depth_tex_cube.height;
            }

            if (debugOutputMode == debugOutputModeEnum.position)
            {
                debug_width = depth_tex_cube.width;
                debug_height = depth_tex_cube.height;
            }


            if (debugTexture == null || debugTexture.width != debug_width)
            {
                debugTexture = new RenderTexture(debug_width, debug_height, 24);
                debugTexture.enableRandomWrite = true;
                debugTexture.Create();
            }

            if (debugOutputMode == debugOutputModeEnum.depth)
            {
                int test_CSDepth_kh = textureCubeCompute.FindKernel("testCSDepth");
                textureCubeCompute.SetInt("test_idx", test_idx);
                textureCubeCompute.SetTexture(test_CSDepth_kh, "depth_cube_for_test", depth_tex_cube);
                textureCubeCompute.SetTexture(test_CSDepth_kh, "depth_test_output", debugTexture);
                textureCubeCompute.Dispatch(test_CSDepth_kh, debug_width / 8, debug_height / 8, 1);
            }

            if (debugOutputMode == debugOutputModeEnum.color)
            {
                int test_CSColor_kh = textureCubeCompute.FindKernel("testCSColor");
                textureCubeCompute.SetInt("test_idx", test_idx);
                textureCubeCompute.SetTexture(test_CSColor_kh, "color_cube_for_test", color_tex_cube);
                textureCubeCompute.SetTexture(test_CSColor_kh, "color_test_output", debugTexture);
                textureCubeCompute.Dispatch(test_CSColor_kh, debug_width / 8, debug_height / 8, 1);
            }

            if (debugOutputMode == debugOutputModeEnum.normal)
            {
                int test_CSNormal_kh = textureCubeCompute.FindKernel("testCSNormal");
                textureCubeCompute.SetInt("test_idx", test_idx);
                textureCubeCompute.SetTexture(test_CSNormal_kh, "normal_cube_for_test", normal_tex_cube);
                textureCubeCompute.SetTexture(test_CSNormal_kh, "normal_test_output", debugTexture);
                textureCubeCompute.Dispatch(test_CSNormal_kh, debug_width / 8, debug_height / 8, 1);
            }

            if (debugOutputMode == debugOutputModeEnum.distortion)
            {
                int test_CSDistortion_kh = textureCubeCompute.FindKernel("testCSDistortion");
                textureCubeCompute.SetInt("test_idx", test_idx);
                textureCubeCompute.SetFloat("distortion_multiplier", distortion_multiplier);
                textureCubeCompute.SetFloat("distortion_dimension", distortion_dimension);
                textureCubeCompute.SetTexture(test_CSDistortion_kh, "distortion_cube_for_test", distortion_tex_cube);
                textureCubeCompute.SetTexture(test_CSDistortion_kh, "distortion_test_output", debugTexture);
                textureCubeCompute.Dispatch(test_CSDistortion_kh, debug_width / 8, debug_height / 8, 1);
            }

            if (debugOutputMode == debugOutputModeEnum.position)
            {
                int test_CSPosition_kh = textureCubeCompute.FindKernel("testCSPosition");
                textureCubeCompute.SetInt("test_idx", test_idx);
                textureCubeCompute.SetTexture(test_CSPosition_kh, "depth_cube_for_position_test", depth_tex_cube);
                textureCubeCompute.SetTexture(test_CSPosition_kh, "distortion_cube_for_position_test", distortion_tex_cube);
                textureCubeCompute.SetTexture(test_CSPosition_kh, "position_test_output", debugTexture);
                textureCubeCompute.SetFloat("position_test_multiplier", position_multiplier);
                textureCubeCompute.Dispatch(test_CSPosition_kh, debug_width / 8, debug_height / 8, 1);
            }



            debugCube.GetComponent<Renderer>().material.mainTexture = debugTexture;
        }



    }

    void loadVoxels()
    {
        int numCameras = camInfoList.Count;

        //clear out voxel buffer so it's all zeros
        int voxelClearKH = voxelCompute.FindKernel("CSVoxelClear");
        voxelCompute.SetBuffer(voxelClearKH, "VoxelsClear", voxelBuffer);
        voxelCompute.Dispatch(voxelClearKH, numVoxels * numVoxels * numVoxels / 64, 1, 1);

        /*
        int voxelFillKH = marchCompute.FindKernel("CSVoxelFill");
        marchCompute.SetBuffer(voxelFillKH, "VoxelsFill", voxelBuffer);
        marchCompute.Dispatch(voxelFillKH, numVoxels * numVoxels * numVoxels / 64, 1, 1);
        */

        for (int cc = 0; cc < numCameras; cc++)
        {
            if (maskArray[cc])
            {

                //load camera data into voxels, flying pixel filter, normal filter
                int voxelKernelHandle = voxelCompute.FindKernel("CSVoxel");
                voxelCompute.SetBuffer(voxelKernelHandle, "Voxels2", voxelBuffer);
                voxelCompute.SetFloat("_CubeWidth2", cubeWidth);
                voxelCompute.SetInt("_NumVoxels2", numVoxels);
                voxelCompute.SetInt("_NumCameras", numCameras);


                voxelCompute.SetBuffer(voxelKernelHandle, "_CamInfoBuffer", camInfoBuffer);
                voxelCompute.SetFloat("flying_pixel_tolerance", flying_pixel_tolerance);
                voxelCompute.SetTexture(voxelKernelHandle, "color_cube", color_tex_cube);
                voxelCompute.SetTexture(voxelKernelHandle, "depth_cube", depth_tex_cube);
                voxelCompute.SetTexture(voxelKernelHandle, "distortion_cube", distortion_tex_cube);
                voxelCompute.SetTexture(voxelKernelHandle, "normal_cube", normal_tex_cube);
                voxelCompute.SetFloat("depth_debug_val", depth_debug_val);
                voxelCompute.SetFloat("max_discontinuity", max_depth_discontinuity);
                voxelCompute.SetInt("discontinuity_delta", discontinuity_delta);
                voxelCompute.SetInt("_CameraId", cc);

                /* //damn, all this was because i messed up the order in the struct.
                marchCompute.SetMatrix("testDepthCameraToWorld", visualizeArray[0].transform.localToWorldMatrix);
                marchCompute.SetFloat("testCx", dii.ppx);
                marchCompute.SetFloat("testCy", dii.ppy);
                marchCompute.SetFloat("testFx", dii.fx);
                marchCompute.SetFloat("testFy", dii.fy);
                marchCompute.SetFloat("depth_debug_val", depth_debug_val);
                */

                voxelCompute.Dispatch(voxelKernelHandle, camInfoList[0].depth_width, camInfoList[0].depth_height, 1);
            }

        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("on application quit!");
        takeDown();
    }

    void OnValidate()
    {
        //Debug.Log("on validate!");
        if(firstFrame == false)
        {
            setup();
        }

    }
}

