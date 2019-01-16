using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Linq;

namespace RandomCampaignStart
{
    public class ModSettings
    {
        /*   TAG RANDOM SETTINGS    */
        //Excluded Mechs for all starts
        public List<string> ExcludedMechs = new List<string>();

        //Excluded Tags for all starts (TagRandomLance only; "BLACKLISTED" added to this list)
        public List<string> ExcludedTags = new List<string>();

        //List of Starting Systems
        public List<string> startSystemList = new List<string>();

        //Allowed Tag Start Variables
        public List<List<string>> AllowedTags = new List<List<string>>();


        /*     LEGACY SETTINGS      */
        public float LegacyMinStartingWeight = 165;
        public float LegacyMaxStartingWeight = 175;
        public float LegacyMaxMechWeight = 50;
        public float LegacyMinMechWeight = 20;

        public List<string> startTypes = new List<string>();
        public List<List<string>> startTypeOptions = new List<List<string>>();

        //Vanilla Start variables
        public List<string> AssaultMechsPossible = new List<string>();
        public List<string> HeavyMechsPossible = new List<string>();
        public List<string> LightMechsPossible = new List<string>();
        public List<string> MediumMechsPossible = new List<string>();

        public int NumberAssaultMechs = 0;
        public int NumberHeavyMechs = 0;
        public int NumberLightMechs = 3;
        public int NumberMediumMechs = 1;

        //Pirate Faction variables
        public List<string> pirateAssaultMechsPossible = new List<string>();
        public List<string> pirateHeavyMechsPossible = new List<string>();
        public List<string> pirateLightMechsPossible = new List<string>();
        public List<string> pirateMediumMechsPossible = new List<string>();

        public int pirateNumberAssaultMechs = 0;
        public int pirateNumberHeavyMechs = 0;
        public int pirateNumberLightMechs = 3;
        public int pirateNumberMediumMechs = 1;

        //Inner Sphere variables
        public List<string> innerAssaultMechsPossible = new List<string>();
        public List<string> innerHeavyMechsPossible = new List<string>();
        public List<string> innerLightMechsPossible = new List<string>();
        public List<string> innerMediumMechsPossible = new List<string>();

        public int innerNumberAssaultMechs = 0;
        public int innerNumberHeavyMechs = 0;
        public int innerNumberLightMechs = 3;
        public int innerNumberMediumMechs = 1;

        //Periphery Start Variables
        public List<string> periAssaultMechsPossible = new List<string>();
        public List<string> periHeavyMechsPossible = new List<string>();
        public List<string> periLightMechsPossible = new List<string>();
        public List<string> periMediumMechsPossible = new List<string>();

        public int periNumberAssaultMechs = 0;
        public int periNumberHeavyMechs = 0;
        public int periNumberLightMechs = 3;
        public int periNumberMediumMechs = 1;

        //Deep Periphery Start Variables
        public List<string> deepAssaultMechsPossible = new List<string>();
        public List<string> deepHeavyMechsPossible = new List<string>();
        public List<string> deepLightMechsPossible = new List<string>();
        public List<string> deepMediumMechsPossible = new List<string>();

        public int deepNumberAssaultMechs = 0;
        public int deepNumberHeavyMechs = 0;
        public int deepNumberLightMechs = 3;
        public int deepNumberMediumMechs = 1;

        //Clan Start variables
        public List<string> clanAssaultMechsPossible = new List<string>();
        public List<string> clanHeavyMechsPossible = new List<string>();
        public List<string> clanLightMechsPossible = new List<string>();
        public List<string> clanMediumMechsPossible = new List<string>();

        public int clanNumberAssaultMechs = 0;
        public int clanNumberHeavyMechs = 0;
        public int clanNumberLightMechs = 3;
        public int clanNumberMediumMechs = 1;

        //Starting Lance
        public List<string> startingLance = new List<string>();

        /*  OTHER RANDOM SETTINGS   */
        public float MinimumStartingWeight = 165;
        public float MaximumStartingWeight = 175;
        public float MaximumMechWeight = 50;
        public float MinimumMechWeight = 20;
        public int MinimumLanceSize = 4;
        public int MaximumLanceSize = 6;
        public bool AllowCustomMechs = false;
        public bool AllowDuplicateChassis = false;
        public float MechPercentageStartingCost = 0.2f;

        public List<string> StartingRonin = new List<string>();
        public int NumberRoninFromList = 4;

        public int NumberProceduralPilots = 0;
        public int NumberRandomRonin = 4;

        public bool RemoveAncestralMech = false;
        public bool IgnoreAncestralMech = true;

        public string ModDirectory = string.Empty;
        public bool Debug = false;
        public int SpiderLoops = 1000;
        public int Loops = 1;

        //Mode Toggles
        public bool NotRandomMode = true;
        public bool TagRandomLance = true;
    }
}
