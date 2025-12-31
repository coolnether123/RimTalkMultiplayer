using System;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.UI;
using RimWorld;
using Verse;

namespace RimTalk.Multiplayer
{
    /// <summary>
    /// Handles integration with the Multiplayer mod using a HOST-AUTHORITATIVE architecture.
    /// 
    /// HOST: Runs all AI generation, processes inputs, makes API calls
    /// CLIENTS: Only receive and display outputs (dialogue bubbles, responses)
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkMultiplayer
    {
        private static bool multiplayerLoaded;
        private static IAPI multiplayerAPI;

        static RimTalkMultiplayer()
        {
            // Check if Multiplayer mod is loaded
            multiplayerLoaded = ModsConfig.IsActive("rwmt.multiplayer");
            
            if (!multiplayerLoaded)
            {
                Log.Message("[RimTalk] Multiplayer mod not detected. Multiplayer compatibility features will not be enabled.");
                return;
            }

            try
            {
                // Get the Multiplayer API
                multiplayerAPI = GetMultiplayerAPI();
                
                if (multiplayerAPI == null)
                {
                    Log.Warning("[RimTalk] Multiplayer mod is active but API could not be loaded.");
                    return;
                }

                Log.Message("[RimTalk] Multiplayer detected. Initializing host-authoritative multiplayer compatibility...");
                
                // Register all sync methods and fields
                RegisterSyncMethods();
                RegisterSyncFields();
                RegisterSyncWorkers();
                
                Log.Message("[RimTalk] Multiplayer compatibility initialized successfully.");
                Log.Message($"[RimTalk] Running as: {(IsHosting() ? "HOST (AI Generation Enabled)" : "CLIENT (Display Only Mode)")}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Error initializing Multiplayer compatibility: {ex}");
            }
        }

        /// <summary>
        /// Gets the Multiplayer API using reflection
        /// </summary>
        public static IAPI GetMultiplayerAPI()
        {
            try
            {
                // Look for the Multiplayer.Common.MultiplayerAPIBridge type
                var apiType = AccessTools.TypeByName("Multiplayer.Common.MultiplayerAPIBridge");
                if (apiType == null)
                {
                    Log.Warning("[RimTalk] Could not find Multiplayer.Common.MultiplayerAPIBridge type.");
                    return null;
                }

                // Get the Instance field
                var instanceField = AccessTools.Field(apiType, "Instance");
                if (instanceField == null)
                {
                    Log.Warning("[RimTalk] Could not find Instance field in MultiplayerAPIBridge.");
                    return null;
                }

                return (IAPI)instanceField.GetValue(null);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Error getting Multiplayer API: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Registers methods that need to be synchronized across multiplayer clients.
        /// These are OUTPUT methods that display dialogue and update visual state.
        /// </summary>
        private static void RegisterSyncMethods()
        {
            try
            {
                // === PLAYER PAWN MANAGEMENT - Sync all player pawns across instances ===
                multiplayerAPI.RegisterSyncMethod(typeof(MultiplayerPlayerPawns), nameof(MultiplayerPlayerPawns.CreatePlayerPawn));
                multiplayerAPI.RegisterSyncMethod(typeof(MultiplayerPlayerPawns), nameof(MultiplayerPlayerPawns.RemovePlayerPawn));
                multiplayerAPI.RegisterSyncMethod(typeof(MultiplayerPlayerPawns), nameof(MultiplayerPlayerPawns.UpdatePlayerName));
                multiplayerAPI.RegisterSyncMethod(typeof(MultiplayerPlayerPawns), nameof(MultiplayerPlayerPawns.Clear));
                
                // === OUTPUT METHODS - These sync from Host to Clients ===
                
                // When host adds talk responses to pawns, clients need to display them
                multiplayerAPI.RegisterSyncMethod(typeof(PawnState), nameof(PawnState.AddTalkRequest));
                
                // When host ignores/clears talks, clients need to update their display
                multiplayerAPI.RegisterSyncMethod(typeof(PawnState), nameof(PawnState.IgnoreTalkResponse));
                multiplayerAPI.RegisterSyncMethod(typeof(PawnState), nameof(PawnState.IgnoreAllTalkResponses));
                
                // When dialogue is executed (user-initiated), all clients show it
                multiplayerAPI.RegisterSyncMethod(typeof(CustomDialogueService), nameof(CustomDialogueService.ExecuteDialogue));
                
                // When host changes personality settings, clients update their display
                multiplayerAPI.RegisterSyncMethod(typeof(PersonaService), nameof(PersonaService.SetPersonality));
                multiplayerAPI.RegisterSyncMethod(typeof(PersonaService), nameof(PersonaService.SetTalkInitiationWeight));
                
                // === STATE RESET METHODS - Keep all instances in sync ===
                multiplayerAPI.RegisterSyncMethod(typeof(RimTalk), nameof(RimTalk.Reset));
                multiplayerAPI.RegisterSyncMethod(typeof(Cache), nameof(Cache.Clear));
                multiplayerAPI.RegisterSyncMethod(typeof(TalkHistory), nameof(TalkHistory.Clear));
                multiplayerAPI.RegisterSyncMethod(typeof(TalkRequestPool), nameof(TalkRequestPool.Clear));
                multiplayerAPI.RegisterSyncMethod(typeof(ApiHistory), nameof(ApiHistory.Clear));
                multiplayerAPI.RegisterSyncMethod(typeof(Stats), nameof(Stats.Reset));
                
                Log.Message("[RimTalk] Registered sync methods successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Error registering sync methods: {ex}");
            }
        }

        /// <summary>
        /// Registers fields that need to be synchronized from host to clients
        /// </summary>
        private static void RegisterSyncFields()
        {
            try
            {
                // === HOST SETTINGS - Synced to all clients ===
                multiplayerAPI.RegisterSyncField(typeof(RimTalkSettings), nameof(RimTalkSettings.TalkInterval));
                multiplayerAPI.RegisterSyncField(typeof(RimTalkSettings), nameof(RimTalkSettings.DisplayTalkWhenDrafted));
                multiplayerAPI.RegisterSyncField(typeof(RimTalkSettings), nameof(RimTalkSettings.ContinueDialogueWhileSleeping));
                
                // Note: PawnState fields are not synced because they are properties, not fields.
                // PawnState is managed through synced methods (AddTalkRequest, etc.) instead.
                
                Log.Message("[RimTalk] Registered sync fields successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Error registering sync fields: {ex}");
            }
        }

        /// <summary>
        /// Registers custom sync workers for complex types that need special serialization
        /// </summary>
        private static void RegisterSyncWorkers()
        {
            try
            {
                // Complex types will use default serialization for now
                // Can add custom sync workers if needed for optimization
                
                Log.Message("[RimTalk] Registered sync workers successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk] Error registering sync workers: {ex}");
            }
        }

        /// <summary>
        /// Checks if we're currently in a multiplayer session
        /// </summary>
        public static bool IsInMultiplayer()
        {
            if (!multiplayerLoaded || multiplayerAPI == null)
                return false;
                
            return multiplayerAPI.IsInMultiplayer;
        }

        /// <summary>
        /// Checks if the current player is the host
        /// </summary>
        public static bool IsHosting()
        {
            if (!multiplayerLoaded || multiplayerAPI == null)
                return false;
                
            return multiplayerAPI.IsHosting;
        }

        /// <summary>
        /// Checks if we're currently executing a sync command
        /// </summary>
        public static bool IsExecutingSyncCommand()
        {
            if (!multiplayerLoaded || multiplayerAPI == null)
                return false;
                
            return multiplayerAPI.IsExecutingSyncCommand;
        }

        /// <summary>
        /// Gets the current player's faction in multiplayer
        /// </summary>
        public static Faction GetRealPlayerFaction()
        {
            if (!multiplayerLoaded || multiplayerAPI == null)
                return Faction.OfPlayer;
                
            return multiplayerAPI.RealPlayerFaction ?? Faction.OfPlayer;
        }

        // ============================================================================
        // HOST-AUTHORITATIVE HELPERS
        // ============================================================================

        /// <summary>
        /// Checks if AI generation should run on this instance.
        /// CRITICAL: Only the host should run AI generation; clients just receive outputs.
        /// 
        /// Use this before:
        /// - Making API calls to Google AI
        /// - Generating dialogue or talk requests
        /// - Processing pawn state changes that trigger AI
        /// </summary>
        public static bool ShouldRunAIGeneration()
        {
            // If not in multiplayer, always run (single player)
            if (!IsInMultiplayer())
                return true;
                
            // In multiplayer, ONLY HOST runs AI generation
            // Clients do NOT make API calls or generate dialogue
            return IsHosting();
        }

        /// <summary>
        /// Checks if this instance should process game inputs and trigger AI generation.
        /// 
        /// HOST ONLY: Processes pawn state changes, tick events, and triggers dialogue generation
        /// CLIENTS: Skip all input processing
        /// 
        /// Use this in:
        /// - Tick methods that check if pawns should talk
        /// - Event handlers that trigger dialogue
        /// - Any code path that leads to AI generation
        /// </summary>
        public static bool ShouldProcessInputs()
        {
            // If not in multiplayer, always process
            if (!IsInMultiplayer())
                return true;
                
            // In multiplayer, only host processes inputs that lead to AI generation
            return IsHosting();
        }

        /// <summary>
        /// Checks if dialogue display should work (all instances).
        /// 
        /// ALL INSTANCES: Both host and clients display dialogue bubbles
        /// The dialogue data comes from synced methods, so everyone sees it
        /// 
        /// Use this for:
        /// - Displaying talk bubbles
        /// - Showing dialogue UI
        /// - Rendering conversation text
        /// </summary>
        public static bool ShouldDisplayDialogue()
        {
            // All instances (host and clients) should display dialogue
            // The dialogue data comes from synced methods, so everyone sees it
            return true;
        }

        /// <summary>
        /// Checks if this is a client (not host) in multiplayer
        /// </summary>
        public static bool IsClient()
        {
            return IsInMultiplayer() && !IsHosting();
        }
    }
}
