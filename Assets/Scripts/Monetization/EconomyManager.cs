using System;
using UnityEngine;

namespace Polyus.Monetization
{
    public sealed class EconomyManager : MonoBehaviour
    {
        private const string SaveKey = "Polyus.Prototype.Economy.v1";
        public EconomyState State { get; private set; }
        public event Action Changed;

        private void Awake()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) { State = new EconomyState(); return; }
            try
            {
                State = JsonUtility.FromJson<EconomyState>(PlayerPrefs.GetString(SaveKey, "{}"));
                if (State == null || State.gold < 0 || State.crystals < 0 || State.battlePassXp < 0
                    || State.inventory == null || State.rewardIds == null || State.transactionIds == null)
                    throw new InvalidOperationException("Invalid local economy save.");
            }
            catch (Exception error)
            {
                // Keep the original for diagnosis instead of silently discarding it.
                PlayerPrefs.SetString(SaveKey + ".backup", PlayerPrefs.GetString(SaveKey, ""));
                Debug.LogWarning("Economy save could not be loaded; a backup was retained. " + error.Message);
                State = new EconomyState();
            }
        }

        public bool GrantOnce(string id, int gold, int crystals = 0)
        {
            if (!State.GrantOnce(id, gold, crystals)) return false;
            Save();
            return true;
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(State));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
