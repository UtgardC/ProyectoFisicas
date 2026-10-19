using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapCrane.Editor
{
    /// <summary>
    /// Checks the saved scene through the real physics solver without the Editor PlayerLoop.
    /// Calls the runtime components' lifecycle methods manually, then restores the saved scenes.
    /// </summary>
    public static class CranePhysicsCheck
    {
        const string ScenePath = "Assets/Scenes/PatioDeChatarra.unity";
        const float Dt = .01f;

        [Serializable]
        sealed class Snapshot
        {
            public string stage;
            public float simulatedSeconds;
            public float[] actual;
            public Vector3 pole;
            public string payload;
            public Vector3 payloadPosition;
            public bool attached;
            public int delivered;
        }

        [Serializable]
        sealed class Report
        {
            public bool success;
            public string startedUtc;
            public string finishedUtc;
            public string message;
            public string sourceScene;
            public string restoredScene;
            public string execution = "Edit Mode; runtime Awake/Start/FixedUpdate invoked manually; Physics.Simulate(0.01).";
            public string releaseAdapter = "The test destroys the payload joint immediately before the normal magnet toggle. Deferred Play Mode destruction is not covered.";
            public int articulationBodies;
            public int jointTypes;
            public int activeDrives;
            public int loads;
            public int steps;
            public float simulatedSeconds;
            public bool physicalPickup;
            public bool retainedDuringLift;
            public bool retainedDuringRotation;
            public bool released;
            public int delivered;
            public List<Snapshot> snapshots = new List<Snapshot>();
        }

        sealed class RunState
        {
            public CraneController crane;
            public ArticulationBody[] bodies;
            public ScrapLoad[] loads;
            public ScrapLoad payload;
            public Report report;
        }

        public static string LastReport { get; private set; } = "";
        public static string LastReportPath { get; private set; } = "";

        // Run only after builds/compilation finish, with the Editor in Edit Mode.
        public static string Run()
        {
            var state = new RunState { report = new Report { startedUtc = DateTime.UtcNow.ToString("O") } };
            var previousMode = Physics.simulationMode;
            float previousFixedDeltaTime = Time.fixedDeltaTime;
            int previousFrameRate = Application.targetFrameRate;
            bool previousBackground = Application.runInBackground;
            SceneSetup[] previousScenes = null;
            string failure = null;

            try
            {
                Require(!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode,
                    "La prueba requiere Edit Mode.");
                Require(!BuildPipeline.isBuildingPlayer && !EditorApplication.isCompiling,
                    "Esperar a que termine la compilación o el build.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    Require(!scene.isDirty && !string.IsNullOrEmpty(scene.path),
                        "Guardar las escenas abiertas antes de iniciar la prueba.");
                }

                state.report.sourceScene = SceneManager.GetActiveScene().path;
                previousScenes = EditorSceneManager.GetSceneManagerSetup();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Physics.simulationMode = SimulationMode.Script;

                state.crane = UnityEngine.Object.FindFirstObjectByType<CraneController>();
                Require(state.crane != null && state.crane.root != null && state.crane.magnet != null && state.crane.deliveryZone != null,
                    "Faltan componentes o referencias de la grúa.");
                state.loads = UnityEngine.Object.FindObjectsByType<ScrapLoad>(FindObjectsSortMode.None);
                foreach (var load in state.loads) load.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                state.crane.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                state.crane.deliveryZone.SendMessage("Start", SendMessageOptions.RequireReceiver);
                Physics.SyncTransforms();

                ValidateConfiguration(state);
                CheckFinite(state);
                Record(state, "inicio");
                if (!state.crane.magnet.IsOn) state.crane.magnet.Toggle();

                for (int i = 0; i < Mathf.RoundToInt(8f / Dt) && state.crane.magnet.AttachedLoad == null; i++) Step(state);
                state.payload = state.crane.magnet.AttachedLoad;
                RequireAttached(state);
                state.report.physicalPickup = true;
                Record(state, "captura física");

                state.crane.Targets[1] = -55f;
                Simulate(state, 7f, true);
                Require(state.payload.Body.worldCenterOfMass.y > 1.4f, "La carga no se elevó sobre las paredes del contenedor.");
                Require(Mathf.Abs(state.crane.Actual(1) + 55f) < 7f, "La pluma no alcanzó el objetivo de elevación.");
                state.report.retainedDuringLift = true;
                Record(state, "elevación");

                state.crane.Targets[0] = 135f;
                Simulate(state, 12f, true);
                Require(Mathf.Abs(Mathf.DeltaAngle(state.crane.Actual(0), 135f)) < 5f, "La torreta no alcanzó 135 grados.");
                state.report.retainedDuringRotation = true;
                Record(state, "giro a contenedor");
                Simulate(state, 4f, true);
                Record(state, "antes de soltar");

                // Destroy() is deferred by the PlayerLoop and is invalid in Edit Mode.
                // Remove the same native constraint now, then let Toggle/Release clear state.
                var attachment = state.payload.GetComponent<FixedJoint>();
                Require(attachment != null, "Falta el FixedJoint antes de la descarga.");
                UnityEngine.Object.DestroyImmediate(attachment);
                state.crane.magnet.Toggle();
                Require(!state.crane.magnet.IsOn && state.crane.magnet.AttachedLoad == null && !state.payload.Attached,
                    "El imán no limpió el estado de carga al apagarse.");
                Simulate(state, 6f, false);
                Require(state.payload.GetComponent<FixedJoint>() == null && !state.payload.Attached,
                    "La carga conserva una unión tras apagar el imán.");
                state.report.released = true;
                Record(state, "descarga asentada");
                Require(state.crane.deliveryZone.DeliveredCount >= 1 && state.payload.Delivered && state.crane.deliveryZone.IsContained(state.payload),
                    "La carga no quedó asentada y reconocida dentro del contenedor.");
                state.report.success = true;
                state.report.message = "Captura física, elevación, giro, liberación y entrega comprobados sin teletransportar cargas.";
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                state.report.message = failure;
                if (state.crane != null && state.crane.Joints != null)
                {
                    try { Record(state, "fallo"); }
                    catch { /* Preserve the original diagnostic when initialization failed. */ }
                }
            }
            finally
            {
                if (state.crane != null && state.crane.deliveryZone != null)
                    state.report.delivered = state.crane.deliveryZone.DeliveredCount;
                Physics.simulationMode = previousMode;
                Time.fixedDeltaTime = previousFixedDeltaTime;
                Application.targetFrameRate = previousFrameRate;
                Application.runInBackground = previousBackground;
                if (previousScenes != null)
                {
                    try
                    {
                        // Restore from serialized scene assets. Never save simulated poses.
                        // First unload the test, including when it has the same path as the source.
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                        EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                        state.report.restoredScene = SceneManager.GetActiveScene().path;
                    }
                    catch (Exception exception)
                    {
                        failure = "No se pudo restaurar la escena original: " + exception.Message;
                        state.report.success = false;
                        state.report.message = failure;
                    }
                }
                state.report.finishedUtc = DateTime.UtcNow.ToString("O");
                LastReport = JsonUtility.ToJson(state.report, true);
                LastReportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CranePhysicsCheck.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(LastReportPath));
                File.WriteAllText(LastReportPath, LastReport);
            }

            Debug.Log("CranePhysicsCheck " + (state.report.success ? "PASS" : "FAIL") + ": " + state.report.message + " Informe: " + LastReportPath);
            if (failure != null) throw new InvalidOperationException("CranePhysicsCheck FAIL: " + failure);
            return LastReport;
        }

        static void ValidateConfiguration(RunState state)
        {
            state.bodies = state.crane.root.GetComponentsInChildren<ArticulationBody>();
            state.report.articulationBodies = state.bodies.Length;
            state.report.loads = state.loads.Length;
            var types = new HashSet<ArticulationJointType>();
            foreach (var body in state.bodies)
            {
                types.Add(body.jointType);
                state.report.activeDrives += Active(body.xDrive) + Active(body.yDrive) + Active(body.zDrive);
            }
            state.report.jointTypes = types.Count;
            Require(state.bodies.Length == 8 && types.Count == 4 && state.report.activeDrives == 4,
                "La escena debe tener ocho ArticulationBody, cuatro tipos y cuatro drives activos.");
            Require(state.loads.Length == 5, "Se esperaban cinco cargas.");
            Require(state.crane.Joints != null && state.crane.Joints.Length == 4 && state.crane.Targets.Length == 4,
                "Se esperaban cuatro mandos inicializados.");
            for (int i = 0; i < 4; i++)
                Require(state.crane.Joints[i] != null && state.crane.Joints[i].jointType ==
                    (i == 3 ? ArticulationJointType.PrismaticJoint : ArticulationJointType.RevoluteJoint),
                    "Los mandos deben ser tres revolutas y una prismática.");
        }

        static int Active(ArticulationDrive drive) => drive.stiffness > 0f && drive.forceLimit > 0f ? 1 : 0;

        static void Simulate(RunState state, float seconds, bool carrying)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++)
            {
                Step(state);
                if (carrying) RequireAttached(state);
            }
        }

        static void Step(RunState state)
        {
            state.crane.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            state.crane.magnet.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Physics.Simulate(Dt);
            state.crane.deliveryZone.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            state.report.steps++;
            state.report.simulatedSeconds = state.report.steps * Dt;
            CheckFinite(state);
        }

        static void RequireAttached(RunState state)
        {
            Require(state.payload != null && state.payload.Attached && state.crane.magnet.IsOn && state.crane.magnet.AttachedLoad == state.payload,
                "El imán no capturó o perdió la carga durante el transporte físico.");
            var joint = state.payload.GetComponent<FixedJoint>();
            Require(joint != null && joint.connectedArticulationBody == state.crane.magnet.magnetBody,
                "Falta el FixedJoint conectado al ArticulationBody del imán.");
        }

        static void CheckFinite(RunState state)
        {
            foreach (var body in state.bodies)
            {
                Require(Finite(body.transform.position) && Finite(body.transform.rotation) && Finite(body.linearVelocity) && Finite(body.angularVelocity),
                    "Estado físico no finito en una articulación.");
                var positions = body.jointPosition;
                var velocities = body.jointVelocity;
                for (int i = 0; i < positions.dofCount; i++)
                    Require(Finite(positions[i]) && Finite(velocities[i]), "Coordenada reducida no finita.");
            }
            foreach (var load in state.loads)
                Require(load != null && load.Body != null && Finite(load.Body.position) && Finite(load.Body.rotation) &&
                    Finite(load.Body.linearVelocity) && Finite(load.Body.angularVelocity), "Estado físico no finito en una carga.");
            for (int i = 0; i < 4; i++)
                Require(Finite(state.crane.Targets[i]) && Finite(state.crane.Actual(i)), "Mando o lectura no finitos.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Record(RunState state, string stage)
        {
            var actual = new float[4];
            for (int i = 0; i < 4; i++) actual[i] = state.crane.Actual(i);
            state.report.snapshots.Add(new Snapshot
            {
                stage = stage,
                simulatedSeconds = state.report.simulatedSeconds,
                actual = actual,
                pole = state.crane.magnet.Pole,
                payload = state.payload != null ? state.payload.name : "",
                payloadPosition = state.payload != null ? state.payload.Body.position : Vector3.zero,
                attached = state.payload != null && state.payload.Attached,
                delivered = state.crane.deliveryZone.DeliveredCount
            });
        }
    }
}
