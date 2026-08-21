using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public enum ArenaPackageLoadStatus
    {
        Missing,
        Loading,
        Ready,
        Failed,
    }

    public sealed class ArenaResidencyManager : IDisposable
    {
        public const int MaximumResidentPackages = 4;

        private sealed class Entry
        {
            public AsyncOperationHandle<ArenaRecipeAsset> Handle;
        }

        private readonly Dictionary<ArenaPackageKey, Entry> _entries =
            new Dictionary<ArenaPackageKey, Entry>(MaximumResidentPackages);

        public int Count => _entries.Count;
        public string LastFailure { get; private set; }

        public bool Acquire(ArenaPackageKey key)
        {
            if (!key.IsValid)
            {
                LastFailure = "Cannot acquire an invalid arena package key.";
                return false;
            }
            if (_entries.ContainsKey(key)) return true;
            if (_entries.Count >= MaximumResidentPackages)
            {
                LastFailure = "Arena residency cap reached while acquiring " + key + ".";
                return false;
            }

            _entries.Add(key, new Entry
            {
                Handle = Addressables.LoadAssetAsync<ArenaRecipeAsset>(
                    ArenaCatalogRules.PackageAddress(key)),
            });
            return true;
        }

        public bool Reconcile(ArenaResidentSet target)
        {
            var release = new ArenaPackageKey[MaximumResidentPackages];
            var releaseCount = 0;
            foreach (var pair in _entries)
                if (!target.Contains(pair.Key)) release[releaseCount++] = pair.Key;
            for (var index = 0; index < releaseCount; index++) Release(release[index]);

            var success = true;
            for (var index = 0; index < target.Count; index++)
                success &= Acquire(target.Items[index]);
            return success;
        }

        public ArenaPackageLoadStatus Status(ArenaPackageKey key)
        {
            if (!_entries.TryGetValue(key, out var entry)) return ArenaPackageLoadStatus.Missing;
            if (!entry.Handle.IsDone) return ArenaPackageLoadStatus.Loading;
            return entry.Handle.Status == AsyncOperationStatus.Succeeded &&
                   entry.Handle.Result != null && entry.Handle.Result.IsValidFor(key)
                ? ArenaPackageLoadStatus.Ready
                : ArenaPackageLoadStatus.Failed;
        }

        public bool TryGet(ArenaPackageKey key, out ArenaRecipeAsset recipe)
        {
            recipe = null;
            if (!_entries.TryGetValue(key, out var entry) || !entry.Handle.IsDone)
                return false;
            if (entry.Handle.Status != AsyncOperationStatus.Succeeded ||
                entry.Handle.Result == null || !entry.Handle.Result.IsValidFor(key))
            {
                LastFailure = "Arena package failed or was invalid: " + key + ".";
                return false;
            }

            recipe = entry.Handle.Result;
            return true;
        }

        public void Release(ArenaPackageKey key)
        {
            if (!_entries.TryGetValue(key, out var entry)) return;
            _entries.Remove(key);
            if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
        }

        public void ReleaseAll()
        {
            foreach (var pair in _entries)
                if (pair.Value.Handle.IsValid()) Addressables.Release(pair.Value.Handle);
            _entries.Clear();
            LastFailure = null;
        }

        public void Dispose()
        {
            ReleaseAll();
        }
    }
}
