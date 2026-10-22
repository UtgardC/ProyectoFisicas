using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapCrane
{
    // LegacyRuntime's default font material ignores scene depth. Keep the same
    // TextMesh and font atlas, but render its glyphs behind opaque geometry.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class WorldTextDepth : MonoBehaviour
    {
        const string ShaderResource = "WorldText";
        static readonly int TintProperty = Shader.PropertyToID("_Color");
        static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        static Shader depthShader;

        TextMesh text;
        MeshRenderer textRenderer;
        Material originalMaterial;
        Material depthMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForExistingLabels()
        {
            // Re-register safely when entering Play with domain reload disabled.
            SceneManager.sceneLoaded -= InstallInScene;
            SceneManager.sceneLoaded += InstallInScene;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                InstallInScene(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        static void InstallInScene(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                    if (label.GetComponent<WorldTextDepth>() == null)
                        label.gameObject.AddComponent<WorldTextDepth>();
            }
        }

        void OnEnable()
        {
            // Never replace a scene's serialized font material in Edit Mode.
            if (!Application.isPlaying) return;
            text = GetComponent<TextMesh>();
            textRenderer = GetComponent<MeshRenderer>();
            if (text.font == null) return;

            if (depthShader == null) depthShader = Resources.Load<Shader>(ShaderResource);
            if (depthShader == null)
            {
                Debug.LogWarning("Falta el shader Resources/WorldText para los rótulos 3D.", this);
                return;
            }

            originalMaterial = textRenderer.sharedMaterial;
            depthMaterial = new Material(depthShader)
            {
                name = "Texto 3D con profundidad · " + name,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color tint = Color.white;
            if (originalMaterial != null)
            {
                if (originalMaterial.HasProperty(TintProperty))
                    tint = originalMaterial.GetColor(TintProperty);
                else if (originalMaterial.HasProperty(BaseColorProperty))
                    tint = originalMaterial.GetColor(BaseColorProperty);
            }
            depthMaterial.SetColor(TintProperty, tint);
            RefreshAtlas();
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        void OnFontTextureRebuilt(Font rebuiltFont)
        {
            if (text != null && rebuiltFont == text.font) RefreshAtlas();
        }

        void RefreshAtlas()
        {
            if (text == null || text.font == null || depthMaterial == null || textRenderer == null) return;
            var fontMaterial = text.font.material;
            if (fontMaterial == null) return;
            depthMaterial.mainTexture = fontMaterial.mainTexture;
            depthMaterial.mainTextureScale = fontMaterial.mainTextureScale;
            depthMaterial.mainTextureOffset = fontMaterial.mainTextureOffset;
            textRenderer.sharedMaterial = depthMaterial;
        }

        void OnDisable()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            ReleaseMaterial();
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            ReleaseMaterial();
        }

        void ReleaseMaterial()
        {
            if (depthMaterial == null) return;
            if (textRenderer != null && textRenderer.sharedMaterial == depthMaterial)
                textRenderer.sharedMaterial = originalMaterial;
            if (Application.isPlaying) Destroy(depthMaterial);
            else DestroyImmediate(depthMaterial);
            depthMaterial = null;
            originalMaterial = null;
        }
    }
}
