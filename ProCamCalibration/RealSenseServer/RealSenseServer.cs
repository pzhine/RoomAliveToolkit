using System;
using System.Diagnostics;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using Intel.RealSense;
using System.Collections;
using System.Threading.Tasks;

namespace RoomAliveToolkit
{
    public class RealSenseCalibration
    {
        public const int depthImageWidth = 848;
        public const int depthImageHeight = 480;
        public const int colorImageWidth = 1280;
        public const int colorImageHeight = 720;
    }

    public class RealSenseHandler
    {
        const int Q_CAPACITY = 5; 

        public static RealSenseHandler instance;
        public Pipeline pipe;

        public FrameQueue depthQueue = new FrameQueue(Q_CAPACITY);
        public ushort[] depthShortBuffer = new ushort[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight];
        public byte[] depthByteBuffer = new byte[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2];

        /*
        public FrameQueue colorQueue = new FrameQueue(Q_CAPACITY);
        public ushort[] colorShortBuffer = new ushort[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight];
        public byte[] colorByteBuffer = new byte[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2];
        */

        public List<AutoResetEvent> depthFrameReady = new List<AutoResetEvent>();

        public RealSenseHandler()
        {
            instance = this;
            pipe = new Pipeline();
            pipe.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    using (var frames = pipe.WaitForFrames())
                    {
                        using (var depth = frames.DepthFrame)
                        {
                            depth.CopyTo<ushort>(depthShortBuffer);
                            lock (depthFrameReady)
                                foreach (var autoResetEvent in depthFrameReady)
                                    autoResetEvent.Set();
                        }

                        using (var color = frames.ColorFrame)
                        {
                            //color.

                        }
                                            }
                }
            });
        }
    }

    /// <summary>
    /// Created on each session.
    /// </summary>
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple)]
    [ServiceContract]
    public class RealSenseServer
    {
        byte[] depthByteBuffer = new byte[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2];
        byte[] yuvByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 2];
        byte[] rgbByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 4];

        AutoResetEvent depthFrameReady = new AutoResetEvent(false);

        public RealSenseServer()
        {
            lock (RealSenseHandler.instance.depthFrameReady)
                RealSenseHandler.instance.depthFrameReady.Add(depthFrameReady);
        }

        // Returns immediately if a frame has been made available since the last time this was called on this client;
        // otherwise blocks until one is available.
        [OperationContract]
        public byte[] LatestDepthImage()
        {
            depthFrameReady.WaitOne();
            Console.WriteLine("depth frame");
            // Is this copy really necessary?:
            lock (RealSenseHandler.instance.depthShortBuffer)
                Buffer.BlockCopy(RealSenseHandler.instance.depthShortBuffer, 0, depthByteBuffer, 0, RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2);
            return depthByteBuffer;
        }

        [OperationContract]
        public byte[] LatestYUVImage()
        {
            return new byte[] { };
        }

        [OperationContract]
        public byte[] LatestRGBImage()
        {
            return new byte[] { };
        }

        [OperationContract]
        public byte[] LatestJPEGImage()
        {
            return new byte[] { };
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            new RealSenseHandler();
            var serviceHost = new ServiceHost(typeof(RealSenseServer));

            // discovery
            serviceHost.Description.Behaviors.Add(new ServiceDiscoveryBehavior());
            serviceHost.AddServiceEndpoint(new UdpDiscoveryEndpoint());

            serviceHost.Open();
            Console.ReadLine();
        }
    }
}