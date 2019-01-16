using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Linq;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace RandomCampaignStart
{
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        // from https://stackoverflow.com/questions/273313/randomize-a-listt
        private static readonly Random rng = new Random();

        private static void RNGShuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private static List<T> GetRandomSubList<T>(List<T> list, int number)
        {
            var subList = new List<T>();

            if (list.Count <= 0 || number <= 0)
                return subList;

            var randomizeMe = new List<T>(list);
            
            // add enough duplicates of the list to satisfy the number specified
            while (randomizeMe.Count < number)
                randomizeMe.AddRange(list);

            randomizeMe.RNGShuffle();
            for (var i = 0; i < number; i++)
                subList.Add(randomizeMe[i]);

            return subList;
        }
        
        public static void Postfix(SimGameState __instance)
        {
            if (RngStart.Settings.NumberRandomRonin + RngStart.Settings.NumberProceduralPilots + RngStart.Settings.NumberRoninFromList > 0)
            {
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }
                List<PilotDef> list = new List<PilotDef>();
                
                if (RngStart.Settings.StartingRonin != null)
                {
                    var RoninRandomizer = new List<string>();
                    RoninRandomizer.AddRange(GetRandomSubList(RngStart.Settings.StartingRonin, RngStart.Settings.NumberRoninFromList));
                    foreach (var roninID in RoninRandomizer)
                    {
                        var pilotDef = __instance.DataManager.PilotDefs.Get(roninID);

                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                            __instance.AddPilotToRoster(pilotDef, true);
                    }
                }

                if (RngStart.Settings.NumberRandomRonin > 0)
                {
                    List<PilotDef> list2 = new List<PilotDef>(__instance.RoninPilots);
                    for (int m = list2.Count - 1; m >= 0; m--)
                    {
                        for (int n = 0; n < __instance.PilotRoster.Count; n++)
                        {
                            if (list2[m].Description.Id == __instance.PilotRoster[n].Description.Id)
                            {
                                list2.RemoveAt(m);
                                break;
                            }
                        }
                    }
                    list2.RNGShuffle<PilotDef>();
                    for (int i = 0; i < RngStart.Settings.NumberRandomRonin; i++)
                    {
                        list.Add(list2[i]);
                    }
                }

                if (RngStart.Settings.NumberProceduralPilots > 0)
                {
                    List<PilotDef> list3;
                    List<PilotDef> collection = __instance.PilotGenerator.GeneratePilots(RngStart.Settings.NumberProceduralPilots, 1, 0f, out list3);
                    list.AddRange(collection);
                }
                foreach (PilotDef def in list)
                {
                    __instance.AddPilotToRoster(def, true);
                }
            }

            //Logger.Debug($"Starting lance creation {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
            // mechs
            if (!RngStart.Settings.TagRandomLance)
            {
                var AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
                bool RemoveAncestralMech = RngStart.Settings.RemoveAncestralMech;
                if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY")
                {
                    RemoveAncestralMech = true;
                }
                var lance = new List<MechDef>();
                float currentLanceWeight = 0;
                var baySlot = 1;

                // clear the initial lance
                for (var i = 1; i < 6; i++)
                {
                    __instance.ActiveMechs.Remove(i);
                }


                // memoize dictionary of tonnages since we may be looping a lot
                //Logger.Debug($"Memoizing");
                var mechTonnages = new Dictionary<string, float>();
                foreach (var kvp in __instance.DataManager.ChassisDefs)
                {
                    if (kvp.Key.Contains("DUMMY") && !kvp.Key.Contains("CUSTOM"))
                    {
                        // just in case someone calls their mech DUMMY
                        continue;
                    }
                    if (kvp.Key.Contains("CUSTOM") || kvp.Key.Contains("DUMMY"))
                    {
                        continue;
                    }
                    if (RngStart.Settings.MaximumMechWeight != 100)
                    {
                        if (kvp.Value.Tonnage > RngStart.Settings.LegacyMaxMechWeight || kvp.Value.Tonnage < RngStart.Settings.LegacyMinMechWeight)
                        {
                            continue;
                        }
                    }
                    // passed checks, add to Dictionary
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }

                bool firstrun = true;
                for (int xloop = 0; xloop < RngStart.Settings.Loops; xloop++)
                {
                    int LanceCounter = 1;
                    if (!RngStart.Settings.NotRandomMode)
                    {
                        
                        // remove ancestral mech if specified
                        if (RemoveAncestralMech && firstrun)
                        {
                            //Logger.Debug($"Lance Size(Legacy1): {lance.Count}");
                            __instance.ActiveMechs.Remove(0);
                            //Logger.Debug($"Lance Size(Legacy2): {lance.Count}");
                        }
                        
                        while (currentLanceWeight < RngStart.Settings.LegacyMinStartingWeight || currentLanceWeight > RngStart.Settings.LegacyMaxStartingWeight)
                        {
                            if (!firstrun)
                            {
                                for (var i = baySlot; i < 6; i++)
                                {
                                    __instance.ActiveMechs.Remove(i);
                                }
                                currentLanceWeight = 0;
                            }

                            if (RemoveAncestralMech == true)
                            {
                                baySlot = 0;
                            }
                            else
                            {
                                currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                                baySlot = 1;
                            }
                            
                            //It's not a BUG, it's a FEATURE.
                            LanceCounter++;
                            if (LanceCounter > RngStart.Settings.SpiderLoops)
                            {
                                MechDef mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID(), true);
                                lance.Add(mechDefSpider); // worry about sorting later
                                for (int j = baySlot; j < 6; j++)
                                {
                                    __instance.AddMech(j, mechDefSpider, true, true, false, null);
                                }
                                break;
                            }

                            var legacyLance = new List<string>();
                            for (int i = 0; i < RngStart.Settings.startTypes.Count; i++)
                            {
                                foreach (var planet in RngStart.Settings.startTypeOptions[i])
                                {
                                    if (planet == __instance.Constants.Story.StartingTargetSystem)
                                    {
                                        if (RngStart.Settings.startTypes[i] == "InnerSphere")
                                        {
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));
                                        }

                                        if (RngStart.Settings.startTypes[i] == "Periphery")
                                        {
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));
                                        }

                                        if (RngStart.Settings.startTypes[i] == "DeepPeriphery")
                                        {
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));
                                        }

                                        if (RngStart.Settings.startTypes[i] == "Clan")
                                        {
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanAssaultMechsPossible, RngStart.Settings.clanNumberAssaultMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanHeavyMechsPossible, RngStart.Settings.clanNumberHeavyMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanMediumMechsPossible, RngStart.Settings.clanNumberMediumMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanLightMechsPossible, RngStart.Settings.clanNumberLightMechs));
                                        }

                                        if (RngStart.Settings.startTypes[i] == "Pirates")
                                        {
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                            legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));
                                        }
                                    }
                                }
                            }
                            

                            // check to see if we're on the last mechbay and if we have more mechs to add
                            // if so, store the mech at index 5 before next iteration.
                            for (int j = 0; j < legacyLance.Count; j++)
                            {
                                Logger.Debug($"Build Lance");

                                MechDef mechDef2 = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[j]), __instance.GenerateSimGameUID(), true);
                                __instance.AddMech(baySlot, mechDef2, true, true, false, null);
                                if (baySlot == 5 && j + 1 < legacyLance.Count)
                                {
                                    __instance.UnreadyMech(5, mechDef2);
                                }
                                else
                                {
                                    baySlot++;
                                }
                                currentLanceWeight += (int)mechDef2.Chassis.Tonnage;
                            }
                            
                            firstrun = false;
                            if (currentLanceWeight >= RngStart.Settings.MinimumStartingWeight && currentLanceWeight <= RngStart.Settings.MaximumStartingWeight)
                            {
                                Logger.Debug($"Classic Mode");
                                for (int y = 0; y < __instance.ActiveMechs.Count(); y++)
                                {
                                    Logger.Debug($"Mech {y}: {__instance.ActiveMechs[y].Description.Id}");
                                }
                            }
                            else
                            {
                                Logger.Debug($"Illegal Lance");
                                Logger.Debug($"Weight: {currentLanceWeight}");
                            }
                        }

                    }
                    else  // G new mode
                    {
                        //Logger.Debug($"New mode");

                        // cap the lance tonnage
                        int minLanceSize = RngStart.Settings.MinimumLanceSize;
                        float maxWeight = RngStart.Settings.MaximumStartingWeight;
                        float maxLanceSize = 6;
                        //bool firstTargetRun = true;
                        
                        currentLanceWeight = 0;
                        if (RemoveAncestralMech == true)
                        {
                            baySlot = 0;
                            if (RngStart.Settings.IgnoreAncestralMech)
                            {
                                maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
                                minLanceSize = minLanceSize + 1;
                            }
                        }
                        else if ((!RemoveAncestralMech && RngStart.Settings.IgnoreAncestralMech))
                        {
                            lance.Add(AncestralMechDef);
                            maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
                        }
                        else
                        {
                            baySlot = 1;
                            lance.Add(AncestralMechDef);
                            currentLanceWeight += AncestralMechDef.Chassis.Tonnage;
                            Logger.Debug($"Weight w/Ancestral: {currentLanceWeight}");
                        }

                        bool dupe = false;
                        bool excluded = false;
                        bool blacklisted = false;
                        bool TargetDummy = false;
                        while (minLanceSize > lance.Count || currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                        {

                            #region Def listing loops

                            //Logger.Debug($"In while loop");
                            //foreach (var mech in __instance.DataManager.MechDefs)
                            //{
                            //    Logger.Debug($"K:{mech.Key} V:{mech.Value}");
                            //}
                            //foreach (var chasis in __instance.DataManager.ChassisDefs)
                            //{
                            //    Logger.Debug($"K:{chasis.Key}");
                            //}
                            #endregion


                            // build lance collection from dictionary for speed
                            var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                            var mechString = randomMech.Key.Replace("chassisdef", "mechdef");
                            // getting chassisdefs so renaming the key to match mechdefs Id
                            //var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                            var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                            //It's not a BUG, it's a FEATURE.
                            if (LanceCounter > RngStart.Settings.SpiderLoops)
                            {
                                MechDef mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID(), true);
                                lance.Add(mechDefSpider); // worry about sorting later
                                for (int j = baySlot; j < 6; j++)
                                {
                                    __instance.AddMech(j, mechDefSpider, true, true, false, null);
                                }
                                break;
                            }

                            /*for (int i = 0; i < mechDef.MechTags.Count; i++)
                            {
                                Logger.Debug($"MechTags: {mechDef.MechTags[i]}");
                            }*/
                            if (mechDef.MechTags.Contains("BLACKLISTED"))
                            {
                                blacklisted = true;

                                Logger.Debug($"Blacklisted! {mechDef.Name}");
                            }

                            if (!RngStart.Settings.AllowDuplicateChassis)
                            {
                                foreach (var mech in lance)
                                {
                                    if (mech.Name == mechDef.Name)
                                    {
                                        dupe = true;

                                        Logger.Debug($"SAME SAME! {mech.Name}\t\t{mechDef.Name}");
                                    }
                                }
                            }


                            // does the mech fit into the lance?
                            if (TargetDummy)
                            {
                                TargetDummy = false;
                            }
                            else
                            {
                                //currentLanceWeight = currentLanceWeight + mechDef.Chassis.Tonnage;
                            }

                            if (!blacklisted && !dupe && !excluded)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                blacklisted = false;
                                dupe = false;
                                excluded = false;
                            }

                            Logger.Debug($"Lance Counter: {LanceCounter}");
                            LanceCounter++;

                            //if (currentLanceWeight > RngStart.Settings.MinimumStartingWeight + mechDef.Chassis.Tonnage)
                            //Logger.Debug($"Minimum lance tonnage met:  done");

                            //Logger.Debug($"current: {currentLanceWeight} tons. " +
                            //    $"tonnage remaining: {RngStart.Settings.MaximumStartingWeight - currentLanceWeight}. " +
                            //    $"before lower limit hit: {Math.Max(0, RngStart.Settings.MinimumStartingWeight - currentLanceWeight)}");
                            
                            // invalid lance, reset
                            if (lance.Count >= maxLanceSize && currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                            {
                                Logger.Debug($"Weight: {currentLanceWeight}");
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                var lightest = lance[0];
                                for (int i = 0; i < lance.Count; i++)
                                {
                                    if (lightest.Chassis.Tonnage > lance[i].Chassis.Tonnage)
                                    {
                                        Logger.Debug($"Mech: {lance[i].Name}");
                                        Logger.Debug($"Weight: {lance[i].Chassis.Tonnage}");
                                        lightest = lance[i];
                                    }
                                    
                                }
                                lance.Remove(lightest);
                                currentLanceWeight -= lightest.Chassis.Tonnage;
                            }
                                
                            if(lance.Count < minLanceSize && currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
                            {
                                Logger.Debug($"Weight: {currentLanceWeight}");
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                var heaviest = lance[0];
                                for (int i = 0; i < lance.Count; i++)
                                {
                                    if(heaviest.Chassis.Tonnage < lance[i].Chassis.Tonnage)
                                    {
                                        Logger.Debug($"Mech: {lance[i].Name}");
                                        Logger.Debug($"Weight: {lance[i].Chassis.Tonnage}");
                                        heaviest = lance[i];
                                    }
                                    
                                }
                                lance.Remove(heaviest);
                                currentLanceWeight -= heaviest.Chassis.Tonnage;
                            }
                        }
                        Logger.Debug($"New mode");
                        Logger.Debug($"Starting lance instantiation");

                        float tonnagechecker = 0;
                        for (int x = 0; x < lance.Count; x++)
                        {
                            Logger.Debug($"x is {x} and lance[x] is {lance[x].Name}");
                            __instance.AddMech(x, lance[x], true, true, false);
                            tonnagechecker = tonnagechecker + lance[x].Chassis.Tonnage;
                        }
                        Logger.Debug($"{tonnagechecker}");
                        float Maxtonnagedifference = tonnagechecker - RngStart.Settings.MaximumStartingWeight;
                        float Mintonnagedifference = tonnagechecker - RngStart.Settings.MinimumStartingWeight;
                        Logger.Debug($"Over tonnage Maximum amount: {Maxtonnagedifference}");
                        Logger.Debug($"Over tonnage Minimum amount: {Mintonnagedifference}");
                        lance.Clear();
                        // valid lance created
                    }
                }
            }
            else
            {
                // clear the initial lance
                for (var i = 1; i < 6; i++)
                {
                    __instance.ActiveMechs.Remove(i);
                }
                var lance = PickLanceTagRandom(__instance);

                float tonnagechecker = 0;
                for (int x = 0; x < lance.Count; x++)
                {
                    Logger.Debug($"x is {x} and lance[x] is {lance[x].Name}");
                    __instance.AddMech(x, lance[x], true, true, false);
                    tonnagechecker = tonnagechecker + lance[x].Chassis.Tonnage;
                }
                Logger.Debug($"{tonnagechecker}");
                float Maxtonnagedifference = tonnagechecker - RngStart.Settings.MaximumStartingWeight;
                float Mintonnagedifference = tonnagechecker - RngStart.Settings.MinimumStartingWeight;
                Logger.Debug($"Over tonnage Maximum amount: {Maxtonnagedifference}");
                Logger.Debug($"Over tonnage Minimum amount: {Mintonnagedifference}");
                lance.Clear();
            }

            //Cheats.MyMethod(__instance);
        }

        private static List<MechDef> PickLanceTagRandom(SimGameState __instance)
        {
            var lance = new List<MechDef>();
            float currentLanceWeight = 0;
            int minLanceSize = RngStart.Settings.MinimumLanceSize;
            float maxWeight = RngStart.Settings.MaximumStartingWeight;
            float maxLanceSize = 6;

            //Find Starting Mech and if Starting Mech is used
            var AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
            bool RemoveAncestralMech = RngStart.Settings.RemoveAncestralMech;
            if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY") {
                RemoveAncestralMech = true;
            }

            if (RemoveAncestralMech) {
                if (RngStart.Settings.IgnoreAncestralMech) {
                    maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
                    minLanceSize = minLanceSize + 1;
                }
            } else if (RngStart.Settings.IgnoreAncestralMech) {
                lance.Add(AncestralMechDef);
                maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
            } else {
                lance.Add(AncestralMechDef);
                currentLanceWeight += AncestralMechDef.Chassis.Tonnage;
                Logger.Debug($"Weight w/Ancestral: {currentLanceWeight}");
            }

            var system = RngStart.Settings.startSystemList.IndexOf(__instance.Constants.Story.StartingTargetSystem);
            if (system < 0) {
                Logger.LogLine($"Starting system not found: {__instance.Constants.Story.StartingTargetSystem} not in startSystemList; pretending it was {RngStart.Settings.startSystemList[0]} instead");
                system = 0;
            }

            // Put together the list of valid mechs
            var mechs = new List<MechDef>();
            foreach (var kvp in __instance.DataManager.MechDefs)
            {
                var mech = kvp.Value;
                var id = kvp.Key;
                if (id.Contains("CUSTOM") || id.Contains("DUMMY")) {
                    continue;
                }

                if (RngStart.Settings.MaximumMechWeight != 100) {
                    if (mech.Chassis.Tonnage > RngStart.Settings.MaximumMechWeight ||
                        mech.Chassis.Tonnage < RngStart.Settings.MinimumMechWeight) {
                        continue;
                    }
                }

                if (id != mech.Description.Id) {
                    Logger.Debug($"id mismatch {kvp.Key} vs {kvp.Value.Description.Id} ");
                    continue;
                }

                if (RngStart.Settings.ExcludedMechs.Contains(id)) {
                    Logger.Debug($"Excluded! {id}");
                    continue;
                }

                bool hasTag = false;
                foreach (var tag in mech.MechTags) {
                    if (RngStart.Settings.AllowedTags[system].Contains(tag)) {
                        hasTag = true;
                        break;
                    }
                    if (RngStart.Settings.ExcludedTags.Contains(tag)) {
                        hasTag = false;
                        break;
                    }
                }
                if (!hasTag) {
                    Logger.Debug($"No matching tags or an excluded tag {id}");
                    continue;
                }

                if (mech.MechTags.Contains("BLACKLISTED")) {
                    Logger.Debug($"Blacklisted! {mech.Name}");
                    continue;
                }

                mechs.Add(mech);
            }

            Logger.Debug($"TagRandom Mode");

            while (lance.Count < minLanceSize || currentLanceWeight < RngStart.Settings.MinimumStartingWeight) {

                Logger.Debug($"Begin Mech Finder Loop");

                var randomMech = mechs[rng.Next(0, mechs.Count)];

                if (!RngStart.Settings.AllowDuplicateChassis) {
                    if (lance.Find(x => randomMech.Name == x.Name) != null) {
                        Logger.Debug($"SAME SAME! {randomMech.Name}");
                        continue;
                    }
                }

                var mechDef = new MechDef(randomMech, __instance.GenerateSimGameUID());

                Logger.Debug($"Lance Count-1: {lance.Count}");

                lance.Add(mechDef);
                currentLanceWeight += mechDef.Chassis.Tonnage;

                Logger.Debug($"Lance Count-2: {lance.Count}");
                Logger.Debug($"Adding mech {mechDef} {mechDef.Chassis.Tonnage} tons");


                if (lance.Count >= maxLanceSize && currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                {
                    Logger.Debug($"Under-Weight: {currentLanceWeight}");
                    Logger.Debug($"Lance Count: {lance.Count}");

                    var lightest = lance[1];
                    for (int i = 1; i < lance.Count; i++)
                    {
                        if (lightest.Chassis.Tonnage > lance[i].Chassis.Tonnage)
                        {
                            Logger.Debug($"Mech: {lance[i].Name}");
                            Logger.Debug($"Weight: {lance[i].Chassis.Tonnage}");
                            lightest = lance[i];
                        }
                    }
                    lance.Remove(lightest);
                    currentLanceWeight -= lightest.Chassis.Tonnage;
                }

                if (lance.Count < minLanceSize && currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
                {
                    Logger.Debug($"Over-Weight: {currentLanceWeight}");
                    Logger.Debug($"Lance Count-1: {lance.Count}");

                    var heaviest = lance[1];
                    for (int i = 1; i < lance.Count; i++)
                    {
                        if (heaviest.Chassis.Tonnage < lance[i].Chassis.Tonnage)
                        {
                            Logger.Debug($"Mech: {lance[i].Name}");
                            Logger.Debug($"Weight: {lance[i].Chassis.Tonnage}");
                            heaviest = lance[i];
                        }
                    }
                    lance.Remove(heaviest);
                    currentLanceWeight -= heaviest.Chassis.Tonnage;
                }
            }

            return lance;
        }

        [HarmonyPatch(typeof(SimGameState), "_OnDefsLoadComplete")]
        public static class Initialize_New_Game
        {
            public static void Postfix(SimGameState __instance)
            {
                float cost = 0;
                foreach (MechDef mechdef in __instance.ActiveMechs.Values)
                {
                    cost += mechdef.Description.Cost * RngStart.Settings.MechPercentageStartingCost/100;
                }
                __instance.AddFunds(-(int)cost, null, false);
            }
        }

        public static class RngStart
        {
            public static ModSettings Settings;

            public static CheatSettings cSettings;

            public static void Init(string modDir, string modSettings)
            {
                var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                // read settings
                try
                {
                    Settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
                    Settings.ModDirectory = modDir;
                }
                catch (Exception)
                {
                    Settings = new ModSettings();
                }

                //Cheats.Start(modDir);
            }
        }
    }
}
