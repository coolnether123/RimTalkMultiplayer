using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimTalk.Data;
using RimWorld;
using Verse;

namespace RimTalk.Multiplayer
{
    /// <summary>
    /// Manages player pawns in multiplayer sessions.
    /// Each multiplayer player gets their own invisible "player pawn" that exists on all instances.
    /// This allows any player to initiate dialogue through their player character.
    /// </summary>
    public static class MultiplayerPlayerPawns
    {
        // Maps multiplayer player ID to their player pawn
        private static readonly Dictionary<int, Pawn> PlayerPawnsByPlayerId = new();
        
        // Maps multiplayer player ID to their player name
        private static readonly Dictionary<int, string> PlayerNamesByPlayerId = new();
        
        // The local player's ID (set when joining multiplayer)
        private static int localPlayerId = -1;

        /// <summary>
        /// Initializes the player pawn system for multiplayer.
        /// Called when a multiplayer game starts or when a player joins.
        /// </summary>
        public static void Initialize()
        {
            if (!RimTalkMultiplayer.IsInMultiplayer())
                return;

            // Get the current player's info from Multiplayer API
            var players = RimTalkMultiplayer.GetMultiplayerAPI()?.GetPlayers();
            if (players == null)
                return;

            // Sync all player pawns
            foreach (var player in players)
            {
                if (!PlayerPawnsByPlayerId.ContainsKey(player.Id))
                {
                    CreatePlayerPawn(player.Id, player.Username);
                }
            }
        }

        /// <summary>
        /// Creates a player pawn for a specific multiplayer player.
        /// This method is synced so it runs on all instances.
        /// </summary>
        [SyncMethod]
        public static void CreatePlayerPawn(int playerId, string playerName)
        {
            // Don't create duplicate player pawns
            if (PlayerPawnsByPlayerId.ContainsKey(playerId))
            {
                Log.Warning($"[RimTalk] Attempted to create duplicate player pawn for player {playerId} ({playerName})");
                return;
            }

            // Generate the invisible player pawn
            Pawn playerPawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist);
            playerPawn.Name = new NameSingle(playerName);
            
            // Store the player pawn
            PlayerPawnsByPlayerId[playerId] = playerPawn;
            PlayerNamesByPlayerId[playerId] = playerName;
            
            // Add to the main cache
            Cache.Get(playerPawn);
            
            Log.Message($"[RimTalk] Created player pawn for multiplayer player {playerId}: {playerName}");
        }

        /// <summary>
        /// Removes a player pawn when a player leaves.
        /// This method is synced so it runs on all instances.
        /// </summary>
        [SyncMethod]
        public static void RemovePlayerPawn(int playerId)
        {
            if (!PlayerPawnsByPlayerId.TryGetValue(playerId, out Pawn playerPawn))
                return;

            // Remove from cache
            PlayerPawnsByPlayerId.Remove(playerId);
            PlayerNamesByPlayerId.Remove(playerId);
            
            Log.Message($"[RimTalk] Removed player pawn for multiplayer player {playerId}");
        }

        /// <summary>
        /// Updates a player's name (if they change it).
        /// This method is synced so it runs on all instances.
        /// </summary>
        [SyncMethod]
        public static void UpdatePlayerName(int playerId, string newName)
        {
            if (!PlayerPawnsByPlayerId.TryGetValue(playerId, out Pawn playerPawn))
                return;

            playerPawn.Name = new NameSingle(newName);
            PlayerNamesByPlayerId[playerId] = newName;
            
            Log.Message($"[RimTalk] Updated player name for player {playerId}: {newName}");
        }

        /// <summary>
        /// Gets the player pawn for a specific multiplayer player.
        /// </summary>
        public static Pawn GetPlayerPawn(int playerId)
        {
            return PlayerPawnsByPlayerId.TryGetValue(playerId, out Pawn pawn) ? pawn : null;
        }

        /// <summary>
        /// Gets the local player's pawn (the player pawn for THIS game instance).
        /// </summary>
        public static Pawn GetLocalPlayerPawn()
        {
            // In single player, use the standard player pawn
            if (!RimTalkMultiplayer.IsInMultiplayer())
                return Cache.GetPlayer();

            // In multiplayer, get the local player's synced pawn
            if (localPlayerId == -1)
            {
                // Try to determine local player ID from Multiplayer API
                var api = RimTalkMultiplayer.GetMultiplayerAPI();
                if (api != null)
                {
                    // Get the local player's username from the API
                    string localUsername = api.PlayerName;
                    
                    // Find the player ID that matches this username
                    foreach (var kvp in PlayerNamesByPlayerId)
                    {
                        if (kvp.Value == localUsername)
                        {
                            localPlayerId = kvp.Key;
                            break;
                        }
                    }
                }
            }

            return localPlayerId != -1 ? GetPlayerPawn(localPlayerId) : null;
        }

        /// <summary>
        /// Gets all player pawns (for all multiplayer players).
        /// </summary>
        public static IEnumerable<Pawn> GetAllPlayerPawns()
        {
            return PlayerPawnsByPlayerId.Values;
        }

        /// <summary>
        /// Checks if a pawn is a player pawn (any multiplayer player's pawn).
        /// </summary>
        public static bool IsPlayerPawn(Pawn pawn)
        {
            if (pawn == null)
                return false;

            // In single player, check against the standard player pawn
            if (!RimTalkMultiplayer.IsInMultiplayer())
                return pawn == Cache.GetPlayer();

            // In multiplayer, check if it's any player's pawn
            return PlayerPawnsByPlayerId.Values.Contains(pawn);
        }

        /// <summary>
        /// Gets the player ID for a given player pawn.
        /// Returns -1 if the pawn is not a player pawn.
        /// </summary>
        public static int GetPlayerIdForPawn(Pawn pawn)
        {
            if (pawn == null)
                return -1;

            foreach (var kvp in PlayerPawnsByPlayerId)
            {
                if (kvp.Value == pawn)
                    return kvp.Key;
            }

            return -1;
        }

        /// <summary>
        /// Clears all player pawns (called when leaving multiplayer or resetting).
        /// </summary>
        [SyncMethod]
        public static void Clear()
        {
            PlayerPawnsByPlayerId.Clear();
            PlayerNamesByPlayerId.Clear();
            localPlayerId = -1;
            
            Log.Message("[RimTalk] Cleared all multiplayer player pawns");
        }

        /// <summary>
        /// Gets a debug string showing all player pawns.
        /// </summary>
        public static string GetDebugInfo()
        {
            if (!RimTalkMultiplayer.IsInMultiplayer())
                return "Not in multiplayer";

            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Local Player ID: {localPlayerId}");
            lines.AppendLine("Player Pawns:");
            
            foreach (var kvp in PlayerPawnsByPlayerId)
            {
                string isLocal = kvp.Key == localPlayerId ? " (LOCAL)" : "";
                lines.AppendLine($"  Player {kvp.Key}: {kvp.Value.LabelShort}{isLocal}");
            }
            
            return lines.ToString();
        }
    }
}
