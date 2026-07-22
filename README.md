# VR Robotic Arm Control & ZED Point Cloud Visualization

## Overview
This project provides a Unity-based mixed-reality toolkit designed to control and observe a robotic arm's behavior in real time from remote locations. By bridging the physical gap between the robot and the technician, it enables immediate fault assessment using a digital twin and real-time sensor streams without requiring on-site transit.

The system combines low-latency point cloud rendering, live stereo video streaming, bidirectional ROS integration, and full VR teleoperation support.

---

## Technology Stack

*   **Game Engine & Environment:** Unity (with Universal Render Pipeline / Built-in Shader Pipeline compatibility)
*   **Virtual Reality:** Meta XR SDK / OpenXR (optimized for Meta Quest 3 via Air Link)
*   **Computer Vision & Depth Sensing:** ZED 2i Stereo Camera & ZED SDK
*   **Robotics Middleware:** ROS (Robot Operating System) via `Unity.Robotics.ROSTCPConnector`
*   **Graphics & Shading:** Custom HLSL geometry shaders using persistent GPU `ComputeBuffer` allocations
*   **Programming Languages:** C# (Unity Client) & Python / C++ (Linux Server)

### 1. Point Cloud Streaming (`ZedPointCloud.cs` & `PointCloudShader.shader`)
*   Binary point cloud data (containing 3D coordinates and packed BGRA colors) is transmitted over a TCP socket.
*   Data parsing occurs off the main thread into a double buffer array to prevent VR frame stuttering.
*   Points are uploaded to GPU memory via a fixed, single persistent `ComputeBuffer`.
*   A custom geometry shader expands each point into a camera-facing billboard quad, supporting 100,000 to 300,000+ points at full VR frame rates.

### 2. Video Feed Streaming (`ZedColorFeed.cs`)
*   JPEG frames are received continuously over TCP.
*   The main thread decodes the byte array directly into a `Texture2D` assigned to the scene display surface.

### 3. Bidirectional ROS Communication (`RosConnection.cs` & `RosJointCommandPublisher.cs`)
*   **Robot-to-Unity:** Subscribes to the `/joint_states` ROS topic to receive actual joint angles and mirror them live on the Unity digital twin.
*   **Unity-to-Robot:** Converts VR controller inputs or position offsets into velocity/position commands published to controller topics (e.g., `/forward_velocity_controller/commands`).

---

## Connection & Network Configuration

### Network Ports
*   **HTTP Config Port (`5002`):** Handles runtime parameter adjustments (e.g., ZED depth confidence filtering).
*   **Color Video TCP Port (`5003`):** Streams JPEG image frames.
*   **Point Cloud TCP Port (`5004`):** Streams raw binary point cloud data.
*   **ROS TCP Bridge Port (`10000`):** Handles bidirectional ROS message communication.

### IP Address Setup
*   **Server Wired Interface:** `192.168.1.X`
*   **Server Wireless Interface / Host:** `192.168.1.Y`

> *Note:* If network updates reset the wired IP on the Linux server, re-assign it using:
> `sudo ip addr add 192.168.1.X/24 dev [eth0]` (replacing `[eth0]` with your wired interface name).

---

## Connection Steps & Operation

### 1. Server-Robot Initialization
1.  Physically connect the ZED 2i camera via a dedicated USB 3.0 port and the robot via the Ethernet interface to the server machine.
2.  Set the robot's companion tablet to **Local Control Mode**.
3.  Open a terminal on the server machine and clean up existing sessions:
    ```bash
    ./start_robot.sh stop
    ```
4.  Run the startup script:
    ```bash
    ./start_robot.sh
    ```
5.  Confirm that terminal messages state:
    *   Server started on `192.168.1.Y:10000`
    *   Camera started successfully
    *   Robot is accepting commands
6.  Press the **Play** button on the robot control tablet.

### 2. VR Client Setup
1.  Ensure both the VR Host PC and the Meta Quest 3 headset are connected to the same local network.
2.  Ensure the headset battery level is **above 30%** (lower levels will force Air Link to disconnect).
3.  In the Quest headset, open **Quick Settings** > **Air Link** and launch the connection to the host PC.
4.  Launch `RobotVRControl.exe` from the host PC desktop or the VR Library environment. The app will launch in full immersive VR.

---

## VR Control Mapping

The robotic arm is teleoperated using the Meta Quest Touch controllers:

| Controller | Control Input | Robot Joint / Motion |
| :--- | :--- | :--- |
| **Left Controller** | Stick Left / Right | Elbow Joint |
| **Left Controller** | Stick Up / Down | Wrist 2 |
| **Left Controller** | Bumper (LB) | Wrist 1 |
| **Left Controller** | Trigger (LT) | Wrist 3 (Accessory Rotation) |
| **Right Controller** | Stick Left / Right | Shoulder Pan |
| **Right Controller** | Stick Up / Down | Shoulder Lift |
| **Right Controller** | Trigger (RT) | Wrist 3 (Accessory Rotation) |

*(Keyboard Fallback: Holding the `Space` key in Unity enables Command Mode, sending Unity joint angles directly to the physical robot.)*

---

## Shutdown Procedure

1.  **Exit Client:** Press `Esc` on the host PC keyboard to close the Unity application, then disconnect Meta Air Link inside the headset dashboard.
2.  **Stop Server:** Run the termination script on the Linux server terminal to gracefully stop processes and avoid zombie background threads:
    ```bash
    ./start_robot.sh stop
    ```

---

## Core Repository Structure & Scripts

*   `Assets/ZedPointCloud.cs`: Background-threaded TCP client that parses point cloud payloads into double buffers and uploads them to GPU memory.
*   `Assets/ZedColorFeed.cs`: Handles JPEG image reception and updates the texture on designated scene renderers.
*   `Assets/PointCloudShader(1).shader`: Custom shader expanding buffer data into screen-aligned billboard quads with soft circular falloff.
*   `Assets/VRJoysticks.cs`: Translates Meta Quest controller stick and trigger inputs into smoothed joint velocity channels.
*   `Assets/RosConnection.cs` / `RosJointCommandPublisher.cs`: Manages ROS publisher and subscriber instances for state sync and execution.
*   `Assets/TelemetryDisplay.cs`: Pushes joint telemetry, tool-center point (TCP) coordinates, and status to a world-space lazy-following HUD.

---
*License: MIT*
