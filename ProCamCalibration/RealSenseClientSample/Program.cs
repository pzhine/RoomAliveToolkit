using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RealSenseClientSample
{
    public class Camera
    {
        public string name;
        public string hostNameOrAddress;

        public RealSenseServerClient Client
        {
            get
            {
                if ((client == null) || (client.InnerChannel.State != CommunicationState.Opened))
                {
                    var binding = new NetTcpBinding();
                    binding.MaxReceivedMessageSize = 8295424;
                    binding.Security.Mode = SecurityMode.None;
                    var uri = "net.tcp://" + hostNameOrAddress + ":9000/RealSenseServer/service";
                    var address = new EndpointAddress(uri);
                    client = new RealSenseServerClient(binding, address);
                    try
                    {
                        client.Open();
                    }
                    catch (EndpointNotFoundException e)
                    {
                        client = null;
                        Console.WriteLine("could not connect to Kinect server '{0}' at '{1}'", name, hostNameOrAddress);
                        throw e;
                    }
                }
                return client;
            }
        }
        private RealSenseServerClient client;
    }

    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Camera camera = new Camera()
            {
                hostNameOrAddress = "localhost",
                name = "realsense"
            };
            byte[] depth = camera.Client.LatestDepthImage();
            Console.WriteLine("depth: " + depth.Length);
            Console.ReadLine();
            /*
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            */
        }
    }
}
