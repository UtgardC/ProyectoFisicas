using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ScrapCrane.Editor
{
    /// <summary>Rebuilds the self-contained teaching scene using only project-owned primitives.</summary>
    public static class CraneSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/PatioDeChatarra.unity";
        const string MaterialFolder = "Assets/Crane/Materials";
        static Material concrete, asphalt, yellow, orange, steel, dark, rubber, glass, teal, white, rust, cable;

        [MenuItem("Grúa/Crear escena del patio")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            NewMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Patio de chatarra";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f, .76f, .82f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .47f, .48f);
            RenderSettings.ambientGroundColor = new Color(.25f, .28f, .3f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.62f, .7f, .73f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40;
            RenderSettings.fogEndDistance = 90;
            RenderSettings.skybox = null;

            BuildYard();
            var zone = BuildContainer();
            var controller = BuildCrane(zone);
            BuildScrap();
            BuildView(controller.transform);
            Physics.gravity = new Vector3(0, -9.81f, 0);
            Physics.defaultSolverIterations = 16;
            Physics.defaultSolverVelocityIterations = 8;
            PlayerSettings.runInBackground = true;
            PlayerSettings.companyName = "Proyecto de Física";
            PlayerSettings.productName = "Grúa electromagnética";
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = true;

            // The controller repeats this on Awake, because IgnoreCollision is runtime state.
            var colliders = controller.root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                for (int j = i + 1; j < colliders.Length; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j]);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = controller.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1.7f, 2), Quaternion.Euler(30, -40, 0), 16);
            Debug.Log("Patio de chatarra creado: ocho ArticulationBody, cuatro drives y cinco cargas.");
        }

        static void NewMaterials()
        {
            concrete = Material("Hormigón", new Color(.51f, .54f, .53f), 0, .12f);
            asphalt = Material("Asfalto", new Color(.19f, .23f, .24f), 0, .1f);
            yellow = Material("Amarillo maquinaria", new Color(.98f, .66f, .12f), .25f, .38f);
            orange = Material("Naranja seguridad", new Color(.91f, .27f, .07f), .18f, .35f);
            steel = Material("Acero", new Color(.39f, .47f, .5f), .7f, .48f);
            dark = Material("Acero oscuro", new Color(.1f, .16f, .19f), .55f, .35f);
            rubber = Material("Neumático", new Color(.055f, .07f, .075f), .05f, .12f);
            glass = Material("Cristal cabina", new Color(.16f, .37f, .43f), .6f, .8f);
            teal = Material("Contenedor petróleo", new Color(.08f, .36f, .36f), .35f, .34f);
            white = Material("Marcas de piso", new Color(.88f, .87f, .73f), 0, .2f);
            rust = Material("Óxido", new Color(.48f, .25f, .15f), .35f, .2f);
            cable = Material("Cable", new Color(.14f, .18f, .2f), .75f, .3f);
        }

        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Transform Group(string name, Transform parent = null, Vector3 position = default)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
        }

        static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool collider = false, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var oldCollider = go.GetComponent<Collider>();
            if (!collider) UnityEngine.Object.DestroyImmediate(oldCollider);
            else if (type == PrimitiveType.Cylinder)
            {
                UnityEngine.Object.DestroyImmediate(oldCollider);
                var convex = go.AddComponent<MeshCollider>();
                convex.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                convex.convex = true;
            }
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, bool collider = false, Quaternion? rotation = null)
            => Shape(name, PrimitiveType.Cube, parent, position, size, material, collider, rotation);

        static GameObject Cylinder(string name, Transform parent, Vector3 position, float radius, float height,
            Material material, bool collider = false, Quaternion? rotation = null)
            => Shape(name, PrimitiveType.Cylinder, parent, position,
                new Vector3(radius * 2, height * .5f, radius * 2), material, collider, rotation);

        static void Line(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
        {
            var delta = b - a;
            Cylinder(name, parent, (a + b) * .5f, radius, delta.magnitude, material, false,
                Quaternion.FromToRotation(Vector3.up, delta));
        }

        static void Label(string name, Transform parent, string text, Vector3 position,
            Quaternion rotation, float size, Color color)
        {
            var go = Group(name, parent, position).gameObject;
            go.transform.localRotation = rotation;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.color = color;
            go.AddComponent<WorldTextDepth>();
        }

        static void BuildYard()
        {
            var yard = Group("Patio industrial");
            Box("Losa de hormigón", yard, new Vector3(0, -.18f, 1), new Vector3(29, .36f, 29), concrete, true);
            Box("Zona de operación", yard, new Vector3(0, .008f, 1.6f), new Vector3(17, .014f, 18), asphalt);
            for (int i = -2; i <= 2; i++)
            {
                Box("Junta longitudinal", yard, new Vector3(i * 5, .005f, 1), new Vector3(.025f, .008f, 29), dark);
                Box("Junta transversal", yard, new Vector3(0, .005f, 1 + i * 5), new Vector3(29, .008f, .025f), dark);
            }
            for (int i = 0; i < 10; i++)
            {
                Box("Límite izquierdo", yard, new Vector3(-8.25f, .025f, -6.5f + i * 1.75f), new Vector3(.1f, .025f, 1.05f), white);
                Box("Límite derecho", yard, new Vector3(8.25f, .025f, -6.5f + i * 1.75f), new Vector3(.1f, .025f, 1.05f), white);
            }
            var pickup = Group("Área de recogida", yard);
            Border(pickup, new Vector3(0, .025f, 6.55f), new Vector2(5.5f, 3.5f), yellow, .09f);
            Label("Rótulo de recogida", pickup, "CHATARRA", new Vector3(0, .045f, 8.7f),
                Quaternion.Euler(90, 0, 0), .12f, new Color(.82f, .76f, .57f));
            Border(yard, new Vector3(0, .027f, 0), new Vector2(6.5f, 5), yellow, .075f);
            Label("Rótulo de seguridad", yard, "ÁREA DE GIRO", new Vector3(-.2f, .045f, -3.15f),
                Quaternion.Euler(90, 0, 0), .1f, new Color(.82f, .76f, .57f));

            // A quiet background gives the working machine a clear silhouette.
            Box("Nave", yard, new Vector3(-5.8f, 2.65f, 12.8f), new Vector3(12, 5.3f, 4), teal, true);
            Box("Cornisa", yard, new Vector3(-5.8f, 5.35f, 12.8f), new Vector3(12.3f, .22f, 4.25f), dark);
            Box("Portón", yard, new Vector3(-4, 1.95f, 10.77f), new Vector3(4, 3.9f, .08f), dark);
            for (int i = 0; i < 9; i++)
                Box("Lama portón", yard, new Vector3(-4, .35f + i * .4f, 10.705f), new Vector3(3.9f, .025f, .02f), steel);
            Label("Cartel nave", yard, "RECUPERACIÓN  /  04", new Vector3(-4, 4.55f, 10.69f),
                Quaternion.identity, .115f, new Color(.9f, .88f, .71f));
            for (int i = 0; i < 11; i++)
                Box("Nervadura nave", yard, new Vector3(-11.5f + i * 1.1f, 2.7f, 10.76f), new Vector3(.045f, 5.1f, .06f), steel);

            for (int i = 0; i <= 10; i++)
            {
                float z = -11 + i * 2.5f;
                Box("Poste cerco", yard, new Vector3(13.5f, 1.15f, z), new Vector3(.07f, 2.3f, .07f), dark);
                if (i < 10)
                {
                    Line("Cerco superior", yard, new Vector3(13.5f, 2.1f, z), new Vector3(13.5f, 2.1f, z + 2.5f), .018f, steel);
                    Line("Cerco medio", yard, new Vector3(13.5f, 1.1f, z), new Vector3(13.5f, 1.1f, z + 2.5f), .012f, steel);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                var pile = Group("Acero almacenado " + i, yard, new Vector3(10, .15f + i * .2f, 8));
                Box("Perfil", pile, Vector3.zero, new Vector3(2.8f, .18f, .22f), i % 2 == 0 ? rust : steel);
                Box("Perfil", pile, new Vector3(0, 0, .4f), new Vector3(2.8f, .18f, .22f), steel);
            }
            for (int i = 0; i < 3; i++)
                Cone(yard, new Vector3(-4.05f, 0, -2.3f + i * 2.25f));
        }

        static void Border(Transform parent, Vector3 center, Vector2 size, Material material, float width)
        {
            Box("Marca lateral", parent, center + Vector3.left * size.x / 2, new Vector3(width, .025f, size.y), material);
            Box("Marca lateral", parent, center + Vector3.right * size.x / 2, new Vector3(width, .025f, size.y), material);
            Box("Marca frontal", parent, center + Vector3.forward * size.y / 2, new Vector3(size.x, .025f, width), material);
            Box("Marca posterior", parent, center + Vector3.back * size.y / 2, new Vector3(size.x, .025f, width), material);
        }

        static void Cone(Transform parent, Vector3 position)
        {
            var t = Group("Baliza", parent, position);
            Box("Base", t, new Vector3(0, .045f, 0), new Vector3(.45f, .09f, .45f), rubber);
            Cylinder("Cuerpo naranja", t, new Vector3(0, .29f, 0), .13f, .48f, orange);
            Cylinder("Banda reflectiva", t, new Vector3(0, .35f, 0), .135f, .09f, white);
        }

        static DeliveryZone BuildContainer()
        {
            var container = Group("Contenedor de entrega", null, new Vector3(4, 0, -4));
            Box("Piso", container, new Vector3(0, .12f, 0), new Vector3(3.5f, .24f, 3.2f), dark, true);
            Box("Pared izquierda", container, new Vector3(-1.69f, .65f, 0), new Vector3(.12f, 1.06f, 3.2f), teal, true);
            Box("Pared derecha", container, new Vector3(1.69f, .65f, 0), new Vector3(.12f, 1.06f, 3.2f), teal, true);
            Box("Pared posterior", container, new Vector3(0, .65f, 1.54f), new Vector3(3.5f, 1.06f, .12f), teal, true);
            Box("Pared frontal", container, new Vector3(0, .65f, -1.54f), new Vector3(3.5f, 1.06f, .12f), teal, true);
            foreach (float x in new[] { -1.73f, 1.73f })
                for (int i = 0; i < 7; i++)
                    Box("Nervadura", container, new Vector3(x, .65f, -1.35f + i * .45f), new Vector3(.045f, 1.02f, .045f), steel);
            foreach (float z in new[] { -1.56f, 1.56f })
            {
                Box("Borde superior", container, new Vector3(0, 1.19f, z), new Vector3(3.58f, .08f, .13f), yellow);
                Box("Borde inferior", container, new Vector3(0, .21f, z), new Vector3(3.55f, .12f, .13f), dark);
            }
            foreach (float x in new[] { -1.71f, 1.71f })
                Box("Borde superior", container, new Vector3(x, 1.19f, 0), new Vector3(.13f, .08f, 3.2f), yellow);
            Box("Placa frontal", container, new Vector3(0, .72f, -1.615f), new Vector3(1.55f, .43f, .035f), dark);
            Label("Rótulo de entrega", container, "ACERO", new Vector3(0, .72f, -1.64f), Quaternion.identity, .12f, Color.white);
            Border(container, new Vector3(0, .03f, 0), new Vector2(4.25f, 4), white, .085f);
            Label("Rótulo piso destino", container, "ENTREGA", new Vector3(0, .055f, -2.45f), Quaternion.Euler(90, 0, 0), .115f, Color.white);
            var trigger = Group("Volumen interior", container, new Vector3(0, .8f, 0)).gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3.05f, 1.25f, 2.75f);
            var zone = container.gameObject.AddComponent<DeliveryZone>();
            zone.zone = trigger;
            zone.requiredCount = 5;
            return zone;
        }

        static ArticulationBody Body(string name, Transform parent, Vector3 position, Quaternion rotation,
            float mass, ArticulationJointType type)
        {
            var t = Group(name, parent, position);
            t.localRotation = rotation;
            var b = t.gameObject.AddComponent<ArticulationBody>();
            b.jointType = type;
            b.mass = mass;
            b.useGravity = true;
            b.matchAnchors = false;
            b.anchorPosition = Vector3.zero;
            b.anchorRotation = Quaternion.identity;
            b.parentAnchorPosition = position;
            b.parentAnchorRotation = Quaternion.identity;
            b.linearDamping = .1f;
            b.angularDamping = .2f;
            b.jointFriction = .5f;
            b.solverIterations = 24;
            b.solverVelocityIterations = 12;
            return b;
        }

        static void Drive(ArticulationBody body, float lower, float upper, float target,
            float stiffness, float damping, float forceLimit)
        {
            var drive = body.xDrive;
            drive.lowerLimit = lower;
            drive.upperLimit = upper;
            drive.target = target;
            drive.targetVelocity = 0;
            drive.stiffness = stiffness;
            drive.damping = damping;
            drive.forceLimit = forceLimit;
            body.xDrive = drive;
        }

        static CraneController BuildCrane(DeliveryZone zone)
        {
            var root = Body("01 · Chasis inmóvil", null, new Vector3(0, .65f, 0), Quaternion.identity,
                4200, ArticulationJointType.FixedJoint);
            root.immovable = true;
            Box("Chasis", root.transform, Vector3.zero, new Vector3(2.35f, .48f, 3.55f), dark, true);
            Box("Carrocería", root.transform, new Vector3(0, .25f, 0), new Vector3(2.3f, .22f, 3.45f), yellow);
            Box("Paragolpes", root.transform, new Vector3(0, -.05f, -1.9f), new Vector3(2.5f, .28f, .22f), orange);
            Box("Paragolpes", root.transform, new Vector3(0, -.05f, 1.9f), new Vector3(2.5f, .28f, .22f), orange);
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float z in new[] { -1.14f, 1.14f })
                {
                    Cylinder("Neumático", root.transform, new Vector3(side * 1.16f, -.08f, z), .57f, .37f, rubber, false, Quaternion.Euler(0, 0, 90));
                    Cylinder("Llanta", root.transform, new Vector3(side * 1.365f, -.08f, z), .3f, .045f, steel, false, Quaternion.Euler(0, 0, 90));
                    Cylinder("Buje", root.transform, new Vector3(side * 1.395f, -.08f, z), .12f, .07f, yellow, false, Quaternion.Euler(0, 0, 90));
                    Box("Brazo estabilizador", root.transform, new Vector3(side * 1.55f, -.17f, z), new Vector3(1.4f, .2f, .23f), yellow);
                    Cylinder("Pata estabilizadora", root.transform, new Vector3(side * 2.16f, -.34f, z), .09f, .5f, steel);
                    Box("Zapata", root.transform, new Vector3(side * 2.16f, -.58f, z), new Vector3(.62f, .12f, .6f), dark, true);
                }
                for (int stripe = 0; stripe < 6; stripe++)
                    Box("Banda de seguridad", root.transform, new Vector3(side * 1.185f, .08f, -.9f + stripe * .35f), new Vector3(.02f, .15f, .12f), orange,
                        false, Quaternion.Euler(30, 0, 0));
            }
            root.centerOfMass = Vector3.zero;

            var yaw = Body("02 · Torreta [Revolute Y]", root.transform, new Vector3(0, .65f, 0), Quaternion.identity,
                1300, ArticulationJointType.RevoluteJoint);
            yaw.anchorRotation = Quaternion.Euler(0, 0, 90);
            yaw.parentAnchorRotation = Quaternion.Euler(0, 0, 90);
            yaw.twistLock = ArticulationDofLock.LimitedMotion;
            Drive(yaw, -155, 155, 0, 200000, 50000, 140000);
            Cylinder("Corona de giro", yaw.transform, new Vector3(0, -.12f, 0), .95f, .28f, steel, true);
            Cylinder("Plataforma", yaw.transform, new Vector3(0, .08f, 0), 1.08f, .24f, yellow);
            Box("Contrapeso", yaw.transform, new Vector3(.55f, .32f, -.7f), new Vector3(.9f, .6f, 1.3f), orange, true);
            Box("Motor", yaw.transform, new Vector3(.58f, .59f, -.75f), new Vector3(.7f, .12f, 1), dark);
            for (int i = 0; i < 6; i++)
                Box("Ventilación", yaw.transform, new Vector3(.64f, .66f, -1.1f + i * .13f), new Vector3(.45f, .018f, .045f), steel);
            Box("Cabina", yaw.transform, new Vector3(-.8f, .69f, -.45f), new Vector3(.95f, 1.25f, 1.35f), yellow, true);
            Box("Parabrisas", yaw.transform, new Vector3(-.8f, .87f, .236f), new Vector3(.78f, .68f, .035f), glass);
            Box("Ventana lateral", yaw.transform, new Vector3(-1.287f, .87f, -.4f), new Vector3(.035f, .68f, 1.06f), glass);
            Box("Techo", yaw.transform, new Vector3(-.8f, 1.35f, -.45f), new Vector3(1.07f, .1f, 1.47f), dark);
            Line("Pasamanos", yaw.transform, new Vector3(-1.32f, .12f, .35f), new Vector3(-1.32f, .62f, .35f), .025f, steel);
            Cylinder("Luz de aviso", yaw.transform, new Vector3(-.8f, 1.48f, -.8f), .09f, .17f, orange);
            foreach (float x in new[] { -.33f, .33f })
                Box("Oreja soporte pluma", yaw.transform, new Vector3(x, .42f, .2f), new Vector3(.15f, .66f, .68f), dark);
            yaw.centerOfMass = new Vector3(-.1f, .35f, -.4f);

            var boom = Body("03 · Pluma [Revolute X]", yaw.transform, new Vector3(0, .7f, .2f), Quaternion.Euler(-36, 0, 0),
                650, ArticulationJointType.RevoluteJoint);
            boom.twistLock = ArticulationDofLock.LimitedMotion;
            Drive(boom, -68, -5, -36, 1200000, 150000, 180000);
            Box("Viga principal", boom.transform, new Vector3(0, 0, 1.9f), new Vector3(.55f, .6f, 3.8f), yellow, true);
            foreach (float side in new[] { -.29f, .29f })
            {
                Box("Refuerzo lateral", boom.transform, new Vector3(side, 0, 1.9f), new Vector3(.045f, .19f, 3.48f), orange);
                Box("Placa de raíz", boom.transform, new Vector3(side * 1.1f, 0, .28f), new Vector3(.09f, .8f, .9f), yellow);
            }
            Cylinder("Pasador base", boom.transform, Vector3.zero, .25f, .9f, steel, false, Quaternion.Euler(0, 0, 90));
            Cylinder("Pasador codo", boom.transform, new Vector3(0, 0, 3.8f), .23f, .8f, steel, false, Quaternion.Euler(0, 0, 90));
            Line("Camisa hidráulica", boom.transform, new Vector3(0, -.42f, .4f), new Vector3(0, -.42f, 1.65f), .14f, dark);
            Line("Vástago", boom.transform, new Vector3(0, -.42f, 1.65f), new Vector3(0, -.42f, 2.8f), .075f, steel);
            boom.centerOfMass = new Vector3(0, -.03f, 1.9f);

            var elbow = Body("04 · Brazo [Revolute X]", boom.transform, new Vector3(0, 0, 3.8f), Quaternion.Euler(72, 0, 0),
                340, ArticulationJointType.RevoluteJoint);
            elbow.twistLock = ArticulationDofLock.LimitedMotion;
            Drive(elbow, 12, 105, 72, 600000, 80000, 135000);
            Box("Camisa telescópica", elbow.transform, new Vector3(0, 0, 1.3f), new Vector3(.43f, .48f, 2.6f), yellow, true);
            foreach (float side in new[] { -.225f, .225f })
                Box("Perfil brazo", elbow.transform, new Vector3(side, 0, 1.4f), new Vector3(.03f, .13f, 2.2f), orange);
            Cylinder("Articulación codo", elbow.transform, Vector3.zero, .3f, .65f, dark, false, Quaternion.Euler(0, 0, 90));
            Box("Collarín", elbow.transform, new Vector3(0, 0, 2.5f), new Vector3(.53f, .58f, .2f), dark);
            Line("Pistón brazo", elbow.transform, new Vector3(0, .34f, .3f), new Vector3(0, .34f, 1.2f), .1f, dark);
            Line("Vástago brazo", elbow.transform, new Vector3(0, .34f, 1.2f), new Vector3(0, .34f, 2.18f), .055f, steel);
            elbow.centerOfMass = new Vector3(0, 0, 1.3f);

            var extension = Body("05 · Extensión [Prismatic Z]", elbow.transform, new Vector3(0, 0, 3.3f), Quaternion.identity,
                95, ArticulationJointType.PrismaticJoint);
            extension.parentAnchorPosition = new Vector3(0, 0, 2.6f);
            extension.anchorRotation = Quaternion.Euler(0, -90, 0);
            extension.parentAnchorRotation = Quaternion.Euler(0, -90, 0);
            extension.linearLockX = ArticulationDofLock.LimitedMotion;
            extension.linearLockY = ArticulationDofLock.LockedMotion;
            extension.linearLockZ = ArticulationDofLock.LockedMotion;
            Drive(extension, 0, 1.8f, .7f, 65000, 9000, 65000);
            Box("Tramo telescópico", extension.transform, new Vector3(0, 0, -.8f), new Vector3(.28f, .31f, 1.8f), steel, true);
            Box("Cabezal", extension.transform, new Vector3(0, -.04f, 0), new Vector3(.44f, .42f, .35f), dark);
            Cylinder("Eje de suspensión", extension.transform, Vector3.zero, .11f, .63f, orange, false, Quaternion.Euler(0, 0, 90));
            extension.centerOfMass = new Vector3(0, 0, -.65f);

            var link1 = PassiveLink("06 · Eslabón superior [Spherical]", extension.transform, Vector3.zero,
                Quaternion.Inverse(extension.transform.rotation), 18);
            var link2 = PassiveLink("07 · Eslabón inferior [Spherical]", link1.transform, new Vector3(0, -.55f, 0), Quaternion.identity, 18);

            var magnet = Body("08 · Electroimán [Fixed]", link2.transform, new Vector3(0, -.55f, 0), Quaternion.identity,
                180, ArticulationJointType.FixedJoint);
            Cylinder("Carcasa imán", magnet.transform, new Vector3(0, -.13f, 0), .6f, .26f, orange, true);
            Cylinder("Núcleo", magnet.transform, new Vector3(0, -.265f, 0), .54f, .045f, dark);
            Cylinder("Tapa", magnet.transform, new Vector3(0, .025f, 0), .39f, .09f, yellow);
            Cylinder("Oreja de suspensión", magnet.transform, new Vector3(0, .1f, 0), .13f, .14f, steel, false, Quaternion.Euler(0, 0, 90));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Cylinder("Bulón", magnet.transform, new Vector3(Mathf.Cos(angle) * .47f, .01f, Mathf.Sin(angle) * .47f), .035f, .04f, steel);
            }
            magnet.centerOfMass = new Vector3(0, -.12f, 0);
            var magnetControl = magnet.gameObject.AddComponent<ScrapMagnet>();
            magnetControl.magnetBody = magnet;

            var controller = root.gameObject.AddComponent<CraneController>();
            controller.root = root;
            controller.yaw = yaw;
            controller.boom = boom;
            controller.elbow = elbow;
            controller.extension = extension;
            controller.magnet = magnetControl;
            controller.deliveryZone = zone;
            controller.loadSpawn = Group("Punto de recogida", null, new Vector3(0, .35f, 5.9f));
            var hud = root.gameObject.AddComponent<CraneHUD>();
            hud.crane = controller;
            return controller;
        }

        static ArticulationBody PassiveLink(string name, Transform parent, Vector3 position, Quaternion rotation, float mass)
        {
            var body = Body(name, parent, position, rotation, mass, ArticulationJointType.SphericalJoint);
            // Twist follows the cable's vertical axis; both horizontal swings remain passive.
            // Matching world anchor frames also gives zero initial reduced coordinates.
            body.anchorRotation = Quaternion.Euler(0, 0, 90);
            body.parentAnchorRotation = Quaternion.Inverse(parent.rotation) * body.transform.rotation * body.anchorRotation;
            body.twistLock = ArticulationDofLock.LockedMotion;
            body.swingYLock = ArticulationDofLock.LimitedMotion;
            body.swingZLock = ArticulationDofLock.LimitedMotion;
            var drive = body.yDrive;
            drive.lowerLimit = -32;
            drive.upperLimit = 32;
            drive.stiffness = 0;
            drive.damping = 20;
            drive.forceLimit = 1000;
            body.yDrive = drive;
            body.zDrive = drive;
            body.angularDamping = 1;
            body.centerOfMass = new Vector3(0, -.275f, 0);
            // A narrow box gives the cable a predictable collision volume.
            Box("Cable estructural", body.transform, new Vector3(0, -.275f, 0), new Vector3(.055f, .55f, .055f), cable, true);
            Cylinder("Terminal superior", body.transform, new Vector3(0, -.04f, 0), .075f, .08f, steel);
            Cylinder("Terminal inferior", body.transform, new Vector3(0, -.51f, 0), .075f, .08f, steel);
            return body;
        }

        static void BuildScrap()
        {
            var loads = Group("Cargas de acero");
            Vector3[] positions = {
                new Vector3(0, .37f, 5.9f), new Vector3(-1.35f, .37f, 6.05f),
                new Vector3(1.35f, .37f, 6.05f), new Vector3(-.7f, .37f, 7.25f),
                new Vector3(.75f, .37f, 7.25f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var t = Group("Carga " + (i + 1).ToString("00") + " · " + (80 + i * 15) + " kg", loads, positions[i]);
                t.localRotation = Quaternion.Euler(0, i * 23 - 15, 0);
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = 80 + i * 15;
                rb.linearDamping = .2f;
                rb.angularDamping = .3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.solverIterations = 16;
                rb.solverVelocityIterations = 8;
                Box("Placa inferior", t, new Vector3(0, -.22f, 0), new Vector3(.85f, .12f, .72f), i % 2 == 0 ? rust : steel, true);
                Box("Perfil izquierdo", t, new Vector3(-.23f, -.045f, 0), new Vector3(.12f, .25f, .8f), steel, true);
                Box("Perfil derecho", t, new Vector3(.23f, -.045f, .05f), new Vector3(.15f, .25f, .7f), rust, true);
                Box("Placa de captación", t, new Vector3(0, .13f, 0), new Vector3(.7f, .1f, .63f), steel, true,
                    Quaternion.Euler(0, 6, 0));
                Box("Fleje", t, new Vector3(0, .19f, -.05f), new Vector3(.06f, .025f, .67f), dark);
                t.gameObject.AddComponent<ScrapLoad>();
            }
        }

        static void BuildView(Transform crane)
        {
            var sun = Group("Sol de tarde").gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            sun.intensity = 2.2f;
            sun.color = new Color(1, .9f, .74f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            sun.shadowBias = .025f;
            QualitySettings.shadowDistance = 55;
            var focus = Group("Foco de cámara", null, new Vector3(0, 1.65f, 2));
            var camera = Group("Cámara de operación", null, new Vector3(-13.5f, 11.5f, -15.5f)).gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.LookAt(focus);
            camera.fieldOfView = 46;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 140;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.59f, .68f, .72f);
            camera.gameObject.AddComponent<AudioListener>();
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = focus;
        }
    }
}
