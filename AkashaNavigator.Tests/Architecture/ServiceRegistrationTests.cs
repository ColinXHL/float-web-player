using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AkashaNavigator.Tests.Architecture;

public class ServiceRegistrationTests
{
    [Fact]
    public void ConfigureAppServices_ResolvesSameProfileManagerInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IProfileManager>();
        var second = provider.GetRequiredService<IProfileManager>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureAppServices_ResolvesSamePluginHostInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IPluginHost>();
        var second = provider.GetRequiredService<IPluginHost>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureAppServices_RegistersOverlayManagerAsSingletonByType()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IOverlayManager));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(OverlayManager), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_RegistersPanelManagerAsSingletonByType()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IPanelManager));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(PanelManager), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_ShouldResolveHotkeyManager()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var manager = provider.GetRequiredService<HotkeyManager>();
        Assert.NotNull(manager);
    }

    [Fact]
    public void ConfigureAppServices_RegistersCompanionSecurityServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var manager = services.Single(d => d.ServiceType == typeof(ICompanionProcessManager));
        var consent = services.Single(d => d.ServiceType == typeof(IPluginPermissionConsentService));

        Assert.Equal(ServiceLifetime.Singleton, manager.Lifetime);
        Assert.Equal(typeof(CompanionProcessManager), manager.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, consent.Lifetime);
        Assert.Equal(typeof(PluginPermissionConsentService), consent.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_RegistersUpdateManifestServiceAsSingleton()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(
            service => service.ServiceType == typeof(IUpdateManifestService));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(UpdateManifestService), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_RegistersPluginRepositoryServiceAsSingleton()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(
            service => service.ServiceType == typeof(IPluginRepositoryService));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(PluginRepositoryService), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_RegistersRepositoryPluginServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var subscriptions = services.Single(
            service =>
                service.ServiceType == typeof(IPluginSubscriptionService));
        var installer = services.Single(
            service => service.ServiceType == typeof(IPluginInstaller));
        var writeCoordinator = services.Single(
            service => service.ServiceType == typeof(PluginWriteCoordinator));

        Assert.Equal(ServiceLifetime.Singleton, subscriptions.Lifetime);
        Assert.Equal(
            typeof(PluginSubscriptionService),
            subscriptions.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, installer.Lifetime);
        Assert.Equal(typeof(PluginInstaller), installer.ImplementationType);

        var distributionResolver = services.Single(
            service =>
                service.ServiceType ==
                typeof(IPluginDistributionResolver));
        Assert.Equal(ServiceLifetime.Singleton, distributionResolver.Lifetime);
        Assert.Equal(
            typeof(PluginDistributionResolver),
            distributionResolver.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, writeCoordinator.Lifetime);
    }

    [Fact]
    public void ConfigureAppServices_RegistersReleaseDownloadServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var selector = services.Single(
            service => service.ServiceType == typeof(IDownloadSourceSelector));
        var packages = services.Single(
            service => service.ServiceType == typeof(IPluginPackageService));
        var resources = services.Single(
            service => service.ServiceType == typeof(IPluginResourceUpdateService));

        Assert.Equal(ServiceLifetime.Singleton, selector.Lifetime);
        Assert.Equal(typeof(DownloadSourceSelector), selector.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, packages.Lifetime);
        Assert.Equal(typeof(PluginPackageService), packages.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, resources.Lifetime);
        Assert.Equal(typeof(PluginResourceUpdateService), resources.ImplementationType);
    }
}
