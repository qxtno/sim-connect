using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct FlightData
{
    public double Altitude;
    public double Airspeed;
    public double VSpeed;
    public double Heading;
    public double Pitch;
    public double Bank;
    public double FuelTotalQty;
    public double FuelLeftQty;
    public double FuelLeftCap;
    public double FuelRightQty;
    public double FuelRightCap;
    public double FuelCenterQty;
    public double FuelCenterCap;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct PlaneInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string ATCModel;
}

class Program
{
    static SimConnect simconnect;
    static UdpClient udp;
    static DateTime lastSendTime = DateTime.MinValue;
    static DateTime lastModelRequest = DateTime.MinValue;
    static string currentModel = "LOADING...";

    static JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    enum DEFINES { FLIGHT_DATA = 0, PLANE_INFO = 1 }
    enum REQUESTS { FLIGHT_DATA = 0, PLANE_INFO = 1 }
    enum EVENTS { AIRCRAFT_LOADED = 0 }

    static void Main()
    {
        udp = new UdpClient();
        udp.Connect("127.0.0.1", 5005);

        try
        {
            simconnect = new SimConnect("UDP Exporter", IntPtr.Zero, 0, null, 0);

            simconnect.OnRecvSimobjectData += OnData;
            simconnect.OnRecvEvent += OnEvent;

            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "VERTICAL SPEED", "feet per minute", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "PLANE HEADING DEGREES MAGNETIC", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL TOTAL QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL LEFT QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL LEFT CAPACITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL RIGHT QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL RIGHT CAPACITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL CENTER QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINES.FLIGHT_DATA, "FUEL CENTER CAPACITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.RegisterDataDefineStruct<FlightData>(DEFINES.FLIGHT_DATA);

            simconnect.AddToDataDefinition(DEFINES.PLANE_INFO, "ATC MODEL", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
            simconnect.RegisterDataDefineStruct<PlaneInfo>(DEFINES.PLANE_INFO);

            // Stick to AircraftLoaded - it is the "cleanest" way.
            simconnect.SubscribeToSystemEvent(EVENTS.AIRCRAFT_LOADED, "AircraftLoaded");

            simconnect.RequestDataOnSimObject(REQUESTS.FLIGHT_DATA, DEFINES.FLIGHT_DATA, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

            // Get initial name
            simconnect.RequestDataOnSimObject(REQUESTS.PLANE_INFO, DEFINES.PLANE_INFO, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

            Console.Clear();
            Console.WriteLine("=========================================");
            Console.WriteLine("         UDP DASHBOARD - RUNNING         ");
            Console.WriteLine("=========================================");

            while (true)
            {
                simconnect.ReceiveMessage();

                // Fallback: If for some reason the event fails, check every 10 seconds.
                // This is much lighter than PositionChanged.
                if ((DateTime.Now - lastModelRequest).TotalSeconds > 10)
                {
                    simconnect.RequestDataOnSimObject(REQUESTS.PLANE_INFO, DEFINES.PLANE_INFO, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
                    lastModelRequest = DateTime.Now;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connection Error: " + ex.Message);
        }
    }

    static void OnEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        if (data.uEventID == (uint)EVENTS.AIRCRAFT_LOADED)
        {
            simconnect.RequestDataOnSimObject(REQUESTS.PLANE_INFO, DEFINES.PLANE_INFO, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        }
    }

    static void OnData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)REQUESTS.PLANE_INFO)
        {
            PlaneInfo pi = (PlaneInfo)data.dwData[0];

            string raw = pi.ATCModel.Replace("\0", "").Trim();

            string cleaned = raw;
            if (raw.Contains(" "))
            {
                string[] parts = raw.Split(' ');
                cleaned = parts[parts.Length - 1];
            }

            if (cleaned.Contains("."))
            {
                cleaned = cleaned.Split('.')[0];
            }

            if (currentModel != cleaned)
            {
                currentModel = cleaned.ToUpper();
            }
            return;
        }

        if (data.dwRequestID == (uint)REQUESTS.FLIGHT_DATA)
        {
            if ((DateTime.Now - lastSendTime).TotalMilliseconds < 50) return;
            lastSendTime = DateTime.Now;

            try
            {
                FlightData fd = (FlightData)data.dwData[0];
                double S(double val) => double.IsFinite(val) ? val : 0.0;

                var payload = new
                {
                    aircraft = currentModel,
                    telemetry = new
                    {
                        alt = Math.Round(S(fd.Altitude), 0),
                        spd = Math.Round(S(fd.Airspeed), 1),
                        vsi = Math.Round(S(fd.VSpeed), 0),
                        hdg = Math.Round(S(fd.Heading), 1),
                        pitch = Math.Round(S(fd.Pitch) * -1.0, 2),
                        bank = Math.Round(S(fd.Bank), 2)
                    },
                    fuel = new
                    {
                        total_gal = Math.Round(S(fd.FuelTotalQty), 1),
                        left = new { qty = Math.Round(S(fd.FuelLeftQty), 1), cap = Math.Round(S(fd.FuelLeftCap), 1) },
                        right = new { qty = Math.Round(S(fd.FuelRightQty), 1), cap = Math.Round(S(fd.FuelRightCap), 1) },
                        center = new { qty = Math.Round(S(fd.FuelCenterQty), 1), cap = Math.Round(S(fd.FuelCenterCap), 1) }
                    }
                };

                string json = JsonSerializer.Serialize(payload, jsonOptions);
                byte[] sendBuffer = Encoding.UTF8.GetBytes(json);
                udp.Send(sendBuffer, sendBuffer.Length);

                Console.SetCursorPosition(0, 3);
                Console.WriteLine($"AIRCRAFT: {currentModel,-30}");
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine($"ALT: {payload.telemetry.alt,-10} SPD: {payload.telemetry.spd,-10}      ");
                Console.WriteLine($"HDG: {payload.telemetry.hdg,-10} VSI: {payload.telemetry.vsi,-10}      ");
                Console.WriteLine($"PIT: {payload.telemetry.pitch,-10} BNK: {payload.telemetry.bank,-10}      ");
                Console.WriteLine($"FUEL TOTAL: {payload.fuel.total_gal,-10} gal                    ");
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine($"L: {payload.fuel.left.qty}/{payload.fuel.left.cap}  R: {payload.fuel.right.qty}/{payload.fuel.right.cap}      ");
                Console.WriteLine($"C: {payload.fuel.center.qty}/{payload.fuel.center.cap}                          ");
            }
            catch { }
        }
    }
}