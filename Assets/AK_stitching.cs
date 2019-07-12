using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AK_stitching : MonoBehaviour {

    //external settings
    public float cubeWidth = 2.54f;
    public int numVoxels = 10;
    public bool[] maskArray;
    public int normalFilterSize=3;

    //references:
    public GameObject AK_receiver;
    List<akplay.camInfo> camInfoList;

    public ComputeShader textureCubeCompute;


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
    }
    



    // Use this for initialization
    void Start () {

	}

    bool firstFrame = true;
	// Update is called once per frame
	void Update () {
        if (firstFrame && AK_receiver.GetComponent<akplay>().camerasReady)
        {
            firstFrame = false;
            setup();
        }

        if(firstFrame == false)
        {
            render_ready = false;
            getCameraInfo();
            makeTextureCubes();
            //loadVoxels();
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
        camInfoBuffer = new ComputeBuffer(camInfoList.Count, 268);  //3 matrices and 19 floats: 3*64 + 19*4 = 268

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

            textureCubeCompute.SetInt("color_idx", i);
            textureCubeCompute.SetInt("depth_idx", i);
            textureCubeCompute.SetInt("distortion_idx", i);
            textureCubeCompute.SetInt("normal_idx", i);

            textureCubeCompute.Dispatch(tex_cube_color_kh, camInfoList[0].color_width / 8, camInfoList[0].color_height / 8, 1);
            textureCubeCompute.Dispatch(tex_cube_depth_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);
            textureCubeCompute.Dispatch(tex_cube_distortion_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);

            textureCubeCompute.SetInt("_filter_size", normalFilterSize);
            textureCubeCompute.SetTexture(tex_cube_normal_kh, "normal_cube", normal_tex_cube);
            textureCubeCompute.SetTexture(tex_cube_normal_kh, "depth_cube_for_normal_cube", depth_tex_cube);
            //textureCubeCompute.SetTexture(tex_cube_normal_kh, "depth_tex_for_normal_cube", depthCubeArray[i].GetComponent<Renderer>().material.mainTexture);
            textureCubeCompute.Dispatch(tex_cube_normal_kh, camInfoList[0].depth_width / 8, camInfoList[0].depth_height / 8, 1);
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
        setup();
    }
}
