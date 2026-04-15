using UnityEngine;

namespace KrumpKraft
{
    public static class ConfigLoader
    {
        public static T Load<T>(string filenameWithoutExtension) where T : class
        {
            var asset = Resources.Load<TextAsset>(filenameWithoutExtension);
            if (asset == null)
            {
                Debug.LogError($"[KrumpKraft] Config not found: Resources/{filenameWithoutExtension}");
                return null;
            }
            try
            {
                var result = JsonUtility.FromJson<T>(asset.text);
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[KrumpKraft] Failed to parse {filenameWithoutExtension}: {e.Message}");
                return null;
            }
        }
    }
}
