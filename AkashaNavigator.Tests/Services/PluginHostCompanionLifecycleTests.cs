using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginHostCompanionLifecycleTests
{
    [Fact]
    public void LoadPlugin_ShouldFailClosedWhenFirstEnableConsentIsDenied()
    {
        using var directory = new TemporaryDirectory();
        var pluginDirectory = Path.Combine(directory.Path, "plugin");
        var configDirectory = Path.Combine(directory.Path, "config");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.js"), string.Empty);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            JsonSerializer.Serialize(new
            {
                id = "companion-test",
                name = "Companion Test",
                version = "1.0.0",
                main = "main.js",
                permissions = new[] { "companion" },
                companion = new
                {
                    executable = "worker/win-x64/Worker.exe",
                    protocolVersion = 1,
                    lifetime = "plugin",
                    singleInstance = true
                }
            }));
        var consent = new RecordingConsentService { Approved = false };
        var manager = new RecordingCompanionProcessManager();
        using var host = CreateHost(manager, consent);

        InvokeLoadPlugin(host, pluginDirectory, configDirectory, "companion-test");

        Assert.Empty(host.LoadedPlugins);
        Assert.Equal(new[] { PluginPermissionConsentOperation.FirstEnable }, consent.Operations);
    }

    [Fact]
    public void LoadPlugin_ShouldRunMainScriptThroughCompanionApiWhenConsentIsApproved()
    {
        using var directory = new TemporaryDirectory();
        var pluginDirectory = Path.Combine(directory.Path, "plugin");
        var configDirectory = Path.Combine(directory.Path, "config");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "main.js"),
            "function onLoad() { " +
            "companion.start(); " +
            "companion.invoke('features.autoPick.setEnabled', { enabled: true }); " +
            "companion.invoke('features.autoDialogue.setEnabled', { enabled: true }); " +
            "companion.invoke('features.quickTeleport.setOptions', { enabled: true }); " +
            "companion.invoke('features.quickTeleport.deleteEverything'); " +
            "companion.invoke('features.9quickTeleport.setOptions'); " +
            "companion.invoke('features.quick/Teleport.setOptions'); " +
            "companion.invoke('automation.emergencyStop'); " +
            "}");
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            JsonSerializer.Serialize(new
            {
                id = "companion-script-test",
                name = "Companion Script Test",
                version = "1.0.0",
                main = "main.js",
                permissions = new[] { "companion" },
                companion = new
                {
                    executable = "worker/win-x64/Worker.exe",
                    protocolVersion = 1,
                    lifetime = "plugin",
                    singleInstance = true
                }
            }));
        var consent = new RecordingConsentService { Approved = true };
        var manager = new RecordingCompanionProcessManager();
        using var host = CreateHost(manager, consent);

        InvokeLoadPlugin(host, pluginDirectory, configDirectory, "companion-script-test");

        Assert.True(
            SpinWait.SpinUntil(
                () => manager.StartedPluginIds.Contains("companion-script-test"),
                TimeSpan.FromSeconds(2)),
            "The plugin main.js did not call companion.start().");
        Assert.True(
            SpinWait.SpinUntil(
                () => manager.InvokedMethods.Contains("automation.emergencyStop"),
                TimeSpan.FromSeconds(2)),
            "The companion allowlist rejected automation.emergencyStop.");
        Assert.Contains("features.autoPick.setEnabled", manager.InvokedMethods);
        Assert.Contains("features.autoDialogue.setEnabled", manager.InvokedMethods);
        Assert.Contains("features.quickTeleport.setOptions", manager.InvokedMethods);
        Assert.DoesNotContain("features.quickTeleport.deleteEverything", manager.InvokedMethods);
        Assert.DoesNotContain("features.9quickTeleport.setOptions", manager.InvokedMethods);
        Assert.DoesNotContain("features.quick/Teleport.setOptions", manager.InvokedMethods);
        Assert.Single(host.LoadedPlugins);
        Assert.Equal(new[] { PluginPermissionConsentOperation.FirstEnable }, consent.Operations);
    }

    [Fact]
    public void LoadPlugin_ShouldRejectManifestIdThatDoesNotMatchLibraryId()
    {
        using var directory = new TemporaryDirectory();
        var pluginDirectory = Path.Combine(directory.Path, "plugin");
        var configDirectory = Path.Combine(directory.Path, "config");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.js"), "function onLoad() { companion.start(); }");
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            JsonSerializer.Serialize(new
            {
                id = "different-plugin",
                name = "Different Plugin",
                version = "1.0.0",
                main = "main.js",
                permissions = new[] { "companion" },
                companion = new
                {
                    executable = "worker/win-x64/Worker.exe",
                    protocolVersion = 1,
                    lifetime = "plugin",
                    singleInstance = true
                }
            }));
        var consent = new RecordingConsentService { Approved = true };
        var manager = new RecordingCompanionProcessManager();
        using var host = CreateHost(manager, consent);

        InvokeLoadPlugin(host, pluginDirectory, configDirectory, "library-plugin");

        Assert.Empty(host.LoadedPlugins);
        Assert.Empty(consent.Operations);
        Assert.Empty(manager.StartedPluginIds);
        Assert.False(Directory.Exists(configDirectory));
    }

    [Fact]
    public void ProfileSwitch_ShouldStopLoadedCompanion()
    {
        var manager = new RecordingCompanionProcessManager();
        using var host = CreateHost(manager, new RecordingConsentService { Approved = true });
        var context = new PluginContext(
            "companion-test",
            "plugin",
            "config",
            new PluginManifest
            {
                Id = "companion-test",
                Name = "Companion Test",
                Version = "1.0.0",
                Main = "main.js"
            });
        AddLoadedPlugin(host, context);

        host.LoadPluginsForProfile("next-profile");

        Assert.Contains("companion-test", manager.StoppedPluginIds);
        Assert.Empty(host.LoadedPlugins);
    }

    private static PluginHost CreateHost(
        ICompanionProcessManager companionProcessManager,
        IPluginPermissionConsentService permissionConsentService)
    {
        var logService = new FakeLogService();
        var associationManager = new FakePluginAssociationManager();
        var pluginLibrary = new FakePluginLibrary();
        var runtimeBridge = new AkashaNavigator.Tests.TestPlayerRuntimeBridge();
        var overlayManager = new OverlayManager();
        var panelManager = new PanelManager();
        var cursorDetectionService = new AkashaNavigator.Tests.MockCursorDetectionService();
        var subtitleService = new SubtitleService(logService);
        var scriptExecutionQueue = new ScriptExecutionQueue(logService);
        var hotkeyService = new HotkeyService();
        var osdManager = new OsdManager();
        var hostObjectFactory = new PluginHostObjectFactory(
            runtimeBridge,
            overlayManager,
            panelManager,
            cursorDetectionService,
            subtitleService,
            scriptExecutionQueue,
            hotkeyService,
            osdManager,
            logService,
            companionProcessManager);

        return new PluginHost(
            true,
            logService,
            associationManager,
            pluginLibrary,
            runtimeBridge,
            overlayManager,
            panelManager,
            cursorDetectionService,
            subtitleService,
            scriptExecutionQueue,
            hotkeyService,
            osdManager,
            hostObjectFactory,
            companionProcessManager,
            permissionConsentService);
    }

    private static void InvokeLoadPlugin(
        PluginHost host,
        string pluginDirectory,
        string configDirectory,
        string pluginId)
    {
        var method = typeof(PluginHost).GetMethod("LoadPlugin", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(host, new object[] { pluginDirectory, configDirectory, pluginId });
    }

    private static void AddLoadedPlugin(PluginHost host, PluginContext context)
    {
        var field = typeof(PluginHost).GetField("_loadedPlugins", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var loadedPlugins = field!.GetValue(host) as IList;
        Assert.NotNull(loadedPlugins);
        loadedPlugins!.Add(context);
    }

    private sealed class RecordingConsentService : IPluginPermissionConsentService
    {
        public bool Approved { get; init; }

        public List<PluginPermissionConsentOperation> Operations { get; } = new();

        public bool EnsureHighRiskPermissionsApproved(
            PluginManifest manifest,
            PluginPermissionConsentOperation operation)
        {
            Operations.Add(operation);
            return Approved;
        }

        public bool RevokeHighRiskPermissionConsent(string pluginId) => true;
    }

    private sealed class RecordingCompanionProcessManager : ICompanionProcessManager
    {
        public ConcurrentBag<string> StartedPluginIds { get; } = new();

        public ConcurrentBag<string> InvokedMethods { get; } = new();

        public List<string> StoppedPluginIds { get; } = new();

        public Task<CompanionStatus> StartAsync(
            string pluginId,
            string pluginDirectory,
            CompanionManifest manifest,
            CancellationToken cancellationToken = default)
        {
            StartedPluginIds.Add(pluginId);
            return Task.FromResult(new CompanionStatus(true, "running", 1));
        }

        public Task<JsonElement?> InvokeAsync(
            string pluginId,
            string method,
            JsonElement? payload,
            CancellationToken cancellationToken = default)
        {
            InvokedMethods.Add(method);
            return Task.FromResult(payload);
        }

        public CompanionStatus GetStatus(string pluginId) => new(false, "stopped");

        public Task StopAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            StoppedPluginIds.Add(pluginId);
            return Task.CompletedTask;
        }

        public Task StopAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose() { }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"AkashaNavigator.PluginHostCompanionTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
