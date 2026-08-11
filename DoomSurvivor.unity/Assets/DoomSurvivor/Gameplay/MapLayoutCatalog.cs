using System;

namespace DoomSurvivor.Gameplay
{
    public enum MapTerrainKind
    {
        Steppe,
        Sandstone,
        Gravel,
        Path,
        Shore,
        Water
    }

    /// <summary>
    /// Authored tile layouts and walkability rules for selectable battle maps.
    /// Rows are stored from the bottom of the map to the top; columns run left to right.
    /// </summary>
    public static class MapLayoutCatalog
    {
        public const string DryHighlandCoastId = "dry_highland_coast";
        public const int DryHighlandCoastColumns = 8;
        public const int DryHighlandCoastRows = 6;

        private static readonly string[] DryHighlandCoastLayout =
        {
            "SSPPGGVW",
            "SSPSSGVW",
            "GGGPPGVW",
            "GGPPGGGW",
            "GRPPSSVW",
            "GRGSSVWW"
        };

        public static bool UsesAuthoredLayout(string mapSkinId) =>
            string.Equals(mapSkinId, DryHighlandCoastId, StringComparison.OrdinalIgnoreCase);

        public static MapTerrainKind GetTerrain(string mapSkinId, int column, int row)
        {
            if (!UsesAuthoredLayout(mapSkinId) || column < 0 || column >= DryHighlandCoastColumns ||
                row < 0 || row >= DryHighlandCoastRows)
                return MapTerrainKind.Steppe;

            return DryHighlandCoastLayout[row][column] switch
            {
                'R' => MapTerrainKind.Sandstone,
                'V' => MapTerrainKind.Gravel,
                'P' => MapTerrainKind.Path,
                'S' => MapTerrainKind.Shore,
                'W' => MapTerrainKind.Water,
                _ => MapTerrainKind.Steppe
            };
        }

        public static string GetTileKey(MapTerrainKind terrain) => terrain switch
        {
            MapTerrainKind.Sandstone => "dry_highland_sandstone",
            MapTerrainKind.Gravel => "dry_highland_gravel",
            MapTerrainKind.Path => "dry_highland_path",
            MapTerrainKind.Shore => "dry_highland_shore",
            MapTerrainKind.Water => "dry_highland_water",
            _ => "dry_highland_steppe"
        };

        public static bool IsWalkable(MapTerrainKind terrain) => terrain != MapTerrainKind.Water;
    }
}
