using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScrapCrane.Editor
{
    /// <summary>Runs a physical pickup-to-delivery check in an already playing scene.</summary>
    public static class CraneSmokeCheck
    {
        enum Stage { Capture, Lift, Rotate, Hold, Settle }

        [Serializable]
        sealed class Snapshot
        {
            public string stage;
            public float elapsed;
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
            public string scene;
            public int articulationBodies;
            public int jointTypes;
            public int activeDrives;
            public int loads;
            public bool stepMode;
            public int requestedSteps;
            public float elapsed;
            public bool physicalPickup;
            public bool retainedDuringLift;
            public bool retainedDuringRotation;
            public bool released;
            public int delivered;
            public List<Snapshot> snapshots = new List<Snapshot>();
        }

        static CraneController crane;
        static ArticulationBody[] bodies;
        static ScrapLoad[] loads;
        static ScrapLoad payload;
        static Report report;
        static Stage stage;
        static float startedTime, stageTime;
        static double startedEditorTime;
        static bool useStepMode, restorePauseState, previousPaused;
        static int observedFrame;

        public static bool IsRunning { get; private set; }
        public static string LastReportPath { get; private set; }

        // Invoke with: ScrapCrane.Editor.CraneSmokeCheck.Start(); while in Play Mode.
        // Only controller targets and the normal magnet toggle are used; no body is teleported.
        public static void Start(bool stepMode = true)
        {
            if (IsRunning)
            {
                Debug.Log("CraneSmoke: la prueba ya está en curso.");
                return;
            }

            report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), stepMode = stepMode };
            startedTime = Time.time;
            startedEditorTime = EditorApplication.timeSinceStartup;
            stageTime = startedTime;
            payload = null;
            crane = null;
            bodies = null;
            loads = null;
            useStepMode = stepMode;
            restorePauseState = false;
            observedFrame = Time.frameCount;

            try
            {
                Require(EditorApplication.isPlaying, "La prueba requiere Play Mode.");
                crane = UnityEngine.Object.FindFirstObjectByType<CraneController>();
                Require(crane != null, "No se encontró CraneController.");
                Require(crane.root != null && crane.magnet != null && crane.deliveryZone != null,
                    "Faltan referencias de la grúa.");
                Require(crane.Joints != null && crane.Joints.Length == 4 && crane.Targets != null && crane.Targets.Length == 4,
                    "La grúa debe exponer cuatro mandos inicializados.");
                Require(crane.DrivesEnabled, "Los drives deben estar activados.");

                report.scene = crane.gameObject.scene.path;
                bodies = crane.root.GetComponentsInChildren<ArticulationBody>();
                loads = UnityEngine.Object.FindObjectsByType<ScrapLoad>(FindObjectsSortMode.None);
                report.articulationBodies = bodies.Length;
                report.loads = loads.Length;
                var types = new HashSet<ArticulationJointType>();
                foreach (var body in bodies)
                {
                    types.Add(body.jointType);
                    report.activeDrives += Active(body.xDrive) + Active(body.yDrive) + Active(body.zDrive);
                }
                report.jointTypes = types.Count;
                Require(bodies.Length == 8, "Se esperaban ocho ArticulationBody.");
                Require(types.Count == 4, "Se esperaban cuatro tipos de articulación.");
                Require(report.activeDrives == 4, "Se esperaban cuatro drives con stiffness y forceLimit positivos.");
                Require(loads.Length == 5, "Se esperaban cinco cargas.");
                Require(crane.deliveryZone.DeliveredCount == 0, "Iniciar desde el patio sin entregas previas.");
                for (int i = 0; i < crane.Joints.Length; i++)
                {
                    var joint = crane.Joints[i];
                    Require(joint != null, "Un mando no tiene articulación.");
                    Require(joint.jointType == (i == 3 ? ArticulationJointType.PrismaticJoint : ArticulationJointType.RevoluteJoint),
                        "Los mandos deben ser tres revolutas y una prismática.");
                    var drive = joint.xDrive;
                    Require(Active(drive) == 1 && Finite(drive.lowerLimit) && Finite(drive.upperLimit) && drive.lowerLimit < drive.upperLimit,
                        "Drive activo o límites inválidos en el mando " + i + ".");
                    Require(crane.Targets[i] >= drive.lowerLimit && crane.Targets[i] <= drive.upperLimit,
                        "Objetivo inicial fuera de rango en el mando " + i + ".");
                }
                CheckFinite();
                Require(crane.boom.xDrive.lowerLimit <= -55f && crane.yaw.xDrive.upperLimit >= 135f,
                    "Los límites no permiten la ruta de prueba.");

                stage = Stage.Capture;
                IsRunning = true;
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
                Record("inicio");
                if (!crane.magnet.IsOn) crane.magnet.Toggle();
                previousPaused = EditorApplication.isPaused;
                restorePauseState = useStepMode;
                if (useStepMode) EditorApplication.isPaused = true;
                Debug.Log("CraneSmoke: captura, elevación, giro y descarga iniciados.");
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
        }

        static int Active(ArticulationDrive drive) => drive.stiffness > 0f && drive.forceLimit > 0f ? 1 : 0;

        static void Tick()
        {
            if (!IsRunning) return;
            try
            {
                Require(EditorApplication.isPlaying && crane != null, "Play Mode terminó antes de completar la prueba.");
                Require(EditorApplication.timeSinceStartup - startedEditorTime < 180d,
                    "La prueba superó 180 segundos de reloj; comprobar el avance de frames o la escala de tiempo.");
                CheckFinite();
                float elapsed = Time.time - stageTime;

                switch (stage)
                {
                    case Stage.Capture:
                        if (crane.magnet.AttachedLoad != null)
                        {
                            payload = crane.magnet.AttachedLoad;
                            RequireAttached();
                            report.physicalPickup = true;
                            Record("captura física");
                            crane.Targets[1] = -55f;
                            Advance(Stage.Lift);
                        }
                        else Require(elapsed <= 8f, "El imán no capturó una carga en ocho segundos sin teletransporte.");
                        break;

                    case Stage.Lift:
                        RequireAttached();
                        if (elapsed >= 7f)
                        {
                            Require(payload.Body.worldCenterOfMass.y > 1.4f, "La carga no se elevó sobre las paredes del contenedor.");
                            Require(Mathf.Abs(crane.Actual(1) - crane.Targets[1]) < 7f, "La pluma no alcanzó la elevación de prueba.");
                            report.retainedDuringLift = true;
                            Record("elevación");
                            crane.Targets[0] = 135f;
                            Advance(Stage.Rotate);
                        }
                        break;

                    case Stage.Rotate:
                        RequireAttached();
                        if (elapsed >= 12f)
                        {
                            Require(Mathf.Abs(Mathf.DeltaAngle(crane.Actual(0), 135f)) < 5f,
                                "La torreta no alcanzó 135 grados.");
                            report.retainedDuringRotation = true;
                            Record("giro a contenedor");
                            Advance(Stage.Hold);
                        }
                        break;

                    case Stage.Hold:
                        RequireAttached();
                        if (elapsed >= 4f)
                        {
                            Record("antes de soltar");
                            crane.magnet.Toggle();
                            Require(!crane.magnet.IsOn && !payload.Attached && crane.magnet.AttachedLoad == null,
                                "El imán no liberó su estado de carga.");
                            Advance(Stage.Settle);
                        }
                        break;

                    case Stage.Settle:
                        if (elapsed >= 6f)
                        {
                            Require(payload != null && payload.GetComponent<FixedJoint>() == null && !payload.Attached,
                                "La carga conserva el FixedJoint tras apagar el imán.");
                            report.released = true;
                            Record("descarga asentada");
                            Require(crane.deliveryZone.DeliveredCount >= 1 && payload.Delivered && crane.deliveryZone.IsContained(payload),
                                "La descarga no dejó una carga asentada y reconocida dentro del contenedor.");
                            Finish(true, "Captura física, transporte y entrega comprobados.");
                        }
                        break;
                }

                // Some background Editor sessions stop scheduling the PlayerLoop. Stepping
                // from the editor callback keeps the normal Update/FixedUpdate loop intact.
                // Never spin or simulate physics directly: wait for the requested frame.
                if (IsRunning && useStepMode)
                {
                    if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
                    int currentFrame = Time.frameCount;
                    if (currentFrame == observedFrame)
                    {
                        EditorApplication.Step();
                        report.requestedSteps++;
                    }
                    observedFrame = currentFrame;
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
        }

        static void Advance(Stage next)
        {
            stage = next;
            stageTime = Time.time;
        }

        static void RequireAttached()
        {
            Require(payload != null && crane.magnet.IsOn && crane.magnet.AttachedLoad == payload && payload.Attached,
                "La carga se perdió durante " + stage + ".");
            var joint = payload.GetComponent<FixedJoint>();
            Require(joint != null && joint.connectedArticulationBody == crane.magnet.magnetBody,
                "La carga debe conservar un FixedJoint conectado al ArticulationBody del imán.");
        }

        static void CheckFinite()
        {
            foreach (var body in bodies)
            {
                Require(body != null && Finite(body.transform.position) && Finite(body.transform.rotation) &&
                    Finite(body.linearVelocity) && Finite(body.angularVelocity), "Estado físico no finito en una articulación.");
                var positions = body.jointPosition;
                var velocities = body.jointVelocity;
                for (int i = 0; i < positions.dofCount; i++)
                    Require(Finite(positions[i]) && Finite(velocities[i]), "Coordenada reducida no finita.");
            }
            foreach (var load in loads)
                Require(load != null && load.Body != null && Finite(load.transform.position) && Finite(load.transform.rotation) &&
                    Finite(load.Body.linearVelocity) && Finite(load.Body.angularVelocity), "Estado físico no finito en una carga.");
            for (int i = 0; i < 4; i++)
                Require(Finite(crane.Targets[i]) && Finite(crane.Actual(i)), "Mando o lectura no finitos.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Record(string name)
        {
            var actual = new float[4];
            for (int i = 0; i < actual.Length; i++) actual[i] = crane.Actual(i);
            report.snapshots.Add(new Snapshot
            {
                stage = name,
                elapsed = Time.time - startedTime,
                actual = actual,
                pole = crane.magnet.Pole,
                payload = payload != null ? payload.name : "",
                payloadPosition = payload != null ? payload.transform.position : Vector3.zero,
                attached = payload != null && payload.Attached,
                delivered = crane.deliveryZone.DeliveredCount
            });
        }

        static void BeforeReload()
        {
            if (IsRunning) Finish(false, "La recompilación interrumpió la prueba.");
        }

        static void Finish(bool success, string message)
        {
            IsRunning = false;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            if (restorePauseState && EditorApplication.isPlaying)
                EditorApplication.isPaused = previousPaused;
            restorePauseState = false;
            report.success = success;
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.elapsed = Time.time - startedTime;
            report.message = message;
            if (crane != null && crane.deliveryZone != null) report.delivered = crane.deliveryZone.DeliveredCount;
            LastReportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CraneSmoke.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(LastReportPath));
            File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("CraneSmoke " + (success ? "PASS" : "FAIL") + ": " + message + " Informe: " + LastReportPath);
            if (!success) throw new InvalidOperationException("CraneSmoke FAIL: " + message);
        }
    }
}
