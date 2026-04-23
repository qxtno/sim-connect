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

class Program
{
    static SimConnect simconnect;
    static UdpClient udp;
    static DateTime lastSendTime = DateTime.MinValue;

    static JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    enum DEFINES { FLIGHT_DATA = 0 }
    enum REQUESTS { FLIGHT_DATA = 0 }

    static void Main()
    {
        udp = new UdpClient();
        udp.Connect("127.0.0.1", 5005);

        try
        {
            simconnect = new SimConnect("UDP Exporter", IntPtr.Zero, 0, null, 0);
            simconnect.OnRecvSimobjectData += OnData;

            // Define Data Mapping
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

            simconnect.RequestDataOnSimObject(REQUESTS.FLIGHT_DATA, DEFINES.FLIGHT_DATA, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

            Console.Clear();
            Console.WriteLine("=========================================");
            Console.WriteLine("         UDP DASHBOARD - RUNNING         ");
            Console.WriteLine("=========================================");

            while (true)
            {
                simconnect.ReceiveMessage();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connection Error: " + ex.Message);
        }
    }

    static void OnData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        // Throttle to 20 updates per second (50ms)
        if ((DateTime.Now - lastSendTime).TotalMilliseconds < 50) return;
        lastSendTime = DateTime.Now;

        try
        {
            FlightData fd = (FlightData)data.dwData[0];

            // Safety check for valid numbers
            double S(double val) => double.IsFinite(val) ? val : 0.0;

            var payload = new
            {
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

            Console.SetCursorPosition(0, 4);
            Console.WriteLine($"ALT: {payload.telemetry.alt,-10} SPD: {payload.telemetry.spd,-10}      ");
            Console.WriteLine($"HDG: {payload.telemetry.hdg,-10} VSI: {payload.telemetry.vsi,-10}      ");
            Console.WriteLine($"PIT: {payload.telemetry.pitch,-10} BNK: {payload.telemetry.bank,-10}      ");
            Console.WriteLine($"FUEL TOTAL: {payload.fuel.total_gal,-10} gal                    ");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"L: {payload.fuel.left.qty}/{payload.fuel.left.cap}  R: {payload.fuel.right.qty}/{payload.fuel.right.cap}      ");
            Console.WriteLine($"C: {payload.fuel.center.qty}/{payload.fuel.center.cap}                          ");
        }
        catch
        {
            // Silent catch for frames during sim loading/transitions
        }
    }
}