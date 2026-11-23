using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Single-entry scene bootstrapper uniting RpgWorld500xRuntimeDriver,
    /// RpgWorld500xStatusOverlayHud, and RpgWorld500xSaveSnapshotSerializer into one
    /// zero-leak Play Mode lifecycle with live HUD sync and checksum-verified save/load.
    /// </summary>
    public sealed class RpgWorld500xSceneBootstrapper : IDisposable
    {
        private readonly GameObject _hostObject;
        private readonly bool _ownsHostObject;
        private readonly Dictionary<string, int> _intStore = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly RpgWorld500xRuntimeDriver _driver;
        private readonly RpgWorld500xStatusOverlayHud _statusHud;
        private bool _disposed;

        public RpgWorld500xRuntimeDriver Driver => _driver;
        public RpgWorld500xStatusOverlayHud StatusHud => _statusHud;
        public IReadOnlyDictionary<string, int> Store => _intStore;
        public bool IsDisposed => _disposed || _driver == null || !_driver.IsInitialized;

        public RpgWorld500xSceneBootstrapper(
            GameObject hostObject = null,
            RpgWorldMapId initialMapId = RpgWorldMapId.Soundwell,
            Camera followCamera = null,
            Sprite[] overrideTileSlices = null)
        {
            _ownsHostObject = hostObject == null;
            _hostObject = hostObject ?? new GameObject("World500xSceneBootstrapperRoot");

            _driver = _hostObject.GetComponent<RpgWorld500xRuntimeDriver>();
            if (_driver == null)
            {
                _driver = _hostObject.AddComponent<RpgWorld500xRuntimeDriver>();
            }

            _driver.FollowCamera = followCamera;
            _driver.Initialize(
                overrideMap: initialMapId,
                getInt: k => _intStore.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => _intStore[k] = v,
                overrideTileSlices: overrideTileSlices);

            _statusHud = new RpgWorld500xStatusOverlayHud(_hostObject.transform);
            _statusHud.SyncFromDriver(_driver);
        }

        public RpgWorld500xEffectiveExplorationModifiers StepFrame(
            Vector2 moveInput,
            float deltaSeconds,
            bool sprinting = false,
            bool interactPressed = false,
            bool toggleJournalPressed = false,
            bool toggleOverviewPressed = false)
        {
            if (IsDisposed)
            {
                return default;
            }

            RpgWorld500xEffectiveExplorationModifiers mods = _driver.TickFrame(
                moveInput,
                deltaSeconds,
                sprinting,
                interactPressed,
                toggleJournalPressed,
                toggleOverviewPressed);
            _statusHud.SyncFromDriver(_driver);
            return mods;
        }

        public bool SwitchMap(RpgWorldMapId targetMapId)
        {
            if (IsDisposed)
            {
                return false;
            }

            bool ok = _driver.SwitchMap(targetMapId);
            if (ok)
            {
                _statusHud.SyncFromDriver(_driver);
            }

            return ok;
        }

        public string ExportSaveJson()
        {
            if (IsDisposed)
            {
                return string.Empty;
            }

            RpgWorld500xGrandRuntimeFacade facade = _driver.Facade;
            RpgWorld500xSaveSnapshot snapshot = RpgWorld500xSaveSnapshotSerializer.Capture(
                facade.CurrentMapId,
                facade.Session.Host.PlayerPosition,
                _intStore,
                facade.Atmosphere);
            return RpgWorld500xSaveSnapshotSerializer.Serialize(snapshot);
        }

        public bool TryImportSaveJson(string json, out string error)
        {
            error = string.Empty;
            if (IsDisposed)
            {
                error = "Bootstrapper is disposed.";
                return false;
            }

            if (!RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                json, out RpgWorld500xSaveSnapshot snapshot, out error))
            {
                return false;
            }

            _intStore.Clear();
            RpgWorld500xSaveSnapshotSerializer.ApplyToStore(
                snapshot,
                (k, v) => _intStore[k] = v,
                _driver.Facade.Atmosphere);

            RpgWorldMapId targetMap = (RpgWorldMapId)snapshot.activeMapId;
            if (_driver.Facade.CurrentMapId != targetMap)
            {
                _driver.SwitchMap(targetMap);
            }
            else
            {
                _driver.Facade.PropSpawner.RefreshActiveSprites();
                _driver.Facade.Session.SyncHud();
            }

            _statusHud.SyncFromDriver(_driver);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _statusHud?.Dispose();
            if (_driver != null)
            {
                _driver.Shutdown();
            }

            if (_ownsHostObject && _hostObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_hostObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_hostObject);
                }
            }
        }
    }
}
