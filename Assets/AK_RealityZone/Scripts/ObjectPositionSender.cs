using UnityEngine;
using BestHTTP.SocketIO;
using SimpleJSON;

public class ObjectPositionSender : MonoBehaviour {
    private SocketManager Manager;
    private bool connected;
    public string objectServerUrl = "http://127.0.0.1:8080";

    void Start()
    {
        SocketOptions options = new SocketOptions();
        options.AutoConnect = false;

        Manager = new SocketManager(new System.Uri(objectServerUrl + "/socket.io/"), options);
        Manager.Socket.On(SocketIOEventTypes.Connect, OnConnected);
        Manager.Socket.On(SocketIOEventTypes.Disconnect, OnDisconnected);

        Manager.Socket.On(SocketIOEventTypes.Error, (socket, packet, args) => Debug.LogError(string.Format("Error: {0}", args[0].ToString())));

        Manager.Open();
    }

    void Update()
    {
    }


    void OnDestroy()
    {
        Manager.Close();
    }

    private void OnConnected(Socket socket, Packet packet, params object[] args)
    {
        //identify yourself as the station.
        Debug.Log("connected to server");
        connected = true;
    }

    private void OnDisconnected(Socket socket, Packet packet, params object[] args)
    {
        connected = false;
        Debug.Log("server disconnected");
    }

    public void SendSkeleton(JSONArray skeletons)
    {
        if (!connected)
        {
            return;
        }

        foreach (var skeleton in skeletons.Values)
        {
            string topLevelKey = "human" + skeleton["id"].ToString();
            
            foreach (var joint in skeleton["joints"])
            {
                string objectKey = topLevelKey + "joint" + joint.Key;
                double x = joint.Value["x"].AsDouble * 1000;
                double y = joint.Value["y"].AsDouble * 1000;
                double z = joint.Value["z"].AsDouble * 1000;
                JSONObject updateMsg = new JSONObject();
                updateMsg["objectKey"] = objectKey;
                updateMsg["position"] = new JSONObject();
                updateMsg["position"]["x"] = x;
                updateMsg["position"]["y"] = y;
                updateMsg["position"]["z"] = z;
                updateMsg["rotationInRadians"] = 0;
                updateMsg["editorId"] = "test";
                Manager.Socket.Emit("/update/object/position", updateMsg.ToString());
            }
        }
    }
}
