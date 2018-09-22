using System;
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

                        if (kvp.Value.Tonnage > RngStart.Settings.MaximumMechWeight || kvp.Value.Tonnage < RngStart.Settings.MinimumMechWeight)
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
                    if (!RngStart.Settings.FullRandomMode)
                    {
                        
                        // remove ancestral mech if specified
                        if (RemoveAncestralMech && firstrun)
                        {
                            //Logger.Debug($"Lance Size(Legacy1): {lance.Count}");
                            __instance.ActiveMechs.Remove(0);
                            //Logger.Debug($"Lance Size(Legacy2): {lance.Count}");
                        }
                        
                        while (currentLanceWeight < RngStart.Settings.MinimumStartingWeight || currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
                        {
                            if (RemoveAncestralMech == true)
                            {
                                baySlot = 0;
                            }
                            else
                            {
                                currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                                baySlot = 1;
                            }

                            if (!firstrun)
                            {
                                for (var i = baySlot; i < 6; i++)
                                {
                                    __instance.ActiveMechs.Remove(i);
                                    currentLanceWeight = 0;
                                }
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
                            if (__instance.Constants.Story.StartingTargetSystem == "UrCruinne")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.AssaultMechsPossible, RngStart.Settings.NumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.HeavyMechsPossible, RngStart.Settings.NumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.MediumMechsPossible, RngStart.Settings.NumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.LightMechsPossible, RngStart.Settings.NumberLightMechs));

                                /*for(int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Galatea")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Tharkad")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                        if (__instance.Constants.Story.StartingTargetSystem == "NewAvalon")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Luthien")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Atreus(FWL)")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Sian")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Rasalhague")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "StranaMechty")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanAssaultMechsPossible, RngStart.Settings.clanNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanHeavyMechsPossible, RngStart.Settings.clanNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanMediumMechsPossible, RngStart.Settings.clanNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.clanLightMechsPossible, RngStart.Settings.clanNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "St.Ives")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerAssaultMechsPossible, RngStart.Settings.innerNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerHeavyMechsPossible, RngStart.Settings.innerNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerMediumMechsPossible, RngStart.Settings.innerNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.innerLightMechsPossible, RngStart.Settings.innerNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Oberon")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Taurus")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Canopus")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Alpheratz")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Circinus")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Alphard(MH)")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Lothario")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Coromodir")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periAssaultMechsPossible, RngStart.Settings.periNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periHeavyMechsPossible, RngStart.Settings.periNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periMediumMechsPossible, RngStart.Settings.periNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.periLightMechsPossible, RngStart.Settings.periNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Asturias")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "FarReach")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Blackbone(Nyserta 3025+)")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Bremen(HL)")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Trondheim(JF)")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "TortugaPrime")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Gotterdammerung")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateAssaultMechsPossible, RngStart.Settings.pirateNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateHeavyMechsPossible, RngStart.Settings.pirateNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateMediumMechsPossible, RngStart.Settings.pirateNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.pirateLightMechsPossible, RngStart.Settings.pirateNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Thala")
                            {
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepAssaultMechsPossible, RngStart.Settings.deepNumberAssaultMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepHeavyMechsPossible, RngStart.Settings.deepNumberHeavyMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepMediumMechsPossible, RngStart.Settings.deepNumberMediumMechs));
                                legacyLance.AddRange(GetRandomSubList(RngStart.Settings.deepLightMechsPossible, RngStart.Settings.deepNumberLightMechs));

                                /*for (int i = 0; i < legacyLance.Count; i++)
                                {
                                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                                    foreach (var mechID in legacyLance)
                                    {
                                        if (mechID == mechDef.Description.Id)
                                        {
                                            lance.Add(mechDef);

                                            Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                            Logger.Debug($"Mech Weight: {mechDef.Chassis.Tonnage}");
                                        }
                                    }
                                }
                                Logger.Debug($"LegacyLance Weight: {currentLanceWeight}");*/
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

                            //Logger.Debug($"TestMech {mechDef.Name}");
                            if (__instance.Constants.Story.StartingTargetSystem == "UrCruinne")
                            {
                                foreach (var mechID in RngStart.Settings.vanExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        Logger.Debug($"Start Planet-Gal {__instance.Constants.Story.StartingTargetSystem}");
                                        Logger.Debug($"Gal-Excluded! {mechDef.Description.Id}");
                                    }
                                }
                                Logger.Debug($"Leaving Gal-Start Loop!");
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Galatea")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        Logger.Debug($"Start Planet-Ur {__instance.Constants.Story.StartingTargetSystem}");
                                        Logger.Debug($"Ur-Excluded! {mechDef.Description.Id}");
                                    }
                                }
                                Logger.Debug($"Leaving Ur-Start Loop!");
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Tharkad")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "NewAvalon")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Luthien")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Atreus(FWL)")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Sian")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Rasalhague")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "StranaMechty")
                            {
                                foreach (var mechID in RngStart.Settings.clanExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "St.Ives")
                            {
                                foreach (var mechID in RngStart.Settings.innerExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Oberon")
                            {
                                foreach (var mechID in RngStart.Settings.pirateExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Taurus")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Canopus")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Alpheratz")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Circinus")
                            {
                                foreach (var mechID in RngStart.Settings.pirateExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Alphard(MH)")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Lothario")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Coromodir")
                            {
                                foreach (var mechID in RngStart.Settings.periExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Asturias")
                            {
                                foreach (var mechID in RngStart.Settings.deepExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "FarReach")
                            {
                                foreach (var mechID in RngStart.Settings.deepExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Blackbone(Nyserta 3025+)")
                            {
                                foreach (var mechID in RngStart.Settings.pirateExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Bremen(HL)")
                            {
                                foreach (var mechID in RngStart.Settings.deepExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Trondheim(JF)")
                            {
                                foreach (var mechID in RngStart.Settings.deepExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "TortugaPrime")
                            {
                                foreach (var mechID in RngStart.Settings.pirateExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Gotterdammerung")
                            {
                                foreach (var mechID in RngStart.Settings.pirateExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            if (__instance.Constants.Story.StartingTargetSystem == "Thala")
                            {
                                foreach (var mechID in RngStart.Settings.deepExcludedMechs)
                                {
                                    if (mechID == mechDef.Description.Id)
                                    {
                                        excluded = true;

                                        //Logger.Debug($"Excluded! {mechDef.Name}");
                                    }
                                }
                            }
                            /*foreach (var mechID in RngStart.Settings.ExcludedMechs)
                            {
                                if (mechID == mechDef.Description.Id)
                                {
                                    excluded = true;

                                    //Logger.Debug($"Excluded! {mechDef.Name}");
                                }
                            }*/


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

                            /*if (currentLanceWeight > RngStart.Settings.MaximumStartingWeight || firstTargetRun)
                            {
                                //Logger.Debug($"Clearing invalid lance");
                                Logger.Debug($"Lance Count-1: {lance.Count}");
                                lance.Remove(mechDef);
                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                dupe = false;
                                blacklisted = false;
                                excluded = false;
                                firstTargetRun = false;
                                if (RemoveAncestralMech == true)
                                {
                                    baySlot = 0;
                                    currentLanceWeight = 0;
                                    maxLanceSize = RngStart.Settings.MaximumLanceSize;
                                    if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY" && RngStart.Settings.IgnoreAncestralMech == true)
                                    {
                                        maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
                                        TargetDummy = true;
                                    }
                                }
                                else if (!RemoveAncestralMech && RngStart.Settings.IgnoreAncestralMech)
                                {
                                    maxLanceSize = RngStart.Settings.MaximumLanceSize + 1;
                                    currentLanceWeight = 0;
                                    lance.Add(AncestralMechDef);
                                    baySlot = 1;
                                }
                                else
                                {
                                    maxLanceSize = RngStart.Settings.MaximumLanceSize;
                                    currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                                    lance.Add(AncestralMechDef);
                                    baySlot = 1;
                                }
                                continue;
                            }*/

                            //Logger.Debug($"Done a loop");
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
                //Find Starting Mech and if Starting Mech is used
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

                        if (kvp.Value.Tonnage > RngStart.Settings.MaximumMechWeight || kvp.Value.Tonnage < RngStart.Settings.MinimumMechWeight)
                        {
                            continue;
                        }
                    }
                    // passed checks, add to Dictionary
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }
                
                Logger.Debug($"TagRandom Mode");

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
                /*
                // build lance collection from dictionary for speed
                var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                var mechString = randomMech.Key.Replace("chassisdef", "mechdef");
                // getting chassisdefs so renaming the key to match mechdefs Id
                //var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                */
                /*for (int j = 0; j < mechTonnages.Count; j++)
                {
                    var currentMech = mechTonnages.ElementAt(j);
                    var currentMechString = currentMech.Key.Replace("chassisdef", "mechdef");
                    var currentMechDef = new MechDef(__instance.DataManager.MechDefs.Get(currentMechString), __instance.GenerateSimGameUID());

                    Logger.Debug($"Mech ID: {currentMechDef.Description.Id}");
                    for (int k = 0; k < currentMechDef.MechTags.Count; k++)
                    {
                        Logger.Debug($"Tag-{k}: {currentMechDef.MechTags[k]}");
                    }

                }*/

                bool bNoTag = false;
                bool bDupe = false;
                bool bExcluded = false;
                bool bBlacklisted = false;
                while (minLanceSize > lance.Count || currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                {
                    // build lance collection from dictionary for speed
                    var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                    var mechString = randomMech.Key.Replace("chassisdef", "mechdef");
                    // getting chassisdefs so renaming the key to match mechdefs Id
                    //var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());

                    foreach (var mechID in RngStart.Settings.ExcludedMechs)
                    {
                        if (mechID == mechDef.Description.Id)
                        {
                            bExcluded = true;

                            Logger.Debug($"Excluded! {mechDef.Description.Id}");
                        }
                    }

                    if (mechDef.MechTags.Contains("BLACKLISTED"))
                    {
                        bBlacklisted = true;

                        Logger.Debug($"Blacklisted! {mechDef.Name}");
                    }

                    if (!RngStart.Settings.AllowDuplicateChassis)
                    {
                        foreach (var mech in lance)
                        {
                            if (mech.Name == mechDef.Name)
                            {
                                bDupe = true;

                                Logger.Debug($"SAME SAME! {mech.Name}\t\t{mechDef.Name}");
                            }
                        }
                    }

                    if (!bBlacklisted && !bDupe && !bExcluded)
                    {
                        Logger.Debug($"Starting Planet: {__instance.Constants.Story.StartingTargetSystem}");
                        if (__instance.Constants.Story.StartingTargetSystem == "StranaMechty")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.clanAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Clan Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if(!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        //Check for Vanilla Tags
                        if (__instance.Constants.Story.StartingTargetSystem == "UrCruinne")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.vanAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Vanilla Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Galatea")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.defAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Default Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Tharkad")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.steinerAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Steiner Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "NewAvalon")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.davionAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Davion Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Luthien")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.kuritaAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Kurita Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Atreus(FWL)")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.marikAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Marik Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Sian")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.liaoAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Liao Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Rasalhague")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.rasAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Rasalhague Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "St.Ives")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.ivesAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"St. Ives Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Oberon")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.oberonAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Oberon Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Taurus")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.taurianAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Taurian Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Canopus")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.magistracyAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Magistracy Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Alpheratz")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.outworldAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Outworld Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Circinus")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.circinusAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Circinus Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Alphard(MH)")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.marianAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Marian Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Lothario")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.lothianAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Lothian Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Coromodir")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.auriganAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Aurigan Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Asturias")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.castilleAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Castille Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "FarReach")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.chainAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Chainelane Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Blackbone(Nyserta 3025+)")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.elysiaAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Elysian Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Bremen(HL)")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.hanseAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Hanse Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Trondheim(JF)")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.jarnfolkAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Vikings Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "TortugaPrime")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.tortugaAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Tortugian Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Gotterdammerung")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.valkyrateAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Valyrate Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }
                        }

                        if (__instance.Constants.Story.StartingTargetSystem == "Thala")
                        {
                            //Check for clan tags
                            for (int iTag = 0; iTag < mechDef.MechTags.Count; iTag++)
                            {
                                foreach (var mechTag in RngStart.Settings.axumiteAllowedTags)
                                {
                                    if (mechTag == mechDef.MechTags[iTag])
                                    {
                                        Logger.Debug($"INCLUDED!");
                                        Logger.Debug($"Included Tag: {mechDef.MechTags[iTag]}");
                                        Logger.Debug($"Included Mech:{mechDef.Description.Id}");
                                        bNoTag = false;
                                        goto endTagCheck;
                                    }
                                    else
                                    {
                                        bNoTag = true;
                                    }
                                }
                                Logger.Debug($" ");
                                Logger.Debug($"Axumite Start!");
                                Logger.Debug($"Invalid Mech!");
                                Logger.Debug($"Mech ID: {mechDef.Description.Id}");
                                Logger.Debug($" ");
                            }

                            endTagCheck:
                            if (!bNoTag)
                            {
                                Logger.Debug($"Lance Count-1: {lance.Count}");

                                lance.Add(mechDef);
                                currentLanceWeight += mechDef.Chassis.Tonnage;

                                Logger.Debug($"Lance Count-2: {lance.Count}");
                                Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            }
                            else
                            {
                                bBlacklisted = false;
                                bDupe = false;
                                bExcluded = false;
                                bNoTag = false;
                            }


                        }
                    }
                    else
                    {
                        bBlacklisted = false;
                        bDupe = false;
                        bExcluded = false;
                        bNoTag = false;
                    }

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
                            lance.Remove(lightest);
                            currentLanceWeight -= lightest.Chassis.Tonnage;
                        }
                    }

                    if (lance.Count < minLanceSize && currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
                    {
                        Logger.Debug($"Weight: {currentLanceWeight}");
                        Logger.Debug($"Lance Count-1: {lance.Count}");

                        var heaviest = lance[0];
                        for (int i = 0; i < lance.Count; i++)
                        {
                            if (heaviest.Chassis.Tonnage < lance[i].Chassis.Tonnage)
                            {
                                Logger.Debug($"Mech: {lance[i].Name}");
                                Logger.Debug($"Weight: {lance[i].Chassis.Tonnage}");
                                heaviest = lance[i];
                            }
                            lance.Remove(heaviest);
                            currentLanceWeight -= heaviest.Chassis.Tonnage;
                        }
                    }
                }

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

        internal class ModSettings
        {
            /*   TAG RANDOM SETTINGS    */
            //Excluded Mechs for all starts
            public List<string> ExcludedMechs = new List<string>();

            //Vanilla Start Variables
            public List<string> vanAllowedTags = new List<string>();

            //Default Faction Variables
            public List<string> defAllowedTags = new List<string>();

            //House Steiner Variables
            public List<string> steinerAllowedTags = new List<string>();

            //House Davion Variables
            public List<string> davionAllowedTags = new List<string>();

            //House Kurita Variables
            public List<string> kuritaAllowedTags = new List<string>();

            //House Marik Variables
            public List<string> marikAllowedTags = new List<string>();

            //House Liao Variables
            public List<string> liaoAllowedTags = new List<string>();

            //Rasalhague Variables
            public List<string> rasAllowedTags = new List<string>();
            
            //Clan Start Variables
            public List<string> clanAllowedTags = new List<string>();

            //St. Ives Variables
            public List<string> ivesAllowedTags = new List<string>();

            //Oberon Variables
            public List<string> oberonAllowedTags = new List<string>();

            //Taurian Variables
            public List<string> taurianAllowedTags = new List<string>();

            //Magistracy Variables
            public List<string> magistracyAllowedTags = new List<string>();

            //Outworld Variables
            public List<string> outworldAllowedTags = new List<string>();

            //Circinus Variables
            public List<string> circinusAllowedTags = new List<string>();

            //House Marian Variables
            public List<string> marianAllowedTags = new List<string>();

            //House Lothian Variables
            public List<string> lothianAllowedTags = new List<string>();

            //Aurigan Restoration Variables
            public List<string> auriganAllowedTags = new List<string>();

            //House Castille Variables
            public List<string> castilleAllowedTags = new List<string>();

            //Chainelane Variables
            public List<string> chainAllowedTags = new List<string>();

            //Elysia Variables
            public List<string> elysiaAllowedTags = new List<string>();

            //Hanse Variables
            public List<string> hanseAllowedTags = new List<string>();

            //Vikings Variables
            public List<string> jarnfolkAllowedTags = new List<string>();

            //Tortuga Variables
            public List<string> tortugaAllowedTags = new List<string>();

            //Valkyrate Variables
            public List<string> valkyrateAllowedTags = new List<string>();

            //Axumite Variables
            public List<string> axumiteAllowedTags = new List<string>();


            /*   FULL RANDOM SETTINGS   */
            //Vanilla Start Variables
            public List<string> vanExcludedMechs = new List<string>();

            //Pirate Faction Variables
            public List<string> pirateExcludedMechs = new List<string>();

            //Inner Sphere Variables
            public List<string> innerExcludedMechs = new List<string>();

            //Periphery Start Variables
            public List<string> periExcludedMechs = new List<string>();

            //Deep Periphery Start Variables
            public List<string> deepExcludedMechs = new List<string>();

            //Clan Start Variables
            public List<string> clanExcludedMechs = new List<string>();


            /*     LEGACY SETTINGS      */
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
            public bool FullRandomMode = true;
            public bool TagRandomLance = true;

        }

        public static class RngStart
        {
            internal static ModSettings Settings;

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
            }
        }
    }
}