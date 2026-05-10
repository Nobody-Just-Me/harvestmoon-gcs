## Plan Generated: pigeon-uno-migration

**Key Decisions Made:**
- **Framework**: Uno Platform (WinUI 3) targeting 100% parity with WPF.
- **Tracker**: Feature retained. New View/ViewModel to be created. Logic extracted to testable Service.
- **Speech**: `ISpeechService` interface wrapping `Windows.Media.SpeechSynthesis` for MVP.

**Scope:**
- **IN**: Foundation (MavLink Serial/UDP, Tests), FlightPage UI, TrackerPage UI, Tracker Logic, Speech Service.
- **OUT**: MapPage/Mission (Deferred to later phase), Advanced AI (YOLO/Vegetation deferred).

**Guardrails Applied:**
- Use Mapsui instead of GMap.NET.
- Use `ISpeechService` interface (no direct native calls in VMs).
- **Test Mandate**: TDD required for all new logic (Tracker math, MavLink parsing).

**Auto-Resolved:**
- **Test Framework**: xUnit (Standard, no existing infra found).
- **MavLink Upgrade**: Add Serial/UDP support to match WPF `MavLinkSerialPortTransport.cs`.

**Defaults Applied:**
- **Navigation**: WinUI `NavigationView` structure (implied by existing app).
- **DI**: `Microsoft.Extensions.DependencyInjection` (standard Uno template).

**Decisions Needed:**
- None. All critical path questions answered.

Plan saved to: `.sisyphus/plans/pigeon-uno-migration.md`
