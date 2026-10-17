using System;
using System.Collections;
using UnityEngine;

namespace Polyus.Monetization
{
    // Replace the simulator with an adapter to Unity Ads/LevelPlay.
    // Completed must come from the SDK reward callback, never from ad close alone.
    public interface IRewardedAdsProvider
    {
        bool IsReady(string placement);
        IEnumerator Show(string placement, Action<RewardedAdResult> finished);
        void Cancel();
    }

    public sealed class SimulatedRewardedAdsProvider : IRewardedAdsProvider
    {
        private bool cancelled;
        public bool IsReady(string placement) => true;
        public void Cancel() => cancelled = true;

        public IEnumerator Show(string placement, Action<RewardedAdResult> finished)
        {
            cancelled = false;
            float end = Time.realtimeSinceStartup + 5f;
            while (!cancelled && Time.realtimeSinceStartup < end) yield return null;
            finished(cancelled ? RewardedAdResult.Cancelled : RewardedAdResult.Completed);
        }
    }

    public sealed class AdsManager : MonoBehaviour
    {
        public bool IsShowing { get; private set; }
        public bool IsSimulation => provider is SimulatedRewardedAdsProvider;
        private IRewardedAdsProvider provider = new SimulatedRewardedAdsProvider();

        public void SetProvider(IRewardedAdsProvider adapter)
        {
            if (IsShowing) throw new InvalidOperationException("Cannot replace an active ad provider.");
            provider = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public bool ShowRewarded(string placement, Action reward, Action<RewardedAdResult> finished = null)
        {
            if (IsShowing || string.IsNullOrEmpty(placement) || reward == null || !provider.IsReady(placement)) return false;
            IsShowing = true;
            StartCoroutine(Show(placement, reward, finished));
            return true;
        }

        private IEnumerator Show(string placement, Action reward, Action<RewardedAdResult> finished)
        {
            bool reported = false;
            var outcome = RewardedAdResult.Failed;
            IEnumerator operation = null;
            try { operation = provider.Show(placement, result =>
            {
                // SDKs can deliver duplicate events; each request has one terminal outcome.
                if (reported) return;
                reported = true;
                outcome = result;
            }); }
            catch (Exception error) { Debug.LogException(error); }
            // Catch adapter failures so a broken SDK does not leave gameplay blocked.
            if (operation != null)
            {
                while (true)
                {
                    bool next = false;
                    object current = null;
                    try { next = operation.MoveNext(); if (next) current = operation.Current; }
                    catch (Exception error)
                    {
                        reported = true;
                        outcome = RewardedAdResult.Failed;
                        Debug.LogException(error);
                    }
                    if (!next) break;
                    yield return current;
                }
                try { (operation as IDisposable)?.Dispose(); }
                catch (Exception error) { outcome = RewardedAdResult.Failed; Debug.LogException(error); }
            }
            IsShowing = false;
            try
            {
                var request = new RewardedRequest();
                request.Resolve(reported ? outcome : RewardedAdResult.Failed, reward);
            }
            finally { finished?.Invoke(outcome); }
        }

        public void Cancel() { if (IsShowing) provider.Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
