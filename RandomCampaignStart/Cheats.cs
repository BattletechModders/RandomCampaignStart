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
    class Cheats
    {
        public static CheatSettings cSettings;

        public static void Start(string modDir)
        {
            Logger.Debug($"Cheat Options!");
            Logger.Debug($"modDir: {modDir}");

            try
            {
                string cheatSettingsData = File.ReadAllText($"{modDir}/cheatsettings.json");
                cSettings = JsonConvert.DeserializeObject<CheatSettings>(cheatSettingsData);
                cSettings.ModDirectory = modDir;
            }
            catch (Exception)
            {
                cSettings = new CheatSettings();
            }
        }

        public static void MyMethod(SimGameState simulation)
        {
            if (Cheats.cSettings.cheatStart)
            {
                Logger.Debug($"Welcome You Cheater!");

                if (Cheats.cSettings.cheatArgo)
                {
                    for (int i = 0; i < Cheats.cSettings.cheatArgoUpgrades.Count; i++)
                    {
                        simulation.AddArgoUpgrade(simulation.DataManager.ShipUpgradeDefs.Get(Cheats.cSettings.cheatArgoUpgrades[i]));
                        Logger.Debug($"Upgrade Added: {Cheats.cSettings.cheatArgoUpgrades[i]}");
                    }
                }

                if (Cheats.cSettings.cheatPilots)
                {
                    Logger.Debug($"Number of Pilots: {simulation.PilotRoster.Count}");
                    foreach (var pilot in simulation.PilotRoster)
                    {
                        Logger.Debug($"Pilot: {pilot.Name}");
                        pilot.AddExperience(0, "Cheat XP", Cheats.cSettings.iCheatXP);
                        Logger.Debug($"XP Added: {Cheats.cSettings.iCheatXP}");
                    }
                    simulation.Constants.Story.CommanderStartingExperience += Cheats.cSettings.iCheatXP;
                }

                if (Cheats.cSettings.cheatMoney)
                {
                    simulation.Constants.Story.StartingCBills += Cheats.cSettings.iCheatMoney;
                    Logger.Debug($"CBills Added: {Cheats.cSettings.iCheatMoney}");
                }

                if (Cheats.cSettings.cheatLance)
                {
                    //__instance.Constants.Progression.PilotingSkills.Add()
                    var tempLance = new List<MechDef>();
                    Logger.Debug($"Readying Mechs: {simulation.ReadyingMechs.Count}");
                    for (int k = 0; k < simulation.ReadyingMechs.Count; k++)
                    {
                        Logger.Debug($"Readying Mechs: {simulation.ReadyingMechs[k].Description.Id}");

                        tempLance.Add(simulation.ReadyingMechs[k]);

                    }

                    Logger.Debug($"Active Mechs: {simulation.ActiveMechs.Count}");
                    for (int j = 0; j < simulation.ActiveMechs.Count; j++)
                    {
                        Logger.Debug($"Active Mech: {simulation.ActiveMechs[j].Description.Id}");

                        tempLance.Add(simulation.ActiveMechs[j]);

                    }

                    Logger.Debug($"Store Mechs");
                    for (int l = 0; l < tempLance.Count; l++)
                    {
                        Logger.Debug($"Storing Mech: {tempLance[l]}");

                        simulation.UnreadyMech(l, tempLance[l]);
                    }

                    for (int i = 0; i < Cheats.cSettings.cheatAddMechs.Count; i++)
                    {
                        Logger.Debug($"Add Cheat Mechs");

                        var curMechDef = new MechDef(simulation.DataManager.MechDefs.Get(Cheats.cSettings.cheatAddMechs[i]), simulation.GenerateSimGameUID());

                        simulation.AddMech(i, curMechDef, true, true, false);
                        Logger.Debug($"Adding... {curMechDef.Description.Id}");
                    }
                }

                if (Cheats.cSettings.cheatRep)
                {
                    Random RNG = new Random();
                    
                    if (Cheats.cSettings.cheatLocalRep)
                    {
                        foreach (KeyValuePair<Faction, FactionDef> pair in simulation.FactionsDict)
                        {
                            int randVal = RNG.Next(Cheats.cSettings.iMinRepVal, Cheats.cSettings.iMaxRepVal);
                            AccessTools.Method(typeof(SimGameState), "SetReputation").Invoke(simulation, new object[] { pair.Key, randVal, StatCollection.StatOperation.Set, null });
                        }
                    }

                    if (Cheats.cSettings.cheatAllRep)
                    {
                        foreach (KeyValuePair<Faction, FactionDef> pair in simulation.FactionsDict)
                        {
                            int iVal = Cheats.cSettings.iAllRepVal;
                            AccessTools.Method(typeof(SimGameState), "SetReputation").Invoke(simulation, new object[] { pair.Key, iVal, StatCollection.StatOperation.Int_Add, null });
                        }
                    }
                }
            }


        }
    }

    public class CheatSettings
    {
        public string ModDirectory = string.Empty;

        //Cheat Toggles
        public bool cheatStart = false;
        public bool cheatArgo = false;
        public bool cheatPilots = false;
        public bool cheatMoney = false;
        public bool cheatLance = false;
        public bool cheatRep = false;
        public bool cheatLocalRep = false;
        public bool cheatAllRep = false;

        //Cheat Values
        public List<string> cheatArgoUpgrades = new List<string>();
        public List<string> cheatAddMechs = new List<string>();
        public int iCheatXP = 0;
        public int iCheatMoney = 0;
        public int iMinRepVal = 15;
        public int iMaxRepVal = 50;
        public int iAllRepVal = 20;

    }
}
