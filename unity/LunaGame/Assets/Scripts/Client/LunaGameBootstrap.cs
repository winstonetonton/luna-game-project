using UnityEngine;

namespace LunaGame.Client
{
    public static class LunaGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartClient()
        {
            if (Object.FindFirstObjectByType<LunaGamePresenter>() == null)
                new GameObject("Luna Game").AddComponent<LunaGamePresenter>();
        }
    }
}
