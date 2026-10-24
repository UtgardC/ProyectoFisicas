using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScrapCrane
{
    public sealed class CraneHUD : MonoBehaviour
    {
        public CraneController crane;
        public static bool EditingNumber { get; private set; }
        static readonly Color Ink = new Color(.075f, .105f, .14f, .96f);
        static readonly Color Muted = new Color(.61f, .69f, .74f);
        static readonly Color Orange = new Color(1f, .67f, .24f);
        static readonly Color Mint = new Color(.4f, .89f, .76f);
        GUIStyle title, small, label, value, number, button, track, thumb, micro, centered;
        readonly string[] input = new string[4];
        Texture2D white, disc;
        static bool settings;
        Vector2 leftStick, rightStick;
        float extensionHeld;
        int operatorHotControl;
        bool releaseCapture;

        static float Scale => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        public static bool PointerOverControls()
        {
            if (Mouse.current == null || Scale <= 0) return false;
            var p = Mouse.current.position.ReadValue();
            p = new Vector2(p.x, Screen.height - p.y) / Scale;
            return new Rect(28, 146, 306, 650).Contains(p) || (settings && new Rect(350, 644, 294, 150).Contains(p));
        }

        void Start()
        {
            settings = false;
            EditingNumber = false;
            if (crane == null) crane = FindFirstObjectByType<CraneController>();
            for (int i = 0; i < 4; i++) input[i] = Format(crane.Targets[i]);
        }

        static string Format(float n) => n.ToString("0.0", CultureInfo.InvariantCulture);

        void Update()
        {
            if (crane == null) return;
            bool pressed = (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);
            if (!crane.OperatorMode || !crane.DrivesEnabled || !Application.isFocused || !pressed)
            {
                CancelOperatorControls();
                return;
            }
            crane.SetOperatorInput(DeadZone(leftStick), DeadZone(rightStick).y, extensionHeld);
        }

        static Vector2 DeadZone(Vector2 input)
        {
            const float threshold = .12f;
            float magnitude = input.magnitude;
            return magnitude <= threshold ? Vector2.zero : input.normalized * Mathf.Clamp01((magnitude - threshold) / (1f - threshold));
        }

        void CancelOperatorControls()
        {
            leftStick = rightStick = Vector2.zero;
            extensionHeld = 0f;
            releaseCapture |= operatorHotControl != 0;
            if (crane != null) crane.StopOperatorInput();
        }

        void OnDisable() { CancelOperatorControls(); EditingNumber = false; }
        void OnApplicationFocus(bool focused) { if (!focused) CancelOperatorControls(); }
        void OnApplicationPause(bool paused) { if (paused) CancelOperatorControls(); }

        void InitStyles()
        {
            white = Texture2D.whiteTexture;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, normal = { textColor = Color.white }, padding = new RectOffset(0, 0, 0, 0) };
            title = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 13, normal = { textColor = Muted } };
            centered = new GUIStyle(label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            disc = MakeDisc();
            micro = new GUIStyle(small) { fontSize = 11 };
            value = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleRight, normal = { textColor = Mint } };
            number = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 16, alignment = TextAnchor.MiddleRight, padding = new RectOffset(7, 9, 4, 4) };
            number.normal.background = MakeTexture(new Color(.15f, .2f, .25f));
            number.focused.background = MakeTexture(new Color(.22f, .29f, .34f));
            number.normal.textColor = Color.white;
            number.focused.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16, fontStyle = FontStyle.Bold, padding = new RectOffset(8, 8, 8, 8) };
            button.normal.background = MakeTexture(new Color(.19f, .25f, .3f));
            button.hover.background = MakeTexture(new Color(.25f, .32f, .37f));
            button.active.background = MakeTexture(new Color(.32f, .39f, .43f));
            button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
            track = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 4, margin = new RectOffset(0, 0, 10, 0) };
            track.normal.background = MakeTexture(new Color(.3f, .36f, .4f), preserveColor: true);
            thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 13, fixedHeight = 20, margin = new RectOffset(0, 0, -8, 0) };
            thumb.normal.background = thumb.hover.background = thumb.active.background = MakeTexture(Orange, preserveColor: true);
        }

        static Color NeutralBackground(Color color)
        {
            Color.RGBToHSV(color, out _, out _, out float brightness);
            return new Color(brightness, brightness, brightness, color.a);
        }

        static Texture2D MakeTexture(Color color, bool preserveColor = false)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, preserveColor ? color : NeutralBackground(color));
            texture.Apply();
            return texture;
        }

        static Texture2D MakeDisc()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            var center = Vector2.one * (size - 1) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(size * .5f - Vector2.Distance(new Vector2(x, y), center)));
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        void Box(Rect rect, Color color, bool preserveColor = false)
        {
            GUI.color = preserveColor ? color : NeutralBackground(color);
            GUI.DrawTexture(rect, white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (crane == null || crane.Targets == null) return;
            if (label == null) InitStyles();
            if (Event.current.type == EventType.MouseLeaveWindow) CancelOperatorControls();
            if (releaseCapture)
            {
                if (GUIUtility.hotControl == operatorHotControl) GUIUtility.hotControl = 0;
                operatorHotControl = 0;
                releaseCapture = false;
            }
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Scale);
            float right = Screen.width / Scale;
            float bottom = Screen.height / Scale;
            Box(new Rect(0, 0, right, 126), new Color(.045f, .07f, .095f, .92f));
            GUI.Label(new Rect(47, 49, 750, 46), "Grúa electromagnética", title);

            int done = crane.deliveryZone.DeliveredCount;
            int total = crane.deliveryZone.requiredCount;
            float rx = right - 300;
            GUI.Label(new Rect(rx, 28, 250, 22), "PIEZAS ENTREGADAS", small);
            GUI.Label(new Rect(rx, 49, 200, 39), done.ToString("00") + " / " + total.ToString("00"), title);
            Box(new Rect(rx, 99, 250, 4), new Color(.23f, .3f, .34f));
            Box(new Rect(rx, 99, 250f * done / Mathf.Max(1, total), 4), Mint);

            if (GUI.Button(new Rect(28, 146, 306, 42), crane.OperatorMode ? "Volver al modo de estudio" : "Controlar como Operador", button))
            {
                CancelOperatorControls();
                GUI.FocusControl(null);
                EditingNumber = false;
                crane.SetOperatorMode(!crane.OperatorMode);
            }
            Box(new Rect(28, 202, 306, 430), Ink);
            if (crane.OperatorMode) OperatorControls();
            else
            {
                GUI.Label(new Rect(48, 220, 170, 24), "MANDOS", small);
                GUI.Label(new Rect(243, 220, 70, 24), "REAL", small);
                for (int i = 0; i < 4; i++) Axis(i, 257 + i * 78);
            }
            Box(new Rect(48, 580, 266, 1), new Color(.25f, .31f, .35f));
            string status = crane.magnet.AttachedLoad != null ? "RETENIDA  ·  " + crane.magnet.CarriedMass.ToString("0") + " kg" :
                            crane.magnet.IsOn ? "CAMPO ACTIVO" : "IMÁN APAGADO";
            GUI.Label(new Rect(48, 596, 265, 28), status, new GUIStyle(small) { normal = { textColor = crane.magnet.IsOn ? Orange : Muted } });

            GUI.backgroundColor = crane.magnet.IsOn ? NeutralBackground(Orange) : Color.white;
            if (GUI.Button(new Rect(28, 644, 306, 48), crane.magnet.IsOn ? "SOLTAR CARGA   [ ESPACIO ]" : "ACTIVAR IMÁN   [ ESPACIO ]", button))
                crane.magnet.Toggle();
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(28, 704, 147, 38), "Reiniciar  [ R ]", button)) crane.Restart();
            if (GUI.Button(new Rect(187, 704, 147, 38), settings ? "Cerrar ajustes" : "Ajustes", button)) settings = !settings;

            if (settings)
            {
                Box(new Rect(350, 644, 294, 150), Ink);
                GUI.Label(new Rect(370, 661, 200, 24), "Esfuerzo máximo", label);
                GUI.Label(new Rect(552, 661, 70, 24), crane.Power.ToString("0.00") + "×", value);
                crane.Power = GUI.HorizontalSlider(new Rect(370, 700, 254, 24), crane.Power, .01f, 3f, track, thumb);
                if (GUI.Button(new Rect(370, 743, 254, 34), crane.DrivesEnabled ? "Desactivar drives" : "Activar drives", button))
                    crane.DrivesEnabled = !crane.DrivesEnabled;
            }

            Box(new Rect(28, bottom - 52, right - 56, 30), new Color(.045f, .07f, .095f, .8f));
            GUI.Label(new Rect(43, bottom - 46, 650, 22), "CÁMARA   Arrastrar con botón izquierdo  /  Rueda para zoom", small);
            GUI.Label(new Rect(right - 400, bottom - 46, 350, 22), "8 CUERPOS   ·   4 DRIVES   ·   1 m = 1 UNIDAD", micro);

            if (!crane.DrivesEnabled)
            {
                Box(new Rect(354, 146, 330, 44), Ink);
                GUI.Label(new Rect(372, 158, 300, 24), "Drives desactivados · gravedad libre", small);
            }
            if (crane.deliveryZone.Complete)
            {
                float cx = right * .5f - 170;
                Box(new Rect(cx, 150, 420, 90), Ink);
                GUI.Label(new Rect(cx + 22, 165, 380, 34), "Maniobra completada", new GUIStyle(label) { fontSize = 25, normal = { textColor = Mint } });
                GUI.Label(new Rect(cx + 22, 205, 380, 24), crane.deliveryZone.DeliveredMass.ToString("0") + " kg recuperados", small);
            }
            EditingNumber = GUI.GetNameOfFocusedControl().StartsWith("target_");
        }

        void OperatorControls()
        {
            GUI.Label(new Rect(48, 220, 266, 24), "PUESTO DE OPERADOR", small);
            GUI.Label(new Rect(48, 257, 154, 24), "GIRO / PLUMA", centered);
            GUI.Label(new Rect(214, 257, 100, 24), "BRAZO", centered);
            GUI.Label(new Rect(73, 282, 100, 24), "Subir", centered);
            GUI.Label(new Rect(214, 282, 100, 24), "Plegar", centered);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && crane.DrivesEnabled;
            leftStick = Joystick(new Vector2(124, 365), 60, leftStick, false);
            rightStick = Joystick(new Vector2(264, 365), 42, rightStick, true);
            GUI.Label(new Rect(41, 350, 22, 28), "←", centered);
            GUI.Label(new Rect(185, 350, 22, 28), "→", centered);
            GUI.Label(new Rect(73, 433, 100, 24), "Bajar", centered);
            GUI.Label(new Rect(214, 433, 100, 24), "Estirar", centered);
            GUI.Label(new Rect(48, 473, 266, 24), "TELESCÓPICO", small);
            MomentaryButton(new Rect(48, 506, 128, 44), "Retraer", -1f);
            MomentaryButton(new Rect(186, 506, 128, 44), "Extender", 1f);
            GUI.enabled = wasEnabled;
        }

        Vector2 Joystick(Vector2 center, float radius, Vector2 input, bool verticalOnly)
        {
            var area = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            int id = GUIUtility.GetControlID(FocusType.Passive, area);
            var evt = Event.current;
            EventType type = evt.GetTypeForControl(id);
            if (GUI.enabled && type == EventType.MouseDown && evt.button == 0 &&
                (evt.mousePosition - center).sqrMagnitude <= radius * radius && GUIUtility.hotControl == 0)
            {
                GUIUtility.hotControl = operatorHotControl = id;
                input = StickPosition(evt.mousePosition, center, radius, verticalOnly);
                evt.Use();
            }
            else if (type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                input = StickPosition(evt.mousePosition, center, radius, verticalOnly);
                evt.Use();
            }
            else if (type == EventType.MouseUp && evt.button == 0 && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = operatorHotControl = 0;
                CancelOperatorControls();
                input = Vector2.zero;
                evt.Use();
            }
            Disc(center, radius + 2f, new Color(.31f, .39f, .43f));
            Disc(center, radius, new Color(.12f, .17f, .22f));
            Box(new Rect(center.x - 1, center.y - radius + 12, 2, radius * 2 - 24), new Color(.27f, .34f, .38f));
            if (!verticalOnly)
                Box(new Rect(center.x - radius + 12, center.y - 1, radius * 2 - 24, 2), new Color(.27f, .34f, .38f));
            Disc(center, 9f, new Color(.3f, .37f, .4f));
            var knob = center + new Vector2(input.x, -input.y) * (radius - 15f);
            Disc(knob, 16f, GUI.enabled ? Orange : Muted, preserveColor: GUI.enabled);
            return input;
        }

        static Vector2 StickPosition(Vector2 pointer, Vector2 center, float radius, bool verticalOnly)
        {
            var offset = new Vector2(pointer.x - center.x, center.y - pointer.y) / (radius - 15f);
            if (verticalOnly) offset.x = 0f;
            return Vector2.ClampMagnitude(offset, 1f);
        }

        void Disc(Vector2 center, float radius, Color color, bool preserveColor = false)
        {
            GUI.color = preserveColor ? color : NeutralBackground(color);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), disc);
            GUI.color = Color.white;
        }

        void MomentaryButton(Rect area, string text, float direction)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, area);
            var evt = Event.current;
            EventType type = evt.GetTypeForControl(id);
            if (GUI.enabled && type == EventType.MouseDown && evt.button == 0 && area.Contains(evt.mousePosition) && GUIUtility.hotControl == 0)
            {
                GUIUtility.hotControl = operatorHotControl = id;
                extensionHeld = direction;
                evt.Use();
            }
            else if (type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                extensionHeld = area.Contains(evt.mousePosition) ? direction : 0f;
                evt.Use();
            }
            else if (type == EventType.MouseUp && evt.button == 0 && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = operatorHotControl = 0;
                CancelOperatorControls();
                evt.Use();
            }
            bool held = GUIUtility.hotControl == id && extensionHeld == direction;
            Box(area, held ? new Color(.48f, .34f, .16f) : new Color(.19f, .25f, .3f));
            GUI.Label(area, text, centered);
        }

        void Axis(int i, float y)
        {
            GUI.Label(new Rect(48, y, 185, 26), crane.Labels[i], label);
            GUI.Label(new Rect(231, y - 1, 82, 28), Format(crane.Actual(i)) + crane.Units[i], value);
            var drive = crane.Joints[i].xDrive;
            float next = GUI.HorizontalSlider(new Rect(48, y + 38, 170, 24), crane.Targets[i], drive.lowerLimit, drive.upperLimit, track, thumb);
            if (Mathf.Abs(next - crane.Targets[i]) > .0001f)
            {
                crane.Targets[i] = next;
                input[i] = Format(next);
                GUI.FocusControl(null);
            }
            string control = "target_" + i;
            if (GUI.GetNameOfFocusedControl() != control) input[i] = Format(crane.Targets[i]);
            GUI.SetNextControlName(control);
            string text = GUI.TextField(new Rect(232, y + 32, 81, 30), input[i] ?? "", 8, number);
            if (text != input[i])
            {
                input[i] = text;
                if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed))
                    crane.Targets[i] = Mathf.Clamp(parsed, drive.lowerLimit, drive.upperLimit);
            }
            GUI.Label(new Rect(48, y + 63, 180, 17), drive.lowerLimit.ToString("0.#") + "  —  " + drive.upperLimit.ToString("0.#") + " " + crane.Units[i], micro);
        }
    }
}
