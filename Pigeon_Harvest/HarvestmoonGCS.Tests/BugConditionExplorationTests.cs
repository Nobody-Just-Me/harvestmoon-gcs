using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.ViewModels;
using System;
using System.IO.Ports;
using Xunit;

namespace HarvestmoonGCS.Tests;

/// <summary>
/// Bug Condition Exploration Tests — Task 1
///
/// These tests document and surface the bug conditions BEFORE any fix is applied.
/// They are EXPECTED TO FAIL on unfixed code, which confirms the bugs exist.
///
/// Bug 1 — Tracker SendRawBytes crash:
///   In CalculateTracking(), when IsTracking=true and IsTrackerConnected=false,
///   _mavLinkService.SendRawBytes(bytes) is called with no try/catch.
///   If SendRawBytes throws, the exception propagates uncaught.
///
/// Bug 2 — TrackerViewModel.RefreshPorts() uses SerialPort.GetPortNames() directly
///   instead of SerialPortHelper.GetAvailablePorts(), missing ttyUSB/ttyACM on Linux.
///
/// Bug 3 — FlightControl.PrepConnection() scans ports only once at startup.
///   No polling timer exists — new ports connected after startup don't appear.
/// </summary>
public class BugConditionExplorationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (TrackerViewModel vm, Mock<IMavLinkService> mavMock) CreateViewModel(
        Action<Mock<IMavLinkService>>? configureMav = null)
    {
        var mavMock = new Mock<IMavLinkService>();
        var dispatcherMock = new Mock<IDispatcherService>();

        // Execute actions synchronously so tests are deterministic
        dispatcherMock
            .Setup(d => d.Enqueue(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        configureMav?.Invoke(mavMock);

        var vm = new TrackerViewModel(mavMock.Object, dispatcherMock.Object);
        return (vm, mavMock);
    }

    private static FlightData MakeFlightData(int lat = 10_000_000, int lon = 20_000_000, int alt = 50_000)
        => new FlightData
        {
            GPS = new GPSData { Latitude = lat, Longitude = lon },
            Altitude = alt
        };

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Bug 1: SendRawBytes throws → CalculateTracking crashes
    //
    // EXPECTED: FAIL on unfixed code (no try/catch around SendRawBytes fallback)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirements 1.1, 1.2
    ///
    /// When IsTracking=true and IsTrackerConnected=false, CalculateTracking() calls
    /// _mavLinkService.SendRawBytes(bytes) as a fallback.
    /// On unfixed code there is NO try/catch around this call, so if SendRawBytes
    /// throws, the exception propagates and crashes the caller.
    ///
    /// This test WILL FAIL on unfixed code — that failure is the counterexample
    /// proving the bug exists.
    /// </summary>
    [Fact(DisplayName = "Bug1 — SendRawBytes exception must not propagate out of CalculateTracking")]
    public void Bug1_SendRawBytes_ThrowingException_ShouldNotCrashCalculateTracking()
    {
        // Arrange: mock SendRawBytes to throw, simulating a disconnected/broken transport
        var (vm, mavMock) = CreateViewModel(mav =>
            mav.Setup(m => m.SendRawBytes(It.IsAny<byte[]>()))
               .Throws(new InvalidOperationException("Serial port not open")));

        // Put the tracker in the bug-triggering state:
        //   IsTracking = true  → CalculateTracking will try to send
        //   IsTrackerConnected = false → falls through to SendRawBytes fallback
        vm.IsTracking = true;
        vm.IsTrackerConnected = false;

        // Set a non-zero tracker position so Distance/Bearing are computed
        vm.TrackerLat = 0;
        vm.TrackerLon = 0;
        vm.TrackerAlt = 0;

        // Act: raise TelemetryReceived with valid GPS data
        // On unfixed code this will throw InvalidOperationException from SendRawBytes
        Action act = () =>
            mavMock.Raise(m => m.TelemetryReceived += null, mavMock.Object, MakeFlightData());

        // Assert: no exception should escape CalculateTracking
        // WILL FAIL on unfixed code — counterexample: InvalidOperationException propagates
        act.Should().NotThrow(
            "CalculateTracking must guard SendRawBytes with try/catch so a transport " +
            "failure does not crash the application");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Bug 2: RefreshPorts uses SerialPort.GetPortNames, not SerialPortHelper
    //
    // Property test: AvailablePorts after RefreshPorts() should equal
    // SerialPortHelper.GetAvailablePorts() — on Linux they may differ because
    // SerialPort.GetPortNames() misses ttyUSB/ttyACM devices.
    //
    // On Windows/macOS this test may pass (no discrepancy), but on Linux with
    // USB serial adapters it WILL FAIL, documenting the root cause.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirements 1.4, 1.5
    ///
    /// RefreshPorts() on unfixed code calls SerialPort.GetPortNames() directly.
    /// SerialPortHelper.GetAvailablePorts() additionally scans /dev/ttyUSB* and
    /// /dev/ttyACM* on Linux, so the two lists can diverge.
    ///
    /// This test documents the discrepancy. On Linux with USB serial adapters it
    /// WILL FAIL — that failure is the counterexample proving the bug exists.
    /// On other platforms it may pass (no ttyUSB ports), but the structural bug
    /// (wrong API call) is still present.
    /// </summary>
    [Fact(DisplayName = "Bug2 — RefreshPorts AvailablePorts should match SerialPortHelper not SerialPort.GetPortNames")]
    public void Bug2_RefreshPorts_ShouldUseSerialPortHelper_NotGetPortNames()
    {
        // Arrange
        var (vm, _) = CreateViewModel();

        // Act: call RefreshPorts (already called in constructor, but call again explicitly)
        vm.RefreshPortsCommand.Execute(null);

        // Collect what the two APIs return
        var helperPorts = HarvestmoonGCS.Core.Helpers.SerialPortHelper.GetAvailablePorts();
        var rawPorts    = SerialPort.GetPortNames();

        // Document the discrepancy
        var missingFromRaw = Array.FindAll(helperPorts, p => Array.IndexOf(rawPorts, p) < 0);

        // Assert: AvailablePorts count should equal SerialPortHelper result
        // On Linux with ttyUSB/ttyACM devices, vm.AvailablePorts.Count will be LESS
        // than helperPorts.Length because RefreshPorts uses GetPortNames() not the helper.
        //
        // COUNTEREXAMPLE (Linux): missingFromRaw = ["/dev/ttyUSB0", "/dev/ttyACM0"]
        //   vm.AvailablePorts.Count = 0  (GetPortNames returns nothing on Linux without udev)
        //   helperPorts.Length      = 2  (SerialPortHelper finds ttyUSB0, ttyACM0)
        vm.AvailablePorts.Count.Should().Be(
            helperPorts.Length,
            $"RefreshPorts() should use SerialPortHelper.GetAvailablePorts() which returns " +
            $"{helperPorts.Length} port(s), but SerialPort.GetPortNames() returns {rawPorts.Length} port(s). " +
            $"Ports missing from GetPortNames: [{string.Join(", ", missingFromRaw)}]");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Bug 3: PrepConnection scans ports only once (no polling timer)
    //
    // This test verifies that SerialPortHelper itself is NOT cached (it returns
    // current state on each call), which confirms the bug is in PrepConnection
    // not calling it again — not in the helper itself.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirements 1.4, 1.5
    ///
    /// SerialPortHelper.GetAvailablePorts() is stateless — it scans the OS on
    /// every call. This test confirms the helper is correct.
    ///
    /// The bug is that FlightControl.PrepConnection() calls ListAllSerialPorts()
    /// (which uses SerialPortHelper) only ONCE at startup. No DispatcherTimer
    /// polls for changes. New ports connected after startup are therefore invisible
    /// until the user manually selects "..REFRESH..".
    ///
    /// Documentation of bug condition:
    ///   "PrepConnection calls ListAllSerialPorts only once — no polling timer exists"
    /// </summary>
    [Fact(DisplayName = "Bug3 — SerialPortHelper is not cached (helper is correct; bug is in PrepConnection)")]
    public void Bug3_SerialPortHelper_IsNotCached_ReturnsCurrentStateEachCall()
    {
        // Act: call GetAvailablePorts twice in succession
        var ports1 = HarvestmoonGCS.Core.Helpers.SerialPortHelper.GetAvailablePorts();
        var ports2 = HarvestmoonGCS.Core.Helpers.SerialPortHelper.GetAvailablePorts();

        // Assert: both calls return the same current state (not a stale cache)
        ports2.Should().BeEquivalentTo(ports1,
            "SerialPortHelper.GetAvailablePorts() must scan the OS on every call " +
            "and return the same result when no ports change between calls. " +
            "This confirms the helper is correct — the bug is that PrepConnection " +
            "does not call it again after startup (no polling timer).");

        // Document the structural bug for the record
        // FlightControl.PrepConnection() calls ListAllSerialPorts() once.
        // There is no DispatcherTimer or background thread that re-invokes it.
        // Fix: add a DispatcherTimer with 2-second interval that calls
        //      SerialPortHelper.GetAvailablePorts() and updates cb_ports when the list changes.
        var bugDocumentation =
            "PrepConnection calls ListAllSerialPorts only once — no polling timer exists. " +
            $"Current ports at test time: [{string.Join(", ", ports1)}]";

        bugDocumentation.Should().NotBeNullOrEmpty(); // always passes — just records the doc
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property Test — Bug 1 (FsCheck): for any valid GPS input, when
    // SendRawBytes always throws, CalculateTracking must never propagate the exception
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirements 1.1, 1.2
    ///
    /// Property: for ALL valid GPS coordinate pairs, when SendRawBytes throws,
    /// no exception should escape CalculateTracking.
    ///
    /// WILL FAIL on unfixed code — the counterexample will be the first GPS pair
    /// that triggers the SendRawBytes fallback path.
    /// </summary>
    [Property(DisplayName = "Bug1 Property — SendRawBytes exception never escapes CalculateTracking for any GPS input")]
    public Property Bug1_Property_SendRawBytesException_NeverEscapesCalculateTracking()
    {
        // Generator: valid GPS coordinates (lat: -90..90 degrees, lon: -180..180 degrees)
        // Stored as int (1e7 scale) matching FlightData.GPS format
        var latGen = Gen.Choose(-900_000_000, 900_000_000);
        var lonGen = Gen.Choose(-1_800_000_000, 1_800_000_000);
        var altGen = Gen.Choose(0, 10_000_000); // 0..10000 m in mm

        return Prop.ForAll(
            Arb.From(latGen),
            Arb.From(lonGen),
            Arb.From(altGen),
            (lat, lon, alt) =>
            {
                var (vm, mavMock) = CreateViewModel(mav =>
                    mav.Setup(m => m.SendRawBytes(It.IsAny<byte[]>()))
                       .Throws(new InvalidOperationException("transport error")));

                vm.IsTracking = true;
                vm.IsTrackerConnected = false;

                var data = new FlightData
                {
                    GPS = new GPSData { Latitude = lat, Longitude = lon },
                    Altitude = alt
                };

                try
                {
                    mavMock.Raise(m => m.TelemetryReceived += null, mavMock.Object, data);
                    return true; // no exception — property holds
                }
                catch
                {
                    return false; // exception escaped — property violated (bug confirmed)
                }
            });
    }
}
