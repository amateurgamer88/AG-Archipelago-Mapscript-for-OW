//modID=2179145 modFileID=0
//modPlatform=Modio
//AG's Archipelago Map
//Made by amateurgamer
//v0.1
//
//This map script borrows from Archipelago and modifies it as a test mapscript. May see release if there are 
//interest in playing it

using System.Collections.Generic;
using TenCrowns.GameCore;
using Mohawk.SystemCore;
using UnityEngine;

public class MapScriptAGArchipelago : DefaultMapScript
{
    readonly int HillsPercent = 20;
    readonly int MinIslandSizeForPlayerStart = 60;

    List<List<int>> islands = new List<List<int>>();
    Dictionary<int, int> tileIslands = new Dictionary<int, int>();

    MapOptionType eLandmassOption = MapOptionType.NONE;

    readonly MapOptionType LANDMASS_LARGE = MapOptionType.NONE;
    readonly MapOptionType LANDMASS_MEDIUM = MapOptionType.NONE;
    readonly MapOptionType LANDMASS_SMALL = MapOptionType.NONE;

    public static string GetName()
    {
        return "AG's Archipelago";
    }

    public static string GetHelp()
    {
        return "A modified Archipelago mapscript by AG. More landmass overall with some water for naval combat to still be relevant at times.";
    }

    public static bool IncludeInRandom()
    {
        return true;
    }

    public static bool IsHidden()
    {
        return false;
    }

    public new static void GetCustomOptionsMulti(List<MapOptionsMultiType> options, Infos infos)
    {
        options.Add(infos.getType<MapOptionsMultiType>("MAP_OPTIONS_ARCHIPELAGO_LANDMASS"));

        DefaultMapScript.GetCustomOptionsMulti(options, infos);
    }

    public MapScriptAGArchipelago(ref MapParameters mapParameters, Infos infos) : base(ref mapParameters, infos)
    {
        LANDMASS_LARGE = infos.getType<MapOptionType>("MAP_OPTION_ARCHIPELAGO_LANDMASS_LARGE");
        LANDMASS_MEDIUM = infos.getType<MapOptionType>("MAP_OPTION_ARCHIPELAGO_LANDMASS_MEDIUM");
        LANDMASS_SMALL = infos.getType<MapOptionType>("MAP_OPTION_ARCHIPELAGO_LANDMASS_SMALL");

        ShapeBoundaryToMap = false;
        ElevationNoiseAmplitude = 2;
    }

    protected override void InitMapData()
    {
        base.InitMapData();

        MapOptionsMultiType eLandmass = infos.getType<MapOptionsMultiType>("MAP_OPTIONS_ARCHIPELAGO_LANDMASS");
        if (eLandmass != MapOptionsMultiType.NONE)
        {
            if (!mapParameters.gameParams.mapMapMultiOptions.TryGetValue(eLandmass, out eLandmassOption))
            {
                eLandmassOption = infos.mapOptionsMulti(eLandmass).meDefault;
            }
            if (eLandmassOption == LANDMASS_SMALL)
            {
                OceanPercent = 35;
                CoastPercent = 25;
            }
            else if (eLandmassOption == LANDMASS_MEDIUM)
            {
                OceanPercent = 30;
                CoastPercent = 30;
            }
            else if (eLandmassOption == LANDMASS_LARGE)
            {
                OceanPercent = 10;
                CoastPercent = 40;
            }
        }
    }

    protected override void SetMapSize()
    {
        base.SetMapSize();
        mapParameters.iWidth = mapParameters.iWidth * 7 / 5; // increased map dimensions due to its low percentage of workable tiles. (approximately twice as many tiles - sqrt(2) ~ 7/5)
        mapParameters.iHeight = mapParameters.iHeight * 7 / 5;
    }

    protected override void GenerateLand()
    {
        int iBoundaryWidth = infos.Globals.MAP_BOUNDARY_IMMUTABLE_OUTER_WIDTH + 7;
        for (int x = 0; x < MapWidth; ++x)
        {
            for (int y = 0; y < iBoundaryWidth; ++y)
            {
                heightGen.SetValue(x, y, y - iBoundaryWidth);
                heightGen.SetValue(x, MapHeight - y - 1, y - iBoundaryWidth);
            }
        }

        for (int y = 0; y < MapHeight; ++y)
        {
            for (int x = 0; x < iBoundaryWidth; ++x)
            {
                heightGen.SetValue(x, y, x - iBoundaryWidth);
                heightGen.SetValue(MapWidth - x - 1, y, x - iBoundaryWidth);
            }
        }

        heightGen.Normalize();
        heightGen.AddNoise(16, ElevationNoiseAmplitude);
        heightGen.Normalize();

        // Set the lowest tiles to be water
        List<NoiseGenerator.TileValue> tiles = heightGen.GetPercentileRange(0, OceanPercent);
        foreach (NoiseGenerator.TileValue tile in tiles)
        {
            TileData loopTile = GetTile(tile.x, tile.y);
            loopTile.meTerrain = WATER_TERRAIN;
            loopTile.meHeight = infos.Globals.OCEAN_HEIGHT;
        }

        // Set the next lowest tiles to be coast
        tiles = heightGen.GetPercentileRange(OceanPercent, OceanPercent + CoastPercent);
        foreach (NoiseGenerator.TileValue tile in tiles)
        {
            TileData loopTile = GetTile(tile.x, tile.y);
            loopTile.meTerrain = WATER_TERRAIN;
            loopTile.meHeight = infos.Globals.COAST_HEIGHT;
        }


        // Scattered hills, not part of mountain chains.
        hillsGen.AddNoise(MapWidth / 25.0f, 1);
        hillsGen.Normalize();

        tiles = hillsGen.GetPercentileRange(100 - HillsPercent, 100);
        foreach (NoiseGenerator.TileValue tile in tiles)
        {
            TileData loopTile = GetTile(tile.x, tile.y);
            if (loopTile.meHeight == infos.Globals.DEFAULT_HEIGHT)
            {
                loopTile.meHeight = infos.Globals.HILL_HEIGHT;
            }
        }

        ResetDistances();
    }

    protected override bool AddPlayerStarts()
    {
        BuildAreas(islands, null, x => IsLand(x) && !x.isImpassable(infos));
        for (int area = islands.Count - 1; area >= 0; --area)
        {
            foreach (int tileID in islands[area])
            {
                tileIslands[tileID] = area;
            }
            if (islands[area].Count <= MinIslandSizeForPlayerStart)
            {
                islands.RemoveAt(area);
            }
        }

        return base.AddPlayerStarts();
    }

    protected override bool IsValidPlayerStart(TileData tile, PlayerType player, bool bDoMinDistanceCheck = true)
    {
        if (!base.IsValidPlayerStart(tile, player, bDoMinDistanceCheck))
        {
            return false;
        }

        if (bDoMinDistanceCheck)
        {
            bool islandTaken = false;
            foreach (var kvp in playerStarts)
            {
                PlayerType loopPlayer = kvp.First;
                int startTileID = kvp.Second;
                if (startTileID != -1)
                {
                    if (tileIslands[startTileID] == tileIslands[tile.ID])
                    {
                        islandTaken = true;
                    }
                }
            }

            if (islands.Count >= Players.Count && islandTaken)
            {
                return false;
            }
        }

        return true;
    }

    protected override bool AddPlayerStartsTwoTeamMP()
    {
        if (!MirrorMap)
        {
            return base.AddPlayerStartsDefault(); // so each team starts on a different continent, if possible, instead of forcing left and right
        }
        else
        {
            return base.AddPlayerStartsTwoTeamMP();
        }
    }
}
