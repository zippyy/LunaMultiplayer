﻿using LunaClient.Base;
using LunaClient.Events;
using LunaClient.Localization;
using LunaClient.Systems.SettingsSys;
using LunaClient.VesselStore;
using LunaClient.VesselUtilities;
using LunaCommon.Time;
using System;
using System.Collections.Concurrent;
using UniLinq;

namespace LunaClient.Systems.VesselRemoveSys
{
    /// <summary>
    /// This system handles the killing of vessels. We kill the vessels that are not in our subspace and 
    /// the vessels that are destroyed, old copies of changed vessels or when they dock
    /// </summary>
    public class VesselRemoveSystem : MessageSystem<VesselRemoveSystem, VesselRemoveMessageSender, VesselRemoveMessageHandler>
    {
        #region Fields & properties

        private VesselRemoveEvents VesselRemoveEvents { get; } = new VesselRemoveEvents();

        public ConcurrentQueue<Guid> VesselsToRemove { get; private set; } = new ConcurrentQueue<Guid>();
        public ConcurrentDictionary<Guid, DateTime> RemovedVessels { get; } = new ConcurrentDictionary<Guid, DateTime>();

        public Guid ManuallyKillingVesselId = Guid.Empty;

        #endregion

        #region Base overrides

        public override string SystemName { get; } = nameof(VesselRemoveSystem);

        protected override void OnEnabled()
        {
            base.OnEnabled();
            GameEvents.onVesselRecovered.Add(VesselRemoveEvents.OnVesselRecovered);
            GameEvents.onVesselTerminated.Add(VesselRemoveEvents.OnVesselTerminated);
            GameEvents.onVesselWillDestroy.Add(VesselRemoveEvents.OnVesselWillDestroy);
            GameEvents.onLevelWasLoaded.Add(VesselRemoveEvents.LevelLoaded);

            RevertEvent.onRevertToLaunch.Add(VesselRemoveEvents.OnRevertToLaunch);
            RevertEvent.onRevertToPrelaunch.Add(VesselRemoveEvents.OnRevertToEditor);
            RevertEvent.onReturnToEditor.Add(VesselRemoveEvents.OnRevertToEditor);

            SetupRoutine(new RoutineDefinition(1000, RoutineExecution.Update, KillPastSubspaceVessels));
            SetupRoutine(new RoutineDefinition(500, RoutineExecution.Update, RemoveQueuedVessels));
            SetupRoutine(new RoutineDefinition(20000, RoutineExecution.Update, FlushRemovedVessels));
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();
            ClearSystem();
            GameEvents.onVesselRecovered.Remove(VesselRemoveEvents.OnVesselRecovered);
            GameEvents.onVesselTerminated.Remove(VesselRemoveEvents.OnVesselTerminated);
            GameEvents.onVesselWillDestroy.Remove(VesselRemoveEvents.OnVesselWillDestroy);
            GameEvents.onLevelWasLoaded.Remove(VesselRemoveEvents.LevelLoaded);

            RevertEvent.onRevertToLaunch.Remove(VesselRemoveEvents.OnRevertToLaunch);
            RevertEvent.onRevertToPrelaunch.Remove(VesselRemoveEvents.OnRevertToEditor);
            RevertEvent.onReturnToEditor.Remove(VesselRemoveEvents.OnRevertToEditor);
        }

        #endregion

        #region Public

        /// <summary>
        /// Clears the dictionary, you should call this method when switching scene
        /// </summary>
        public void ClearSystem()
        {
            VesselsToRemove = new ConcurrentQueue<Guid>();
            RemovedVessels.Clear();
        }

        /// <summary>
        /// Add a vessel so it will be killed later
        /// </summary>
        public void AddToKillList(Guid vesselId)
        {
            VesselsToRemove.Enqueue(vesselId);
        }

        /// <summary>
        /// Check if vessel is in the kill list
        /// </summary>
        public bool VesselWillBeKilled(Guid vesselId)
        {
            return VesselsToRemove.Contains(vesselId) || RemovedVessels.ContainsKey(vesselId);
        }

        /// <summary>
        /// Unloads a vessel from the game in 1 frame. Caution with this method as it can generate issues!
        /// Specially if you receive a message for a vessel and that vessel is not found as you called this method
        /// </summary>
        private void UnloadVessel(Vessel killVessel)
        {
            if (killVessel == null || !FlightGlobals.Vessels.Contains(killVessel))
            {
                return;
            }
            
            UnloadVesselFromGame(killVessel);
            KillGivenVessel(killVessel);
            UnloadVesselFromScenario(killVessel);
        }

        /// <summary>
        /// Unloads a vessel from the game in 1 frame.
        /// </summary>
        public void UnloadVessel(Guid vesselId)
        {
            UnloadVessel(FlightGlobals.FindVessel(vesselId));
        }

        /// <summary>
        /// Kills and unloads a vessel.
        /// </summary>
        public void KillVessel(Guid vesselId)
        {
            //ALWAYS remove it from the proto store as this dictionary is maintained even if we are in the KSC
            //This means that while in KSC if we receive a vessel remove msg, our FlightGlobals.Vessels will be empty
            //But our VesselsProtoStore probably contains that vessel that must be removed.
            VesselsProtoStore.RemoveVessel(vesselId);

            var killVessel = FlightGlobals.FindVessel(vesselId);
            if (killVessel == null || killVessel.state == Vessel.State.DEAD)
                return;

            LunaLog.Log($"[LMP]: Killing vessel {killVessel.id}");
            SwitchVesselIfSpectating(killVessel);
            UnloadVesselFromGame(killVessel);
            KillGivenVessel(killVessel);
            UnloadVesselFromScenario(killVessel);

            //When vessel.Die() is called, KSP calls RefreshMarkers() so no need to call it ourselves
        }

        /// <summary>
        /// Removes the vessels that are inside the safety bubble
        /// </summary>
        public void RemoveVesselsInSafetyBubble()
        {
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel != null && VesselCommon.IsInSafetyBubble(vessel) && FlightGlobals.ActiveVessel?.id != vessel.id)
                {
                    LunaLog.Log($"[LMP]: Killing vessel {vessel.id} because it's inside safety bubble");

                    UnloadVesselFromGame(vessel);
                    KillGivenVessel(vessel);
                    UnloadVesselFromScenario(vessel);
                }
            }
        }

        #endregion

        #region Update methods

        /// <summary>
        /// Flush vessels older than 20 seconds
        /// </summary>
        private void FlushRemovedVessels()
        {
            var vesselsToFlush = RemovedVessels
                .Where(v => (LunaTime.Now - v.Value) > TimeSpan.FromSeconds(20))
                .Select(v => v.Key);

            foreach (var vesselId in vesselsToFlush)
            {
                RemovedVessels.TryRemove(vesselId, out _);
            }
        }

        /// <summary>
        /// Unload or kills the vessels in the queue
        /// </summary>
        private void RemoveQueuedVessels()
        {
            while(VesselsToRemove.TryDequeue(out var vesselId))
            {
                KillVessel(vesselId);

                //Always add to the killed list even if it exists that vessel or not.
                RemovedVessels.TryAdd(vesselId, LunaTime.Now);
            }
        }

        /// <summary>
        /// Get the vessels that are in a past subspace and kill them
        /// </summary>
        private void KillPastSubspaceVessels()
        {
            if (SettingsSystem.ServerSettings.ShowVesselsInThePast) return;

            if (Enabled)
            {
                var vesselsToUnloadIds = VesselsProtoStore.AllPlayerVessels
                    .Where(v => v.Value.VesselExist && VesselCommon.VesselIsControlledAndInPastSubspace(v.Key));

                foreach (var vesselId in vesselsToUnloadIds)
                {
                    AddToKillList(vesselId.Key);
                }
            }
        }

        #endregion

        #region Private methods

        private static void SwitchVesselIfSpectating(Vessel killVessel)
        {
            if (FlightGlobals.ActiveVessel?.id == killVessel.id)
            {
                //Get a random vessel and switch to it if exists, otherwise go to spacecenter
                var otherVessel = FlightGlobals.Vessels.FirstOrDefault(v => v.id != killVessel.id);
                
                if (otherVessel != null)
                    FlightGlobals.ForceSetActiveVessel(otherVessel);
                else
                    HighLogic.LoadScene(GameScenes.SPACECENTER);

                ScreenMessages.PostScreenMessage(LocalizationContainer.ScreenText.SpectatingRemoved, 10f);
            }
        }

        /// <summary>
        /// Removes the vessel from the scenario. 
        /// If you don't call this, the vessel will still be found in FlightGlobals.Vessels
        /// </summary>
        private static void UnloadVesselFromScenario(Vessel killVessel)
        {
            try
            {
                HighLogic.CurrentGame.DestroyVessel(killVessel);
                HighLogic.CurrentGame.Updated();
            }
            catch (Exception destroyException)
            {
                LunaLog.LogError($"[LMP]: Error destroying vessel from the scenario: {destroyException}");
            }
        }

        /// <summary>
        /// Kills the vessel
        /// </summary>
        private void KillGivenVessel(Vessel killVessel)
        {
            try
            {

                if (killVessel == null) return;

                ManuallyKillingVesselId = killVessel.id;

                //CAUTION!!!!! This method will call our event "VesselRemoveEvents.OnVesselWillDestroy" Check the method to see what can happen!
                killVessel.Die();
            }
            catch (Exception killException)
            {
                LunaLog.LogError($"[LMP]: Error destroying vessel: {killException}");
            }
            finally
            {
                ManuallyKillingVesselId = Guid.Empty;
            }
        }

        /// <summary>
        /// Unload the vessel so the crew is kiled when we remove the vessel.
        /// </summary>
        private static void UnloadVesselFromGame(Vessel killVessel)
        {
            if (killVessel.loaded)
            {
                try
                {
                    killVessel.Unload();
                }
                catch (Exception unloadException)
                {
                    LunaLog.LogError($"[LMP]: Error unloading vessel: {unloadException}");
                }
            }
        }

        #endregion
    }
}
