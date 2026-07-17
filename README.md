# ProjectComp — Robotic Arm + ZED Point Cloud Visualization

## Overview

`ProjectComp` is a Unity-based toolkit for visualizing live ZED stereo camera data and controlling a robotic arm through ROS. It combines a low-latency point cloud renderer and color video feed with ROS bidirectional integration and VR teleoperation support, enabling a seamless mixed-reality workflow for research and prototyping.

Key capabilities:
- High-performance live point cloud streaming and rendering from a remote ZED server.
- Live color video feed (JPEG-over-TCP) for texture-mapped displays.
- ROS integration via `Unity.Robotics.ROSTCPConnector` to subscribe to joint states and publish joint commands.
- VR controller teleoperation and a keyboard fallback for commanding joint velocities.

## Highlights / Features

- `ZedPointCloud.cs`: Efficient, threaded TCP client that parses binary point clouds off the main thread, uploads via a single persistent `ComputeBuffer`, and draws with `PointCloudShader.shader`. Handles 100k–300k points at VR frame rates.
- `ZedColorFeed.cs`: JPEG-over-TCP video client that decodes frames on the main thread and updates a `Texture2D` on a `Renderer`.
- `RosJointCommandPublisher.cs` / `UrRobotController`: Bridges Unity and ROS by subscribing to `/joint_states` and publishing commands to a configurable controller topic.
- `VRJoysticks.cs`: Maps Meta Quest controllers (or equivalent XR controllers) to joint velocity commands with deadzone and smoothing.
- Included shader assets for point rendering and an example material configured to `Cull Off`.

## Architecture

- A lightweight Ubuntu-side `zed_server.py` streams two TCP services:
  - Video JPEG stream (default port `5003`)
  - Point cloud binary stream (default port `5004`)
  - HTTP control endpoint (default port `5002`) for runtime settings (e.g. confidence threshold)

- Unity runs three main subsystems:
  - Network threads for ZED point cloud and color feed (non-Unity APIs only) that hand off parsed frames to the main thread.
  - Rendering layer using `ComputeBuffer` + GPU shader for efficient point rendering.
  - ROS bridge and teleoperation components for robot state sync and command publishing.

## Quick Start

Prerequisites:
- Unity Editor (open a compatible Unity project or create a new one and copy the `Assets/` files)
- A running ZED data server (see `zed_server.py`) on the same LAN or reachable IP
- (Optional) ROS + `rosbridge`/`ros_tcp_endpoint` and `Unity.Robotics.ROSTCPConnector` for ROS comms

Unity setup:
1. Open the project in Unity and add the provided scripts and shaders to a Scene.
2. Create an empty `GameObject` and attach `ZedPointCloud`:
   - Assign a Material that uses `PointCloudShader.shader` (ensure `Cull Off` in the Pass block).
   - Set `Server Ip` to your server (e.g. `192.168.1.130`).
   - Set `Server Port` to `5004` (point cloud TCP port).
   - Tune `Max Point Capacity` (default `300000`) to match your expected peak.
3. Create a Quad (or mesh) and attach `ZedColorFeed`:
   - Set `Server Ip` and `Server Port` (`5003` by default).
   - Assign the target `Renderer` (or let the component pick the attached `Renderer`).
4. For ROS control:
   - Add `UrRobotController` and populate the `joints` array with the robot's `ArticulationBody` chain.
   - Configure `jointStateTopic` (default `/joint_states`) and `commandTopic` (default `/forward_position_controller/commands`).
5. For VR teleop:
   - Add `VRJoysticks` and assign a `UrVelocityBridge` implementation (bridge publishes velocity commands to ROS).

Running:
- Start the ZED server on your Ubuntu machine. Ensure the Unity Editor machine can reach the server IP and the configured ports (`5002`, `5003`, `5004`).
- Enter Play mode in Unity. Monitor the Console for connection logs (`ZedPointCloud: Connected...`, `ZedColorFeed: Connected...`).
- Use the Inspector `Confidence Threshold` slider to tune depth filtering in real time. The value is pushed to the server via the HTTP endpoint.

VR Controls & Teleop:
- By default, holding `Space` switches Unity into Command Mode and sends the Unity joint positions to the robot.
- `VRJoysticks` maps controller axes and buttons to six joint velocity channels. Tune `maxSpeed`, `deadzone`, and `smoothing` in the Inspector.
- Left/Right sticks and triggers map to specific joints (see `Assets/VRJoysticks.cs` comments for exact mapping).

## Networking & Ports

- Point cloud TCP: `5004` (binary format: uint32 length, payload with uint32 N then N * 16 bytes per point)
- Color video TCP: `5003` (JPEG frames, prefixed by uint32 length)
- HTTP config: `5002` (GET endpoints for runtime settings, e.g. `/set_confidence?value=XX`)

Firewall and NAT notes:
- Ensure the server machine allows incoming TCP connections on the above ports.
- For multi-machine setups across subnets, confirm routing/firewall rules and use static IPs where possible.

## Troubleshooting

- No point cloud frames:
  - Verify `zed_server.py` is running and bound to the correct IP.
  - Check Unity Console for connection or read errors from `ZedPointCloud`.
  - Ensure `maxPointCapacity` is high enough; incoming frames larger than the cap are truncated and logged.

- Video not updating:
  - Confirm the `targetRenderer` material property name (default `_MainTex`).
  - Look for `ZedColorFeed` connection logs and JPEG length sanity rejections.

- ROS comms not working:
  - Verify the ROS TCP endpoint and `Unity.Robotics.ROSTCPConnector` configuration.
  - Ensure topics match and message types are correct.

## Project Structure

- `Assets/` — Unity scripts and shader assets
  - `ZedPointCloud.cs`, `ZedColorFeed.cs`, `PointCloudShader.shader`
  - `RosJointCommandPublisher.cs`, `VRJoysticks.cs`, and bridge utilities
