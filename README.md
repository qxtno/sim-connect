# MSFS UDP Exporter

A utility to export real-time flight data from Microsoft Flight Simulator to a UDP stream (JSON format) for external dashboards and telemetry apps.

## Features
- **SimConnect Integration:** Pulls live aircraft variables directly from MSFS.
- **UDP Streaming:** Broadcasts data to `127.0.0.1:5005`.
- **JSON Format:** Structured data for easy consumption by external scripts.
- **Console HUD:** Real-time summary display in the terminal.

## Commands

### Run Development
```bash
dotnet run
```

### Build Release
To create a standalone, self-contained executable for Windows x64:
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

### Data Points
- Telemetry: Altitude, Airspeed, Vertical Speed, Heading, Pitch, Bank.
- Fuel: Total gallons, plus Quantity/Capacity for Left, Right, and Center tanks.

### Technical Notes
- Update Rate: 20Hz (50ms intervals).
- Protocol: UDP over Localhost (127.0.0.1) on Port 5005.
- Dependency: Requires Microsoft.FlightSimulator.SimConnect.