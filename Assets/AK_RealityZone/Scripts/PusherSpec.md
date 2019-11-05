# Pusher Specification

This specification exists to document the existing Pusher messages between it
and the `pass` hardware interface contained on the `realityZoneInterface` of
the `RE-server` repository.

These messages take place over a one-to-one socket.io connection and have a
message name paired with an optional payload.

## Connection establishment

The URL for the socket connection is http://127.0.0.1:3020/socket.io

## Incoming message names

- message
- resolution
- resolutionPhone
- pose
- poseVuforia
- poseVuforiaDesktop
- vuforiaResult_server_system
- realityEditorObject_server_system
- startRecording_server_system
- stopRecording_server_system
- twin_server_system
- clearTwins_server_system
- zoneInteractionMessage_server_system

## Outgoing message names

- vuforiaImage_system_server
- vuforiaModelUpdate_system_server
- name
- realityZoneSkeleton
- image
- realityZoneGif_system_server


## realityZoneSkeleton

Payload:
```javascript
{
  id: number,
  joints: Array<{x: number, y: number, z: number}>,
}
```

The id is k4abt's best guess at an accurate per-frame identifier

Each joint corresponds to an entry in
[k4abt_joint_id_t](https://microsoft.github.io/Azure-Kinect-Body-Tracking/release/0.9.x/group__btenums.html#ga5fe6fa921525a37dec7175c91c473781)
as follows:

```c
k4abt_joint_id_t {
  K4ABT_JOINT_PELVIS = 0,
  K4ABT_JOINT_SPINE_NAVAL,
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
  K4ABT_JOINT_COUNT // 32
}
```

For example, the first jointed listed will be the pelvis and the last joint
will be the right ear.


## image

Payload: data url with encoding base64 containing an image rendered from the
game camera
