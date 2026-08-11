using DoomSurvivor.Core;
using UnityEngine;

namespace DoomSurvivor.Presentation
{
    public static class DisplaySettingsService
    {
        private const int PreferredWindowWidth = 1600;
        private const int PreferredWindowHeight = 900;
        private const int WindowMargin = 80;

        public static void Apply(GameSettings settings)
        {
            if (settings == null || Application.isBatchMode)
                return;

            if (settings.Fullscreen)
            {
                var resolution = Screen.currentResolution;
                if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow ||
                    Screen.width != resolution.width ||
                    Screen.height != resolution.height)
                {
                    Screen.SetResolution(
                        resolution.width,
                        resolution.height,
                        FullScreenMode.FullScreenWindow,
                        resolution.refreshRateRatio);
                }

                return;
            }

            var windowSize = GetWindowedResolution(Screen.currentResolution);

            if (Screen.fullScreenMode != FullScreenMode.Windowed ||
                Screen.fullScreen ||
                Screen.width != windowSize.x ||
                Screen.height != windowSize.y)
                Screen.SetResolution(windowSize.x, windowSize.y, FullScreenMode.Windowed);
        }

        public static void ToggleFullscreen(GameSettings settings)
        {
            if (settings == null)
                return;

            settings.Fullscreen = !settings.Fullscreen;
            Apply(settings);
        }

        public static string GetLabel(bool fullscreen)
        {
            return fullscreen ? "退出全屏" : "全屏";
        }

        private static Vector2Int GetWindowedResolution(Resolution displayResolution)
        {
            if (displayResolution.width <= 0 || displayResolution.height <= 0)
                return new Vector2Int(PreferredWindowWidth, PreferredWindowHeight);

            var availableWidth = Mathf.Max(640, displayResolution.width - WindowMargin);
            var availableHeight = Mathf.Max(360, displayResolution.height - WindowMargin);
            var scale = Mathf.Min(
                1f,
                availableWidth / (float)PreferredWindowWidth,
                availableHeight / (float)PreferredWindowHeight);

            return new Vector2Int(
                Mathf.RoundToInt(PreferredWindowWidth * scale),
                Mathf.RoundToInt(PreferredWindowHeight * scale));
        }
    }
}
