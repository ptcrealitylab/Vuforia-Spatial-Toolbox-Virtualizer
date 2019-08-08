using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AK_radiusFeature : MonoBehaviour {
    
    //references
    public GameObject AK_receiver;
    public ComputeShader radiusFeatureDetectorCompute;
    List<akplay.camInfo> camInfoList;
    public List<GameObject> result_display_list = new List<GameObject>();

    //internal:
    bool camerasReady = false;
    ComputeBuffer camInfoBuffer;
    public RenderTexture[] resultTexture;
    public RenderTexture[] normalTexture;

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
    void Start () {
		
	}

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
            getFeatures();
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("on application quit!");
        takeDown();
    }

    #endregion

    void takeDown()
    {
        if (camInfoBuffer != null)
        {
            //Debug.Log("disposing of cam info buffer because its not null");
            camInfoBuffer.Dispose();
        }
    }


    void do_setup()
    {

        float delta = 0.15f;
        for (int i = 0; i < AK_receiver.GetComponent<akplay>().camInfoList.Count; i++)
        {
            GameObject result_display = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result_display.name = "threshold_" + i;
            result_display.transform.parent = gameObject.transform;
            result_display.transform.localScale = new Vector3(0.1f, 0.1f, 0.001f);
            result_display.transform.localPosition = new Vector3(0.0f, -delta * i, 0.0f);
            //result_display.GetComponent<Renderer>().material = new Material(Shader.Find("Custom/floatShaderRealsense"));
            result_display_list.Add(result_display);
        }

        resultTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];
        normalTexture = new RenderTexture[AK_receiver.GetComponent<akplay>().camInfoList.Count];

        camInfoList = AK_receiver.GetComponent<akplay>().camInfoList;

        //Debug.Log("setting up cam info list count: " + camInfoList.Count);
        //camInfoBuffer = new ComputeBuffer(camInfoList.Count, 268);  //3 matrices and 19 floats: 3*64 + 19*4 = 268
        camInfoBuffer = new ComputeBuffer(camInfoList.Count, 328);  //3 matrices and 19 floats: 3*64 + 34*4 = 328
        getCameraInfo();
    }


    public float normal_multiplier = 1.0f;
    public int normal_filter_size = 5;
    public int search_size = 5;
    public float proximity = 0.4f;
    public float result_multiplier = 1.0f;

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
            int radiusFeatureDetector_kh = radiusFeatureDetectorCompute.FindKernel("getNormalDiff");
            //Debug.Log("radius feature detector: " + radiusFeatureDetector_kh);

            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "depth_tex", camInfoList[cc].depthTex);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "distortion_tex", camInfoList[cc].distortionMapTex);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "normal_tex", normalTexture[cc]);
            radiusFeatureDetectorCompute.SetTexture(radiusFeatureDetector_kh, "result_tex", resultTexture[cc]);

            radiusFeatureDetectorCompute.SetInt("search_size", search_size);
            radiusFeatureDetectorCompute.SetFloat("proximity", proximity);
            radiusFeatureDetectorCompute.SetFloat("result_multiplier", result_multiplier);

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
}
