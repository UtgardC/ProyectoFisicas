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
        GUIStyle title, small, label, value, number, button, track, thumb, micro, subtitle;
        readonly string[] input = new string[4];
        Texture2D white;
        static bool settings;

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

        void InitStyles()
        {
            white = Texture2D.whiteTexture;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, normal = { textColor = Color.white }, padding = new RectOffset(0, 0, 0, 0) };
            title = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold };
            subtitle = new GUIStyle(label) { fontSize = 15, normal = { textColor = Muted } };
            small = new GUIStyle(label) { fontSize = 13, normal = { textColor = Muted } };
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
            track.normal.background = MakeTexture(new Color(.3f, .36f, .4f));
            thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 13, fixedHeight = 20, margin = new RectOffset(0, 0, -8, 0) };
            thumb.normal.background = thumb.hover.background = thumb.active.background = MakeTexture(Orange);
        }

        static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        void Box(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (crane == null || crane.Targets == null) return;
            if (label == null) InitStyles();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Scale);
            float right = Screen.width / Scale;
            float bottom = Screen.height / Scale;
            Box(new Rect(0, 0, right, 126), new Color(.045f, .07f, .095f, .92f));
            Box(new Rect(28, 29, 4, 58), Orange);
            GUI.Label(new Rect(48, 25, 700, 22), "OPERACIÓN 01   /   PATIO DE CHATARRA", small);
            GUI.Label(new Rect(47, 49, 750, 46), "Grúa electromagnética", title);
            GUI.Label(new Rect(48, 99, 750, 24), "Recogé las cinco piezas y descargalas en el contenedor verde.", subtitle);

            int done = crane.deliveryZone.DeliveredCount;
            int total = crane.deliveryZone.requiredCount;
            float rx = right - 300;
            GUI.Label(new Rect(rx, 28, 250, 22), "PIEZAS ENTREGADAS", small);
            GUI.Label(new Rect(rx, 49, 200, 39), done.ToString("00") + " / " + total.ToString("00"), title);
            Box(new Rect(rx, 99, 250, 4), new Color(.23f, .3f, .34f));
            Box(new Rect(rx, 99, 250f * done / Mathf.Max(1, total), 4), Mint);

            Box(new Rect(28, 146, 306, 486), Ink);
            GUI.Label(new Rect(48, 166, 170, 24), "MANDOS", small);
            GUI.Label(new Rect(243, 166, 70, 24), "REAL", small);
            for (int i = 0; i < 4; i++) Axis(i, 209 + i * 86);
            Box(new Rect(48, 554, 266, 1), new Color(.25f, .31f, .35f));
            string status = crane.magnet.AttachedLoad != null ? "RETENIDA  ·  " + crane.magnet.CarriedMass.ToString("0") + " kg" :
                            crane.magnet.IsOn ? "CAMPO ACTIVO" : "IMÁN APAGADO";
            GUI.Label(new Rect(48, 574, 265, 28), status, new GUIStyle(small) { normal = { textColor = crane.magnet.IsOn ? Orange : Muted } });

            GUI.backgroundColor = crane.magnet.IsOn ? Orange : Color.white;
            if (GUI.Button(new Rect(28, 644, 306, 48), crane.magnet.IsOn ? "SOLTAR CARGA   [ ESPACIO ]" : "ACTIVAR IMÁN   [ ESPACIO ]", button))
                crane.magnet.Toggle();
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(28, 704, 147, 38), "Reiniciar  [ R ]", button)) crane.Restart();
            if (GUI.Button(new Rect(187, 704, 147, 38), settings ? "Cerrar ajustes" : "Ajustes", button)) settings = !settings;

            if (settings)
            {
                Box(new Rect(350, 644, 294, 150), Ink);
                GUI.Label(new Rect(370, 661, 200, 24), "Potencia de drives", label);
                GUI.Label(new Rect(552, 661, 70, 24), crane.Power.ToString("0.0") + "×", value);
                crane.Power = GUI.HorizontalSlider(new Rect(370, 700, 254, 24), crane.Power, .25f, 1.5f, track, thumb);
                if (GUI.Button(new Rect(370, 743, 254, 34), crane.DrivesEnabled ? "Desactivar drives" : "Activar drives", button))
                    crane.DrivesEnabled = !crane.DrivesEnabled;
            }

            Box(new Rect(28, bottom - 52, right - 56, 30), new Color(.045f, .07f, .095f, .8f));
            GUI.Label(new Rect(43, bottom - 46, 650, 22), "CÁMARA   Arrastrar con botón derecho  /  Rueda para zoom", small);
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
