using System;
using System.Collections.Generic;
using DisplayProfileManager.Helpers;
using Newtonsoft.Json;

namespace DisplayProfileManager.Core
{
    public class Profile
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; } = false;

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonProperty("lastModifiedDate")]
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        // Datos CCD completos (estilo MonitorSwitcherGUI) - estos son los que se usan para aplicar el perfil
        [JsonProperty("ccdPaths")]
        public List<CcdPathInfo> CcdPaths { get; set; } = new List<CcdPathInfo>();

        [JsonProperty("ccdModes")]
        public List<CcdModeInfo> CcdModes { get; set; } = new List<CcdModeInfo>();

        [JsonProperty("monitorInfo")]
        public List<MonitorAdditionalInfo> MonitorInfo { get; set; } = new List<MonitorAdditionalInfo>();

        // DisplaySettings se mantiene para compatibilidad con UI y para HDR/DPI
        [JsonProperty("displaySettings")]
        public List<DisplaySetting> DisplaySettings { get; set; } = new List<DisplaySetting>();

        [JsonProperty("audioSettings")]
        public AudioSetting AudioSettings { get; set; } = new AudioSetting();

        [JsonProperty("hotkeyConfig")]
        public HotkeyConfig HotkeyConfig { get; set; } = new HotkeyConfig();


        public Profile()
        {
        }

        public Profile(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

        public void AddDisplaySetting(string deviceName, int width, int height, uint dpiScaling, int frequency = 60)
        {
            var setting = new DisplaySetting
            {
                DeviceName = deviceName,
                Width = width,
                Height = height,
                DpiScaling = dpiScaling,
                Frequency = frequency
            };

            DisplaySettings.Add(setting);
        }

        public void UpdateLastModified()
        {
            LastModifiedDate = DateTime.Now;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Información de ruta CCD (DisplayConfigPathInfo) - estilo MonitorSwitcherGUI
    /// </summary>
    public class CcdPathInfo
    {
        [JsonProperty("sourceAdapterId")]
        public string SourceAdapterId { get; set; } = string.Empty;

        [JsonProperty("sourceId")]
        public uint SourceId { get; set; }

        [JsonProperty("sourceModeInfoIdx")]
        public uint SourceModeInfoIdx { get; set; }

        [JsonProperty("sourceStatusFlags")]
        public uint SourceStatusFlags { get; set; }

        [JsonProperty("targetAdapterId")]
        public string TargetAdapterId { get; set; } = string.Empty;

        [JsonProperty("targetId")]
        public uint TargetId { get; set; }

        [JsonProperty("targetModeInfoIdx")]
        public uint TargetModeInfoIdx { get; set; }

        [JsonProperty("outputTechnology")]
        public uint OutputTechnology { get; set; }

        [JsonProperty("rotation")]
        public uint Rotation { get; set; }

        [JsonProperty("scaling")]
        public uint Scaling { get; set; }

        [JsonProperty("refreshRateNumerator")]
        public uint RefreshRateNumerator { get; set; }

        [JsonProperty("refreshRateDenominator")]
        public uint RefreshRateDenominator { get; set; }

        [JsonProperty("scanLineOrdering")]
        public uint ScanLineOrdering { get; set; }

        [JsonProperty("targetAvailable")]
        public bool TargetAvailable { get; set; }

        [JsonProperty("targetStatusFlags")]
        public uint TargetStatusFlags { get; set; }

        [JsonProperty("flags")]
        public uint Flags { get; set; }
    }

    /// <summary>
    /// Información de modo CCD (DisplayConfigModeInfo) - estilo MonitorSwitcherGUI
    /// </summary>
    public class CcdModeInfo
    {
        [JsonProperty("infoType")]
        public uint InfoType { get; set; } // 1=Source, 2=Target

        [JsonProperty("id")]
        public uint Id { get; set; }

        [JsonProperty("adapterId")]
        public string AdapterId { get; set; } = string.Empty;

        // Source mode (si InfoType == 1)
        [JsonProperty("sourceWidth")]
        public uint SourceWidth { get; set; }

        [JsonProperty("sourceHeight")]
        public uint SourceHeight { get; set; }

        [JsonProperty("sourcePixelFormat")]
        public uint SourcePixelFormat { get; set; }

        [JsonProperty("sourcePositionX")]
        public int SourcePositionX { get; set; }

        [JsonProperty("sourcePositionY")]
        public int SourcePositionY { get; set; }

        // Target mode (si InfoType == 2)
        [JsonProperty("targetPixelRate")]
        public long TargetPixelRate { get; set; }

        [JsonProperty("targetHSyncNumerator")]
        public uint TargetHSyncNumerator { get; set; }

        [JsonProperty("targetHSyncDenominator")]
        public uint TargetHSyncDenominator { get; set; }

        [JsonProperty("targetVSyncNumerator")]
        public uint TargetVSyncNumerator { get; set; }

        [JsonProperty("targetVSyncDenominator")]
        public uint TargetVSyncDenominator { get; set; }

        [JsonProperty("targetActiveWidth")]
        public uint TargetActiveWidth { get; set; }

        [JsonProperty("targetActiveHeight")]
        public uint TargetActiveHeight { get; set; }

        [JsonProperty("targetTotalWidth")]
        public uint TargetTotalWidth { get; set; }

        [JsonProperty("targetTotalHeight")]
        public uint TargetTotalHeight { get; set; }

        [JsonProperty("targetVideoStandard")]
        public uint TargetVideoStandard { get; set; }

        [JsonProperty("targetScanLineOrdering")]
        public uint TargetScanLineOrdering { get; set; }
    }

    /// <summary>
    /// Información adicional del monitor (EDID, nombres) - estilo MonitorSwitcherGUI
    /// </summary>
    public class MonitorAdditionalInfo
    {
        [JsonProperty("monitorFriendlyDevice")]
        public string MonitorFriendlyDevice { get; set; } = string.Empty;

        [JsonProperty("monitorDevicePath")]
        public string MonitorDevicePath { get; set; } = string.Empty;

        [JsonProperty("edidManufactureId")]
        public ushort EdidManufactureId { get; set; }

        [JsonProperty("edidProductCodeId")]
        public ushort EdidProductCodeId { get; set; }
    }

    public class DisplaySetting
    {
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonProperty("deviceString")]
        public string DeviceString { get; set; } = string.Empty;

        [JsonProperty("readableDeviceName")]
        public string ReadableDeviceName { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("frequency")]
        public int Frequency { get; set; } = 60;

        [JsonProperty("dpiScaling")]
        public uint DpiScaling { get; set; } = 100;

        [JsonProperty("isPrimary")]
        public bool IsPrimary { get; set; } = false;

        [JsonProperty("adapterId")]
        public string AdapterId { get; set; } = string.Empty;

        [JsonProperty("sourceId")]
        public uint SourceId { get; set; } = 0;

        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonProperty("pathIndex")]
        public uint PathIndex { get; set; } = 0;

        [JsonProperty("targetId")]
        public uint TargetId { get; set; } = 0;

        [JsonProperty("displayPositionX")]
        public int DisplayPositionX { get; set; } = 0;

        [JsonProperty("displayPositionY")]
        public int DisplayPositionY { get; set; } = 0;

        [JsonProperty("manufacturerName")]
        public string ManufacturerName { get; set; } = string.Empty;

        [JsonProperty("productCodeID")]
        public string ProductCodeID { get; set; } = string.Empty;

        [JsonProperty("serialNumberID")]
        public string SerialNumberID { get; set; } = string.Empty;

        [JsonProperty("availableResolutions")]
        public List<string> AvailableResolutions { get; set; } = new List<string>();

        [JsonProperty("availableDpiScaling")]
        public List<uint> AvailableDpiScaling { get; set; } = new List<uint>();

        [JsonProperty("availableRefreshRates")]
        public Dictionary<string, List<int>> AvailableRefreshRates { get; set; } = new Dictionary<string, List<int>>();

        [JsonProperty("isHdrSupported")]
        public bool IsHdrSupported { get; set; } = false;

        [JsonProperty("isHdrEnabled")]
        public bool IsHdrEnabled { get; set; } = false;

        [JsonProperty("rotation")]
        public int Rotation { get; set; } = 1; // Default to IDENTITY (1)

        [JsonProperty("physicalConnectionId")]
        public string PhysicalConnectionId { get; set; } = string.Empty;

        [JsonProperty("connectionType")]
        public string ConnectionType { get; set; } = string.Empty;

        [JsonProperty("connectorInstance")]
        public uint ConnectorInstance { get; set; } = 0;

        public DisplaySetting()
        {
        }

        public string GetResolutionString()
        {
            return $"{Width}x{Height} @ {Frequency}Hz";
        }

        public string GetDpiString()
        {
            return $"{DpiScaling}%";
        }

        public override string ToString()
        {
            var enabledStatus = IsEnabled ? "Enabled" : "Disabled";
            var hdrStatus = IsHdrSupported ? (IsHdrEnabled ? "HDR On" : "HDR Off") : "No HDR";
            return $"{DeviceName}: {GetResolutionString()}, DPI: {GetDpiString()}, {hdrStatus} [{enabledStatus}]";
        }

        public void UpdateDeviceNameFromWMI()
        {
            string resolvedDeviceName = DisplayHelper.GetDeviceNameFromWMIMonitorID(ManufacturerName, ProductCodeID, SerialNumberID);
            if (string.IsNullOrEmpty(resolvedDeviceName))
            {
                resolvedDeviceName = DeviceName;
            }

            DeviceName = resolvedDeviceName;
        }
    }

    public class AudioSetting
    {
        [JsonProperty("defaultPlaybackDeviceId")]
        public string DefaultPlaybackDeviceId { get; set; } = string.Empty;

        [JsonProperty("defaultCaptureDeviceId")]
        public string DefaultCaptureDeviceId { get; set; } = string.Empty;

        [JsonProperty("playbackDeviceName")]
        public string PlaybackDeviceName { get; set; } = string.Empty;

        [JsonProperty("captureDeviceName")]
        public string CaptureDeviceName { get; set; } = string.Empty;

        [JsonProperty("applyPlaybackDevice")]
        public bool ApplyPlaybackDevice { get; set; } = false;

        [JsonProperty("applyCaptureDevice")]
        public bool ApplyCaptureDevice { get; set; } = false;

        public AudioSetting()
        {
        }

        public AudioSetting(string playbackId, string playbackName, string captureId, string captureName)
        {
            DefaultPlaybackDeviceId = playbackId;
            PlaybackDeviceName = playbackName;
            DefaultCaptureDeviceId = captureId;
            CaptureDeviceName = captureName;
        }

        public bool HasPlaybackDevice()
        {
            return !string.IsNullOrEmpty(DefaultPlaybackDeviceId);
        }

        public bool HasCaptureDevice()
        {
            return !string.IsNullOrEmpty(DefaultCaptureDeviceId);
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(PlaybackDeviceName))
                parts.Add($"Output: {PlaybackDeviceName}");
            if (!string.IsNullOrEmpty(CaptureDeviceName))
                parts.Add($"Input: {CaptureDeviceName}");
            return parts.Count > 0 ? string.Join(", ", parts) : "No audio devices configured";
        }
    }

    public class ProfileCollection
    {
        [JsonProperty("profiles")]
        public List<Profile> Profiles { get; set; } = new List<Profile>();

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [JsonProperty("currentProfileId")]
        public string CurrentProfileId { get; set; }

        public ProfileCollection()
        {
        }

        public void AddProfile(Profile profile)
        {
            Profiles.Add(profile);
            LastUpdated = DateTime.Now;
        }

        public void RemoveProfile(string profileId)
        {
            Profiles.RemoveAll(p => p.Id == profileId);
            LastUpdated = DateTime.Now;
        }

        public Profile GetProfile(string profileId)
        {
            return Profiles.Find(p => p.Id == profileId);
        }

        public Profile GetDefaultProfile()
        {
            return Profiles.Find(p => p.IsDefault);
        }

        public void SetDefaultProfile(string profileId)
        {
            foreach (var profile in Profiles)
            {
                profile.IsDefault = profile.Id == profileId;
            }
            LastUpdated = DateTime.Now;
        }

        public bool HasProfile(string name)
        {
            return Profiles.Exists(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public void UpdateProfile(Profile updatedProfile)
        {
            var existingProfile = GetProfile(updatedProfile.Id);
            if (existingProfile != null)
            {
                var index = Profiles.IndexOf(existingProfile);
                updatedProfile.UpdateLastModified();
                Profiles[index] = updatedProfile;
                LastUpdated = DateTime.Now;
            }
        }
    }
}