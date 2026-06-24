using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using HarvestmoonGCS.Core.Services.AI;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.ViewModels;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.Helpers;
using HarvestmoonGCS.Core.Services;
using Serilog;
using Uno.Resizetizer;
using CoreThemeService = HarvestmoonGCS.Core.Services.IThemeService;

namespace HarvestmoonGCS;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        // Initialize Serilog once at startup so diagnostic messages from services
        // (YoloDetector, HarvestFunctionalService, camera services, etc.) actually
        // reach stdout/file. Before this, every Serilog.Log.* call was a no-op.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                System.IO.Path.Combine(System.AppContext.BaseDirectory, "logs", "moonharvest-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("[App] MoonHarvest GCS starting. BaseDirectory={Dir}", System.AppContext.BaseDirectory);

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
        }
        catch (DllNotFoundException)
        {
            // Optional runtime dependency on Linux. Keep app running without video playback.
        }
        catch (Exception)
        {
            // Avoid startup crash when LibVLC initialization fails for any other reason.
        }

        this.InitializeComponent();
        Services = ConfigureServices();
    }

    public new static App Current => (App)Application.Current;
    public IServiceProvider Services { get; }
    public static T GetService<T>() where T : notnull
    {
        return Current.Services.GetRequiredService<T>();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<FlightViewModel>();
        // MapViewModel is singleton so Dashboard and Map page share the same waypoints,
        // geofence and vehicle position state instead of getting separate instances.
        services.AddSingleton<MapViewModel>();
        services.AddTransient<StatsViewModel>();
        services.AddTransient<TrackerViewModel>();
        services.AddTransient<CalibrationViewModel>();
        services.AddTransient<LoRaViewModel>();
        services.AddTransient<TlogViewModel>();
#if !__WASM__
        services.AddSingleton<ChatViewModel>();
#endif

        // Core services
        services.AddSingleton<IDispatcherService, WinUIDispatcherService>();
        services.AddSingleton<ILocalizationService, UnoLocalizationService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<CoreThemeService, ThemeService>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<ObservabilityService>();
        services.AddSingleton<PageCacheManager>();
        services.AddSingleton<Serilog.ILogger>(_ => Log.Logger);
        services.AddSingleton<ILoggingService, SerilogLoggingService>();

        services.AddSingleton<IMavLinkService, HarvestmoonGCS.Core.Services.MavLinkService>();
        services.AddSingleton<IWaypointService, WaypointService>();
        services.AddSingleton<IMissionService, MissionService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IGeofenceService, GeofenceService>();
        services.AddSingleton<GeofenceMonitor>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton(new HarvestmoonGCS.Core.Models.OfflineMapSettings());
        services.AddSingleton<MBTilesDatabase>();
        services.AddSingleton<IOfflineMapService, OfflineMapService>();
        services.AddSingleton<ITileDownloadService, TileDownloadService>();

        services.AddSingleton<ITelemetryStore, SqliteTelemetryStore>();
        services.AddSingleton<ISpeechService, UnoSpeechService>();
        services.AddSingleton<IFileService, UnoFileService>();
        services.AddSingleton<HarvestFunctionalService>();

#if __ANDROID__
        // Android needs Context for camera, file, and other platform services
        services.AddSingleton<Android.Content.Context>(Android.App.Application.Context);
        services.AddSingleton<ISerialPortService, HarvestmoonGCS.Platforms.Android.Services.AndroidSerialPortService>();
        services.AddSingleton<ICameraService, HarvestmoonGCS.Platforms.Android.Services.AndroidCameraService>();
        services.AddSingleton<IVideoPlayerService, HarvestmoonGCS.Platforms.Android.Services.AndroidVideoPlayerService>();
#else
        services.AddSingleton<ISerialPortService, DesktopSerialPortService>();
        services.AddSingleton<ICameraService, PythonCameraService>();
#endif
        services.AddSingleton<IVideoRecorderService, VideoRecorderService>();
        services.AddSingleton<IncidentTimelineService>();
        services.AddSingleton<MissionLoggingService>();
        services.AddSingleton<ILoRaService, LoRaService>();
        services.AddSingleton<ITlogPlayerService, TlogPlayerService>();

        // AI/PIA services
        services.AddSingleton<IAlertManager, AlertManager>();
        services.AddSingleton<AlertManager>(sp => (AlertManager)sp.GetRequiredService<IAlertManager>());
        services.AddSingleton<IVoiceRecognitionService, NoOpVoiceRecognitionService>();
        services.AddAIServices();
#if !__WASM__
        services.AddSingleton<IPIAHistoryStore, SqlitePIAHistoryStore>();
        services.AddPIAIntelligence();
#endif
        services.AddSingleton<ResearchTelemetryExportService>();

        return services.BuildServiceProvider();
    }


    private Window? _window;
    public static Window? MainWindow { get; private set; }


    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if NET6_0_OR_GREATER && WINDOWS && !HAS_UNO
        _window = new MainWindow();
#else
        _window = new Window();
#endif
        MainWindow = _window;

#if DEBUG
        MainWindow.UseStudio();
#endif


        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;

            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            var navigated = rootFrame.Navigate(typeof(MainPage_Modern), args.Arguments);
            if (!navigated || rootFrame.Content == null)
            {
                rootFrame.Content = new MainPage_Modern();
            }
        }

        // Ensure the current window is active
        MainWindow.Activate();
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
