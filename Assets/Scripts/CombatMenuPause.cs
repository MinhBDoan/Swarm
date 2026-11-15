using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        // Menu intent is explicit, independent of the cursor released by upgrade cards.
        readonly HashSet<ulong> menuPausers = new HashSet<ulong>();
        int remoteMenuPauseCount;
        public bool LocalMenuOpen { get; private set; }
        public int MenuPauseCount => IsCoopClient ? remoteMenuPauseCount : menuPausers.Count;
        public bool MenuPauseActive => LocalMenuOpen || MenuPauseCount > 0;

        public void ToggleLocalMenu() => SetLocalMenuOpen(!LocalMenuOpen);
        public void ResumeLocalMenu() => SetLocalMenuOpen(false);

        public void SetLocalMenuOpen(bool open)
        {
            if (!Running || Won || PartyDefeated || LocalMenuOpen == open) return;
            LocalMenuOpen = open;
            if (NetworkActive)
            {
                if (IsCoopClient) SendCombatCommand("menu", open ? 1 : 0);
                else ApplyMenuPauseCommand(NetworkManager.Singleton.LocalClientId, open);
            }
            if (open) { ReleaseMouse(); PauseAgents(); }
            else if (!ChoosingUpgrade && Health > 0) CaptureMouse();
        }

        void ApplyMenuPauseCommand(ulong sender, bool open)
        {
            // The command receiver already checks current run and connected party identity.
            if (!Running || !party.ContainsKey(sender)) return;
            if (open) menuPausers.Add(sender);
            else menuPausers.Remove(sender);
            RefreshPartyPause();
            if (PartyPaused) PauseAgents();
            snapshotClock = 0;
        }

        void RefreshPartyPause()
        {
            PartyPaused = menuPausers.Count > 0;
            foreach (var state in party.Values)
                PartyPaused |= state.choosing && state.health > 0;
        }

        void AcceptMenuPauseSnapshot(int count) => remoteMenuPauseCount = Mathf.Max(0,count);

        void ResetMenuPauseState()
        {
            LocalMenuOpen = false;
            remoteMenuPauseCount = 0;
            menuPausers.Clear();
        }
    }
}
