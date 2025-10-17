using DisplayProfileManager.Helpers;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DisplayProfileManager.Core
{
    public class ProfileManager
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();

        public class ProfileApplyResult
        {
            public bool Success { get; set; }
            public bool PrimaryChanged { get; set; }
            public bool DisplayConfigApplied { get; set; }
            public bool ResolutionChanged { get; set; }
            public bool DpiChanged { get; set; }
            public bool AudioSuccess { get; set; }
        }

        private static ProfileManager _instance;
        private static readonly object _lock = new object();

        private List<Profile> _profiles;
        private readonly string _profilesFilePath;
        private readonly string _profilesFolderPath;
        private readonly string _appDataFolder;
        private string _currentProfileId;

        public static ProfileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ProfileManager();
                    }
                }
                return _instance;
            }
        }

        public event EventHandler<Profile> ProfileAdded;
        public event EventHandler<Profile> ProfileUpdated;
        public event EventHandler<string> ProfileDeleted;
        public event EventHandler<Profile> ProfileApplied;
        public event EventHandler ProfilesLoaded;

        public string CurrentProfileId => _currentProfileId;

        private ProfileManager()
        {
            _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayProfileManager");
            _profilesFilePath = Path.Combine(_appDataFolder, "profiles.json");
            _profilesFolderPath = Path.Combine(_appDataFolder, "Profiles");
            _profiles = new List<Profile>();
            _currentProfileId = null;

            EnsureAppDataFolderExists();
            EnsureProfilesFolderExists();
        }

        private void EnsureAppDataFolderExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        private void EnsureProfilesFolderExists()
        {
            if (!Directory.Exists(_profilesFolderPath))
            {
                Directory.CreateDirectory(_profilesFolderPath);
            }
        }

        public async Task<bool> LoadProfilesAsync()
        {
            try
            {
                _profiles.Clear();
                
                var profileFiles = Directory.GetFiles(_profilesFolderPath, "*.dpm");
                
                foreach (var file in profileFiles)
                {
                    try
                    {
                        var json = await Task.Run(() => File.ReadAllText(file));
                        var profile = JsonConvert.DeserializeObject<Profile>(json);
                        if (profile != null)
                        {
                            _profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error loading profile from {file}");
                    }
                }

                if (_profiles.Count == 0)
                {
                    await CreateDefaultProfileAsync();
                }

                // Load current profile ID from settings
                _currentProfileId = _settingsManager.GetCurrentProfileId();

                ProfilesLoaded?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading profiles");
                _profiles = new List<Profile>();
                return false;
            }
        }

        public async Task<bool> SaveProfileAsync(Profile profile)
        {
            try
            {
                var filePath = GetProfileFilePath(profile.Id);
                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(filePath, json));
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error saving profile");
                return false;
            }
        }

        private string GetProfileFilePath(string profileId)
        {
            return Path.Combine(_profilesFolderPath, $"{profileId}.dpm");
        }

        public async Task<Profile> CreateDefaultProfileAsync()
        {
            var defaultProfile = new Profile("Default", "Default system profile created automatically");
            defaultProfile.IsDefault = true;

            try
            {
                var currentSettings = await GetCurrentDisplaySettingsAsync();
                defaultProfile.DisplaySettings.AddRange(currentSettings);

                AddProfile(defaultProfile);
                _currentProfileId = defaultProfile.Id;
                await SaveProfileAsync(defaultProfile);
                await _settingsManager.SetCurrentProfileIdAsync(defaultProfile.Id);
                return defaultProfile;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating default profile");
                AddProfile(defaultProfile);
                return defaultProfile;
            }
        }

        public async Task<List<DisplaySetting>> GetCurrentDisplaySettingsAsync()
        {
            return await Task.Run(() =>
            {
                var settings = new List<DisplaySetting>();
                try
                {
                    logger.Debug("Getting current display settings using unified DisplayConfigHelper...");

                    List<DisplayConfigHelper.DisplayConfigInfo> displayConfigs = DisplayConfigHelper.GetDisplayConfigs();
                    var activeConfigs = displayConfigs.Where(d => d.IsEnabled).ToList();

                    foreach (var config in activeConfigs)
                    {
                        DpiHelper.DPIScalingInfo dpiInfo = DpiHelper.GetDPIScalingInfo(config.DeviceName);

                        DisplaySetting setting = new DisplaySetting
                        {
                            DeviceName = config.DeviceName,
                            ReadableDeviceName = config.FriendlyName,
                            Width = config.Width,
                            Height = config.Height,
                            Frequency = (int)config.RefreshRate,
                            DpiScaling = dpiInfo.Current,
                            IsPrimary = config.IsPrimary,
                            AdapterId = $"{config.AdapterId.HighPart:X8}{config.AdapterId.LowPart:X8}",
                            SourceId = config.SourceId,
                            IsEnabled = config.IsEnabled,
                            PathIndex = config.PathIndex,
                            TargetId = config.TargetId,
                            DisplayPositionX = config.DisplayPositionX,
                            DisplayPositionY = config.DisplayPositionY,
                            IsHdrSupported = config.IsHdrSupported,
                            IsHdrEnabled = config.IsHdrEnabled,
                            Rotation = (int)config.Rotation,

                            // NUEVO: Guardar identificadores de conexión física
                            PhysicalConnectionId = config.PhysicalConnectionId,
                            ConnectionType = config.ConnectionType,
                            ConnectorInstance = config.ConnectorInstance,

                            // Identificadores EDID
                            ManufacturerName = config.ManufacturerName,
                            ProductCodeID = config.ProductCodeID,
                            SerialNumberID = config.SerialNumberID
                        };

                        logger.Debug($"PROFILE DEBUG: Creating DisplaySetting for {setting.DeviceName}:");
                        logger.Debug($"PROFILE DEBUG:   HDR Supported: {setting.IsHdrSupported}");
                        logger.Debug($"PROFILE DEBUG:   HDR Enabled: {setting.IsHdrEnabled}");
                        logger.Debug($"PROFILE DEBUG:   PhysicalConnectionId: {setting.PhysicalConnectionId}");
                        logger.Debug($"PROFILE DEBUG:   ConnectionType: {setting.ConnectionType}");

                        // Capture available options for this monitor
                        try
                        {
                            // Get available resolutions
                            setting.AvailableResolutions = DisplayHelper.GetSupportedResolutionsOnly(setting.DeviceName);

                            // Get available DPI scaling
                            var dpiValues = DpiHelper.GetSupportedDPIScalingOnly(setting.DeviceName);
                            setting.AvailableDpiScaling = dpiValues.ToList();

                            // Get available refresh rates for each resolution
                            setting.AvailableRefreshRates = new Dictionary<string, List<int>>();
                            foreach (var resolution in setting.AvailableResolutions)
                            {
                                var parts = resolution.Split('x');
                                if (parts.Length == 2 &&
                                    int.TryParse(parts[0], out int width) &&
                                    int.TryParse(parts[1], out int height))
                                {
                                    var refreshRates = DisplayHelper.GetAvailableRefreshRates(setting.DeviceName, width, height);
                                    if (refreshRates.Count > 0)
                                    {
                                        setting.AvailableRefreshRates[resolution] = refreshRates;
                                    }
                                }
                            }

                            logger.Debug($"Captured available options for {setting.DeviceName}: " +
                                $"{setting.AvailableResolutions.Count} resolutions, " +
                                $"{setting.AvailableDpiScaling.Count} DPI values, " +
                                $"{setting.AvailableRefreshRates.Count} resolution-refresh rate mappings");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error capturing available options for {setting.DeviceName}");
                        }

                        settings.Add(setting);
                    }

                    logger.Info($"Found {settings.Count} display settings");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error getting current display settings");
                }

                return settings;
            });
        }

        /// <summary>
        /// Encuentra el mejor monitor del sistema que coincide con un monitor del perfil usando una estrategia jerárquica
        /// </summary>
        private DisplayConfigHelper.DisplayConfigInfo FindBestMatchForProfile(
            DisplaySetting profileSetting,
            List<DisplayConfigHelper.DisplayConfigInfo> systemDisplays)
        {
            logger.Debug($"Finding match for profile monitor: {profileSetting.ReadableDeviceName} (ConnectionId: {profileSetting.PhysicalConnectionId})");

            // Nivel 1: Coincidencia de conexión física exacta.
            if (!string.IsNullOrEmpty(profileSetting.PhysicalConnectionId))
            {
                var exactMatch = systemDisplays.FirstOrDefault(d => d.PhysicalConnectionId == profileSetting.PhysicalConnectionId);
                if (exactMatch != null)
                {
                    logger.Info($"✓ Nivel 1 - Coincidencia de conexión exacta: {exactMatch.FriendlyName} via {exactMatch.ConnectionType}");
                    return exactMatch;
                }
            }

            // Nivel 2: Coincidencia por EDID (mismo monitor físico, posiblemente otra conexión).
            var edidMatches = systemDisplays.Where(d =>
                !string.IsNullOrEmpty(profileSetting.ManufacturerName) &&
                d.ManufacturerName == profileSetting.ManufacturerName &&
                d.ProductCodeID == profileSetting.ProductCodeID &&
                (string.IsNullOrEmpty(profileSetting.SerialNumberID) || profileSetting.SerialNumberID == "0" || d.SerialNumberID == profileSetting.SerialNumberID)
            ).ToList();

            if (edidMatches.Any())
            {
                // Priorizar monitores activos, luego el mismo tipo de conexión.
                var bestEdidMatch = edidMatches
                    .OrderByDescending(d => d.IsEnabled ? 1 : 0)
                    .ThenByDescending(d => d.ConnectionType == profileSetting.ConnectionType ? 1 : 0)
                    .First();
                logger.Info($"✓ Nivel 2 - Coincidencia EDID: {bestEdidMatch.FriendlyName} via {bestEdidMatch.ConnectionType}");
                return bestEdidMatch;
            }

            // Nivel 3: Fallback a FriendlyName + AdapterId.
            var nameAdapterMatch = systemDisplays.FirstOrDefault(d =>
                d.FriendlyName == profileSetting.ReadableDeviceName &&
                $"{d.AdapterId.HighPart:X8}{d.AdapterId.LowPart:X8}" == profileSetting.AdapterId
            );
            if (nameAdapterMatch != null)
            {
                logger.Info($"✓ Nivel 3 - Coincidencia por Nombre+Adaptador: {nameAdapterMatch.FriendlyName}");
                return nameAdapterMatch;
            }

            // Nivel 4: Fallback final a solo FriendlyName.
            var nameMatch = systemDisplays.FirstOrDefault(d => d.FriendlyName == profileSetting.ReadableDeviceName);
            if (nameMatch != null)
            {
                logger.Warn($"⚠ Nivel 4 - Coincidencia de respaldo por nombre: {nameMatch.FriendlyName}. Esto puede ser incorrecto.");
                return nameMatch;
            }

            logger.Error($"✗ NO SE ENCONTRÓ COINCIDENCIA para '{profileSetting.ReadableDeviceName}'");
            return null;
        }

        public async Task<ProfileApplyResult> ApplyProfileAsync(Profile profile)
        {
            try
            {
                ProfileApplyResult result = new ProfileApplyResult { AudioSuccess = true };

                // Step 1: Obtener la configuración completa del sistema (monitores activos e inactivos).
                var currentSystemDisplays = DisplayConfigHelper.GetDisplayConfigs();
                if (currentSystemDisplays.Count == 0)
                {
                    logger.Error("ApplyProfileAsync: No se pudo obtener la configuración de pantalla actual del sistema.");
                    return new ProfileApplyResult { Success = false };
                }

                // Step 2: Preparar la configuración de pantalla de destino emparejando monitores del perfil con monitores del sistema.
                var displayConfigs = new List<DisplayConfigHelper.DisplayConfigInfo>();
                if (profile.DisplaySettings.Count > 0)
                {
                    foreach (var profileSetting in profile.DisplaySettings)
                    {
                        // Usar la nueva lógica de emparejamiento jerárquico
                        var matchedSystemDisplay = FindBestMatchForProfile(profileSetting, currentSystemDisplays);

                        if (matchedSystemDisplay != null)
                        {
                            // Crear una nueva configuración usando los identificadores actuales del sistema,
                            // pero con los ajustes deseados del perfil.
                            displayConfigs.Add(new DisplayConfigHelper.DisplayConfigInfo
                            {
                                // Ajustes deseados del perfil
                                IsEnabled = profileSetting.IsEnabled,
                                IsPrimary = profileSetting.IsPrimary,
                                Width = profileSetting.Width,
                                Height = profileSetting.Height,
                                RefreshRate = profileSetting.Frequency,
                                IsHdrEnabled = profileSetting.IsHdrEnabled,
                                Rotation = (DisplayConfigHelper.DISPLAYCONFIG_ROTATION)profileSetting.Rotation,
                                DisplayPositionX = profileSetting.DisplayPositionX,
                                DisplayPositionY = profileSetting.DisplayPositionY,

                                // Identificadores actuales del sistema
                                DeviceName = matchedSystemDisplay.DeviceName,
                                AdapterId = matchedSystemDisplay.AdapterId,
                                SourceId = matchedSystemDisplay.SourceId,
                                TargetId = matchedSystemDisplay.TargetId,

                                // Otra información útil
                                PathIndex = matchedSystemDisplay.PathIndex,
                                FriendlyName = matchedSystemDisplay.FriendlyName,
                                PhysicalConnectionId = matchedSystemDisplay.PhysicalConnectionId,
                                ConnectionType = matchedSystemDisplay.ConnectionType,
                                IsHdrSupported = matchedSystemDisplay.IsHdrSupported // Usar el soporte actual del sistema
                            });
                        }
                        else
                        {
                            logger.Warn($"No se pudo encontrar un monitor del sistema coincidente para el monitor del perfil '{profileSetting.ReadableDeviceName}'. Podría estar desconectado.");
                        }
                    }
                }

                // Step 3: Set Primary Display first (adjusts positions in the config list)
                if (displayConfigs.Count > 0)
                {
                    logger.Debug("Setting primary display...");
                    result.PrimaryChanged = DisplayConfigHelper.SetPrimaryDisplay(displayConfigs);
                    if (!result.PrimaryChanged)
                    {
                        logger.Warn("Failed to set primary display.");
                        result.Success = false;
                    }
                    else
                    {
                        logger.Info("Set primary display successfully");
                    }
                }

                // Step 4: Choose application path (Staged or Simple)
                if (_settingsManager.ShouldUseStagedApplication() && displayConfigs.Count > 1)
                {
                    logger.Info("Using staged application path.");
                    result.DisplayConfigApplied = ApplyStagedConfiguration(displayConfigs);
                    result.ResolutionChanged = result.DisplayConfigApplied; // Staged method handles resolution
                }
                else
                {
                    logger.Info("Using simple application path.");
                    // --- Simple Path Logic ---
                    // 4.1: Apply Topology
                    result.DisplayConfigApplied = DisplayConfigHelper.ApplyDisplayTopology(displayConfigs);
                    if(result.DisplayConfigApplied)
                    {
                        // 4.2: Apply Resolution/Frequency
                        logger.Info("Applying resolution and refresh rate for all enabled monitors...");
                        bool allResolutionsChanged = true;
                        foreach (var displayConfig in displayConfigs.Where(d => d.IsEnabled))
                        {
                            if (DisplayHelper.IsMonitorConnected(displayConfig.DeviceName))
                            {
                                if (!DisplayHelper.ChangeResolution(displayConfig.DeviceName, displayConfig.Width, displayConfig.Height, (int)displayConfig.RefreshRate))
                                {
                                    logger.Warn($"Failed to change resolution for {displayConfig.DeviceName}");
                                    allResolutionsChanged = false;
                                }
                                else
                                {
                                    logger.Info($"Successfully changed resolution for {displayConfig.DeviceName} to {displayConfig.Width}x{displayConfig.Height}@{displayConfig.RefreshRate}Hz");
                                }
                            }
                        }
                        result.ResolutionChanged = allResolutionsChanged;
                    }
                }

                // Step 5: Apply DPI and Final HDR (DPI is always separate, HDR is final confirmation)
                if (result.DisplayConfigApplied)
                {
                    bool allDpiChanged = true;
                    foreach (var displayConfig in displayConfigs.Where(d => d.IsEnabled))
                    {
                        if (DisplayHelper.IsMonitorConnected(displayConfig.DeviceName))
                        {
                            // Buscar el DPI del perfil original para este display
                            var profileSetting = profile.DisplaySettings.FirstOrDefault(s =>
                                s.ReadableDeviceName == displayConfig.FriendlyName ||
                                s.PhysicalConnectionId == displayConfig.PhysicalConnectionId);

                            if (profileSetting != null)
                            {
                                if(!DpiHelper.SetDPIScaling(displayConfig.DeviceName, profileSetting.DpiScaling))
                                {
                                    logger.Warn($"Failed to set DPI scaling for {displayConfig.DeviceName}");
                                    allDpiChanged = false;
                                }
                            }
                        }
                    }
                    result.DpiChanged = allDpiChanged;

                    logger.Debug("Applying final HDR settings...");
                    if (!DisplayConfigHelper.ApplyHdrSettings(displayConfigs))
                    {
                        logger.Warn("Failed to apply final HDR settings.");
                    }
                }

                result.Success = result.PrimaryChanged && result.DisplayConfigApplied && result.ResolutionChanged && result.DpiChanged;

                // Step 6: Apply Audio Settings (common to both paths)
                if (profile.AudioSettings != null)
                {
                    result.AudioSuccess = AudioHelper.ApplyAudioSettings(profile.AudioSettings);
                }

                if (result.Success)
                {
                    _currentProfileId = profile.Id;
                    await _settingsManager.SetCurrentProfileIdAsync(profile.Id);
                    ProfileApplied?.Invoke(this, profile);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying profile");
                return new ProfileApplyResult { Success = false };
            }
        }

        public string GetApplyResultErrorMessage(string profileName, ProfileApplyResult result)
        {
            string errorDetails =
                $"Failed to apply profile '{profileName}'.\n" +
                $"Some settings may not have been applied correctly.\n\n" +
                $"Primary Changed: {result.PrimaryChanged},\n" +
                $"Display Topology Applied: {result.DisplayConfigApplied},\n" +
                $"Resolution Frequency Changed: {result.ResolutionChanged},\n" +
                $"DPI Scaling Changed: {result.DpiChanged},\n" +
                $"Audio Success: {result.AudioSuccess}";

            return errorDetails;
        }

        public Profile GetCurrentProfile()
        {
            if (string.IsNullOrEmpty(_currentProfileId))
                return null;
            return GetProfile(_currentProfileId);
        }

        public List<Profile> GetAllProfiles()
        {
            return _profiles.ToList();
        }

        public Profile GetProfile(string profileId)
        {
            return _profiles.FirstOrDefault(p => p.Id == profileId);
        }

        public Profile GetDefaultProfile()
        {
            return _profiles.FirstOrDefault(p => p.IsDefault);
        }

        public void AddProfile(Profile profile)
        {
            _profiles.Add(profile);
            ProfileAdded?.Invoke(this, profile);
        }

        public async Task<bool> AddProfileAsync(Profile profile)
        {
            AddProfile(profile);
            return await SaveProfileAsync(profile);
        }

        public void UpdateProfile(Profile profile)
        {
            var existingProfile = GetProfile(profile.Id);
            if (existingProfile != null)
            {
                var index = _profiles.IndexOf(existingProfile);
                profile.UpdateLastModified();
                _profiles[index] = profile;
                ProfileUpdated?.Invoke(this, profile);
            }
        }

        public async Task<bool> UpdateProfileAsync(Profile profile)
        {
            UpdateProfile(profile);
            return await SaveProfileAsync(profile);
        }

        public void DeleteProfile(string profileId)
        {
            _profiles.RemoveAll(p => p.Id == profileId);
            ProfileDeleted?.Invoke(this, profileId);
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            try
            {
                DeleteProfile(profileId);
                var filePath = GetProfileFilePath(profileId);
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error deleting profile");
                return false;
            }
        }

        public void SetDefaultProfile(string profileId)
        {
            foreach (var profile in _profiles)
            {
                profile.IsDefault = profile.Id == profileId;
            }
        }

        public async Task<bool> SetDefaultProfileAsync(string profileId)
        {
            SetDefaultProfile(profileId);
            bool success = true;
            foreach (var profile in _profiles)
            {
                if (!await SaveProfileAsync(profile))
                {
                    success = false;
                }
            }
            return success;
        }

        public bool HasProfile(string name)
        {
            return _profiles.Exists(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public string GetUniqueProfileName(string baseName)
        {
            if (!HasProfile(baseName))
                return baseName;

            int counter = 1;
            string uniqueName;
            do
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            } while (HasProfile(uniqueName));

            return uniqueName;
        }

        public int GetProfileCount()
        {
            return _profiles.Count;
        }

        public string GetProfilesFilePath()
        {
            return _profilesFilePath;
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder;
        }

        public string GetProfilesFolder()
        {
            return _profilesFolderPath;
        }

        public async Task<bool> ExportProfileAsync(string profileId, string destinationPath)
        {
            try
            {
                var profile = GetProfile(profileId);
                if (profile == null)
                    return false;

                var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(destinationPath, json));
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error exporting profile");
                return false;
            }
        }

        public async Task<Profile> ImportProfileAsync(string sourcePath)
        {
            try
            {
                var json = await Task.Run(() => File.ReadAllText(sourcePath));
                var profile = JsonConvert.DeserializeObject<Profile>(json);
                
                if (profile == null)
                    return null;

                // Generate new ID if profile already exists
                if (GetProfile(profile.Id) != null)
                {
                    profile.Id = Guid.NewGuid().ToString();
                }

                // Ensure unique name
                profile.Name = GetUniqueProfileName(profile.Name);
                profile.UpdateLastModified();

                await AddProfileAsync(profile);
                return profile;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error importing profile");
                return null;
            }
        }

        public List<Profile> GetProfilesWithHotkeys()
        {
            return _profiles.Where(p => p.HotkeyConfig != null && 
                                       p.HotkeyConfig.IsEnabled && 
                                       p.HotkeyConfig.Key != System.Windows.Input.Key.None).ToList();
        }

        public List<Profile> GetAllProfilesWithHotkeys()
        {
            return _profiles.Where(p => p.HotkeyConfig != null && 
                                       p.HotkeyConfig.Key != System.Windows.Input.Key.None).ToList();
        }

        public Profile GetProfileByHotkey(HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return null;

            return _profiles.FirstOrDefault(p => p.HotkeyConfig != null &&
                                                p.HotkeyConfig.IsEnabled &&
                                                p.HotkeyConfig.Equals(hotkey));
        }

        public bool HasHotkeyConflict(string profileId, HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return false;

            return _profiles.Any(p => p.Id != profileId &&
                                     p.HotkeyConfig != null &&
                                     p.HotkeyConfig.Key != System.Windows.Input.Key.None &&
                                     p.HotkeyConfig.Equals(hotkey));
        }

        public Profile FindConflictingProfile(string excludeProfileId, HotkeyConfig hotkey)
        {
            if (hotkey?.Key == System.Windows.Input.Key.None)
                return null;

            return _profiles.FirstOrDefault(p => p.Id != excludeProfileId &&
                                                p.HotkeyConfig != null &&
                                                p.HotkeyConfig.Key != System.Windows.Input.Key.None &&
                                                p.HotkeyConfig.Equals(hotkey));
        }

        public async Task<bool> ClearHotkeyAsync(string profileId)
        {
            try
            {
                var profile = GetProfile(profileId);
                if (profile?.HotkeyConfig != null)
                {
                    profile.HotkeyConfig = new HotkeyConfig();
                    return await UpdateProfileAsync(profile);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error clearing hotkey for profile {profileId}");
                return false;
            }
        }

        public Dictionary<string, HotkeyConfig> GetAllHotkeys()
        {
            var hotkeys = new Dictionary<string, HotkeyConfig>();
            
            foreach (var profile in _profiles.Where(p => p.HotkeyConfig != null && 
                                                        p.HotkeyConfig.IsEnabled && 
                                                        p.HotkeyConfig.Key != System.Windows.Input.Key.None))
            {
                hotkeys[profile.Id] = profile.HotkeyConfig;
            }
            
            return hotkeys;
        }

        public Profile DuplicateProfile(string profileId)
        {
            var sourceProfile = GetProfile(profileId);
            if (sourceProfile == null)
                return null;

            // Create new profile with duplicated data
            var duplicatedProfile = new Profile
            {
                Id = Guid.NewGuid().ToString(),
                Name = GetUniqueProfileName(sourceProfile.Name),
                Description = sourceProfile.Description,
                IsDefault = false, // Never duplicate as default
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                DisplaySettings = sourceProfile.DisplaySettings.Select(ds => new DisplaySetting
                {
                    DeviceName = ds.DeviceName,
                    DeviceString = ds.DeviceString,
                    ReadableDeviceName = ds.ReadableDeviceName,
                    Width = ds.Width,
                    Height = ds.Height,
                    Frequency = ds.Frequency,
                    DpiScaling = ds.DpiScaling,
                    IsPrimary = ds.IsPrimary,
                    AdapterId = ds.AdapterId,
                    SourceId = ds.SourceId,
                    IsEnabled = ds.IsEnabled,
                    PathIndex = ds.PathIndex,
                    TargetId = ds.TargetId,
                    DisplayPositionX = ds.DisplayPositionX,
                    DisplayPositionY = ds.DisplayPositionY,
                    IsHdrSupported = ds.IsHdrSupported,
                    IsHdrEnabled = ds.IsHdrEnabled,
                    Rotation = ds.Rotation,
                    ManufacturerName = ds.ManufacturerName,
                    ProductCodeID = ds.ProductCodeID,
                    SerialNumberID = ds.SerialNumberID,
                    PhysicalConnectionId = ds.PhysicalConnectionId,
                    ConnectionType = ds.ConnectionType,
                    ConnectorInstance = ds.ConnectorInstance,
                    AvailableResolutions = ds.AvailableResolutions != null ? new List<string>(ds.AvailableResolutions) : new List<string>(),
                    AvailableDpiScaling = ds.AvailableDpiScaling != null ? new List<uint>(ds.AvailableDpiScaling) : new List<uint>(),
                    AvailableRefreshRates = ds.AvailableRefreshRates != null ? new Dictionary<string, List<int>>(ds.AvailableRefreshRates.ToDictionary(kvp => kvp.Key, kvp => new List<int>(kvp.Value))) : new Dictionary<string, List<int>>()
                }).ToList(),
                AudioSettings = sourceProfile.AudioSettings != null ? new AudioSetting
                {
                    DefaultPlaybackDeviceId = sourceProfile.AudioSettings.DefaultPlaybackDeviceId,
                    PlaybackDeviceName = sourceProfile.AudioSettings.PlaybackDeviceName,
                    DefaultCaptureDeviceId = sourceProfile.AudioSettings.DefaultCaptureDeviceId,
                    CaptureDeviceName = sourceProfile.AudioSettings.CaptureDeviceName,
                    ApplyPlaybackDevice = sourceProfile.AudioSettings.ApplyPlaybackDevice,
                    ApplyCaptureDevice = sourceProfile.AudioSettings.ApplyCaptureDevice
                } : new AudioSetting(),
                HotkeyConfig = new HotkeyConfig() // Clear hotkey to avoid conflicts
            };

            return duplicatedProfile;
        }

        public async Task<Profile> DuplicateProfileAsync(string profileId)
        {
            var duplicatedProfile = DuplicateProfile(profileId);
            if (duplicatedProfile == null)
                return null;

            if (await AddProfileAsync(duplicatedProfile))
            {
                return duplicatedProfile;
            }

            return null;
        }

private bool ApplyStagedConfiguration(List<DisplayConfigHelper.DisplayConfigInfo> targetConfigs)
        {
            try
            {
                logger.Info($"Starting staged application for {targetConfigs.Count} displays");

                // --- Get current system state to identify active monitors ---
                var currentSystemDisplays = DisplayConfigHelper.GetDisplayConfigs();
                var currentlyActiveSystemDisplayIds = new HashSet<uint>(currentSystemDisplays.Where(d => d.IsEnabled).Select(d => d.TargetId));
                logger.Debug($"Found {currentlyActiveSystemDisplayIds.Count} currently active display(s).");

                // --- Phase 1: Apply target configuration only to monitors that are already active ---
                var phase1Configs = targetConfigs
                    .Where(tc => currentlyActiveSystemDisplayIds.Contains(tc.TargetId) && tc.IsEnabled)
                    .ToList();

                if (phase1Configs.Any())
                {
                    logger.Info($"Phase 1: Applying new settings for {phase1Configs.Count} monitor(s) that are already active.");

                    // Step 1.1: Apply partial topology (activation flags, rotation)
                    if (!DisplayConfigHelper.ApplyPartialDisplayTopology(phase1Configs))
                    {
                        logger.Error("Phase 1 (partial topology) failed. Falling back to single-step application.");
                        return DisplayConfigHelper.ApplyDisplayTopology(targetConfigs) &&
                               DisplayConfigHelper.ApplyDisplayPosition(targetConfigs) &&
                               DisplayConfigHelper.ApplyHdrSettings(targetConfigs);
                    }
                    
                    // Step 1.2: Explicitly apply resolution and refresh rate for the active monitors.
                    // This is the critical step to stabilize the system before adding more monitors.
                    logger.Info("Phase 1: Applying resolution and refresh rate changes for active monitors.");
                    foreach (var config in phase1Configs)
                    {
                        logger.Debug($"Phase 1: Changing mode for {config.DeviceName} to {config.Width}x{config.Height}@{config.RefreshRate}Hz");
                        if (!DisplayHelper.ChangeResolution(config.DeviceName, config.Width, config.Height, (int)config.RefreshRate))
                        {
                            logger.Warn($"Phase 1: Failed to change resolution for {config.DeviceName}.");
                        }
                    }

                    // Step 1.3: Apply HDR settings for this subset
                    if (!DisplayConfigHelper.ApplyHdrSettings(phase1Configs))
                    {
                        logger.Warn("Phase 1: Some HDR settings failed to apply.");
                    }

                    logger.Info("Phase 1 completed. Waiting for stabilization...");
                    int pauseMs = _settingsManager.GetStagedApplicationPauseMs();
                    System.Threading.Thread.Sleep(pauseMs);
                }
                else
                {
                    logger.Info("No currently active monitors are part of the new profile's active set. Skipping Phase 1.");
                }

                // --- Phase 2: Apply the full configuration to activate remaining monitors and finalize state ---
                logger.Info("Phase 2: Applying the full target profile.");

                // Use the standard ApplyDisplayTopology which will handle enabling/disabling correctly
                if (!DisplayConfigHelper.ApplyDisplayTopology(targetConfigs))
                {
                    logger.Error("Phase 2 failed: Could not apply the full display topology.");
                    return false;
                }

                // Apply final positions for all monitors
                if (!DisplayConfigHelper.ApplyDisplayPosition(targetConfigs))
                {
                    logger.Warn("Phase 2: Failed to apply final display positions.");
                }

                // Apply final HDR settings for all monitors
                if (!DisplayConfigHelper.ApplyHdrSettings(targetConfigs))
                {
                    logger.Warn("Phase 2: Some final HDR settings failed to apply.");
                }

                logger.Info("Staged application completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during staged application");
                return false;
            }
        }

        private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    }
}