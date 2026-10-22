using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ScrapCrane
{
    // Both control modes move the same physical drives, in the physics step.
    public sealed class CraneController : MonoBehaviour
    {
        public ArticulationBody root, yaw, boom, elbow, extension;
        public ScrapMagnet magnet;
        public Transform loadSpawn;
        public DeliveryZone deliveryZone;

        public readonly string[] Labels = { "Rotación", "Pluma", "Brazo", "Extensión" };
        public readonly string[] Units = { "°", "°", "°", "m" };
        public float[] Targets { get; private set; }
        public float Power { get; set; } = 1f;
        public bool DrivesEnabled { get; set; } = true;
        public ArticulationBody[] Joints { get; private set; }
        public bool OperatorMode { get; private set; }
        readonly float[] speeds = { 18f, 12f, 18f, .65f };
        ArticulationDrive[] initialDrives;
        float[] commanded;
        Vector4 operatorInput;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Time.fixedDeltaTime = .01f;
            Joints = new[] { yaw, boom, elbow, extension };
            Targets = new float[4];
            commanded = new float[4];
            initialDrives = new ArticulationDrive[4];
            for (int i = 0; i < 4; i++)
            {
                initialDrives[i] = Joints[i].xDrive;
                Targets[i] = commanded[i] = initialDrives[i].target;
                // Articulation state is initialized in reduced coordinates, in radians/metres.
                // This also avoids a first solver step from the default zero joint state.
                Joints[i].jointPosition = new ArticulationReducedSpace(Targets[i] * (i == 3 ? 1f : Mathf.Deg2Rad));
                Joints[i].jointVelocity = new ArticulationReducedSpace(0f);
            }
            // Ignore internal geometry, while preserving collisions with all loose loads.
            var colliders = new List<Collider>();
            foreach (var c in root.GetComponentsInChildren<Collider>())
                if (c.GetComponentInParent<ArticulationBody>() != null) colliders.Add(c);
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j]);
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || CraneHUD.EditingNumber) return;
            if (keys.spaceKey.wasPressedThisFrame) magnet.Toggle();
            if (keys.rKey.wasPressedThisFrame) Restart();
        }

        void FixedUpdate()
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                var drive = Joints[i].xDrive;
                if (OperatorMode)
                {
                    // A centered lever holds the last drive target: there is no queued destination.
                    if (DrivesEnabled)
                        commanded[i] += operatorInput[i] * speeds[i] * Time.fixedDeltaTime;
                    commanded[i] = Mathf.Clamp(commanded[i], drive.lowerLimit, drive.upperLimit);
                    Targets[i] = commanded[i];
                }
                else
                {
                    Targets[i] = Mathf.Clamp(Targets[i], drive.lowerLimit, drive.upperLimit);
                    commanded[i] = Mathf.MoveTowards(commanded[i], Targets[i], speeds[i] * Time.fixedDeltaTime);
                }
                drive.target = commanded[i];
                drive.stiffness = DrivesEnabled ? initialDrives[i].stiffness : 0f;
                drive.damping = DrivesEnabled ? initialDrives[i].damping : 0f;
                drive.forceLimit = DrivesEnabled ? initialDrives[i].forceLimit * Power : 0f;
                Joints[i].xDrive = drive;
            }
        }

        public void SetOperatorMode(bool enabled)
        {
            if (OperatorMode == enabled) return;
            OperatorMode = enabled;
            StopOperatorInput();
            // Cancel pending study-mode movement without changing the current drive command.
            if (Joints == null) return;
            for (int i = 0; i < Joints.Length; i++)
                Targets[i] = commanded[i] = Joints[i].xDrive.target;
        }

        public void SetOperatorInput(Vector2 rotationAndBoom, float elbowInput, float extensionInput)
        {
            if (!OperatorMode || !DrivesEnabled) { StopOperatorInput(); return; }
            operatorInput = new Vector4(
                Mathf.Clamp(rotationAndBoom.x, -1f, 1f),
                -Mathf.Clamp(rotationAndBoom.y, -1f, 1f),
                Mathf.Clamp(elbowInput, -1f, 1f),
                Mathf.Clamp(extensionInput, -1f, 1f));
        }

        public void StopOperatorInput() => operatorInput = Vector4.zero;

        void OnDisable() => StopOperatorInput();
        void OnApplicationFocus(bool focused) { if (!focused) StopOperatorInput(); }
        void OnApplicationPause(bool paused) { if (paused) StopOperatorInput(); }

        public float Actual(int i)
        {
            var position = Joints[i].jointPosition;
            return position.dofCount > 0 ? position[0] * (i == 3 ? 1f : Mathf.Rad2Deg) : 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
