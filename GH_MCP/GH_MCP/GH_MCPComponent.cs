using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GH_MCP.Commands;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino;
using Newtonsoft.Json;
using System.IO;

namespace GrasshopperMCP
{
    /// <summary>
    /// Grasshopper MCP component for communicating with Python server
    /// </summary>
    public class GrasshopperMCPComponent : GH_Component
    {
        private static TcpListener listener;
        private static bool isRunning = false;
        private static int grasshopperPort = 8080;
        
        /// <summary>
        /// Initialize a new instance of the GrasshopperMCPComponent class
        /// </summary>
        public GrasshopperMCPComponent()
            : base("Grasshopper MCP", "MCP", "Machine Control Protocol for Grasshopper", "Params", "Util")
        {
        }
        
        /// <summary>
        /// Register input parameters
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Enabled", "E", "Enable or disable the MCP server", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Port", "P", "Port to listen on", GH_ParamAccess.item, grasshopperPort);
        }
        
        /// <summary>
        /// Register output parameters
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Server status", GH_ParamAccess.item);
            pManager.AddTextParameter("LastCommand", "C", "Last received command", GH_ParamAccess.item);
        }
        
        /// <summary>
        /// Solve component instance
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool enabled = false;
            int port = grasshopperPort;
            
            // Get input parameters
            if (!DA.GetData(0, ref enabled)) return;
            if (!DA.GetData(1, ref port)) return;
            
            // Update port
            grasshopperPort = port;
            
            // Start or stop server based on enabled state
            if (enabled && !isRunning)
            {
                Start();
                DA.SetData(0, $"Running on port {grasshopperPort}");
            }
            else if (!enabled && isRunning)
            {
                Stop();
                DA.SetData(0, "Stopped");
            }
            else if (enabled && isRunning)
            {
                DA.SetData(0, $"Running on port {grasshopperPort}");
            }
            else
            {
                DA.SetData(0, "Stopped");
            }
            
            // Set last received command
            DA.SetData(1, LastCommand);
        }
        
        /// <summary>
        /// Component GUID
        /// </summary>
        public override Guid ComponentGuid => new Guid("12345678-1234-1234-1234-123456789012");
        
        /// <summary>
        /// Expose icon
        /// </summary>
        protected override Bitmap Icon => null;
        
        /// <summary>
        /// Last received command
        /// </summary>
        public static string LastCommand { get; private set; } = "None";
        
        /// <summary>
        /// Start MCP server
        /// </summary>
        public static void Start()
        {
            if (isRunning) return;
            
            // Initialize command registry
            GrasshopperCommandRegistry.Initialize();
            
            // Start TCP listener
            isRunning = true;
            listener = new TcpListener(IPAddress.Loopback, grasshopperPort);
            listener.Start();
            RhinoApp.WriteLine($"GrasshopperMCPBridge started on port {grasshopperPort}.");
            
            // Start accepting connections
            Task.Run(ListenerLoop);
        }
        
        /// <summary>
        /// Stop MCP server
        /// </summary>
        public static void Stop()
        {
            if (!isRunning) return;
            
            isRunning = false;
            listener.Stop();
            RhinoApp.WriteLine("GrasshopperMCPBridge stopped.");
        }
        
        /// <summary>
        /// Listener loop to handle incoming connections
        /// </summary>
        private static async Task ListenerLoop()
        {
            try
            {
                while (isRunning)
                {
                    // Wait for client connection
                    var client = await listener.AcceptTcpClientAsync();
                    RhinoApp.WriteLine("GrasshopperMCPBridge: Client connected.");
                    
                    // Handle client connection
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    RhinoApp.WriteLine($"GrasshopperMCPBridge error: {ex.Message}");
                    isRunning = false;
                }
            }
        }
        
        /// <summary>
        /// Handle client connection
        /// </summary>
        /// <param name="client">TCP client</param>
        private static async Task HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    // Read command
                    string commandJson = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(commandJson))
                    {
                        return;
                    }
                    
                    // Update last received command
                    LastCommand = commandJson;
                    
                    // Parse command
                    Command command = JsonConvert.DeserializeObject<Command>(commandJson);
                    RhinoApp.WriteLine($"GrasshopperMCPBridge: Received command: {command.Type}");
                    
                    // Execute command
                    Response response = GrasshopperCommandRegistry.ExecuteCommand(command);
                    
                    // Send response
                    string responseJson = JsonConvert.SerializeObject(response);
                    await writer.WriteLineAsync(responseJson);
                    
                    RhinoApp.WriteLine($"GrasshopperMCPBridge: Command {command.Type} executed with result: {(response.Success ? "Success" : "Error")}");
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"GrasshopperMCPBridge error handling client: {ex.Message}");
                    
                    // Send error response
                    Response errorResponse = Response.CreateError($"Server error: {ex.Message}");
                    string errorResponseJson = JsonConvert.SerializeObject(errorResponse);
                    await writer.WriteLineAsync(errorResponseJson);
                }
            }
        }
    }
}
