using System;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using Intel.RealSense;
using System.Threading.Tasks;
using RealSenseServer;
using SharpDX.WIC;

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
        public static RealSenseHandler instance;
        public Pipeline pipeline;

        // public FrameQueue depthQueue = new FrameQueue(Q_CAPACITY);
        public byte[] depthByteBuffer = new byte[RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2];
        public List<AutoResetEvent> depthFrameReady = new List<AutoResetEvent>();

        private byte[] rgbByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 3];

        public byte[] yuvByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 2];
        public List<AutoResetEvent> yuvFrameReady = new List<AutoResetEvent>();
        public byte[] bgraByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 4];
        public List<AutoResetEvent> bgraFrameReady = new List<AutoResetEvent>();
        public byte[] jpegByteBuffer = new byte[RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 4];
        public List<AutoResetEvent> jpegFrameReady = new List<AutoResetEvent>();
        public int nJpegBytes = 0;

        ImagingFactory imagingFactory = new ImagingFactory();

        void HandleDepthFrame(DepthFrame depth)
        {
            depth.CopyTo<byte>(depthByteBuffer);
            lock (depthFrameReady)
                foreach (var autoResetEvent in depthFrameReady)
                    autoResetEvent.Set();
        }

        void HandleColorFrame(VideoFrame color)
        {
            color.CopyTo<byte>(rgbByteBuffer);

            // BGRA
            bgraByteBuffer = ConvertRGB24.ToBGRA(rgbByteBuffer, color.Width, color.Height, 255);
            lock (bgraFrameReady)
                foreach (var autoResetEvent in bgraFrameReady)
                    autoResetEvent.Set();

            // YUV
            yuvByteBuffer = ConvertRGB24.ToYUY2(rgbByteBuffer, color.Width, color.Height);
            lock (yuvFrameReady)
                foreach (var autoResetEvent in yuvFrameReady)
                    autoResetEvent.Set();

            // JPEG
            var bitmapSource = new Bitmap(imagingFactory, RealSenseCalibration.colorImageWidth, RealSenseCalibration.colorImageHeight, SharpDX.WIC.PixelFormat.Format32bppBGR, BitmapCreateCacheOption.CacheOnLoad);
            var bitmapLock = bitmapSource.Lock(BitmapLockFlags.Write);
            Marshal.Copy(bgraByteBuffer, 0, bitmapLock.Data.DataPointer, RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 4);
            bitmapLock.Dispose();

            var memoryStream = new MemoryStream();

            var stream = new WICStream(imagingFactory, memoryStream);

            var jpegBitmapEncoder = new JpegBitmapEncoder(imagingFactory);
            jpegBitmapEncoder.Initialize(stream);

            var bitmapFrameEncode = new BitmapFrameEncode(jpegBitmapEncoder);
            bitmapFrameEncode.Options.ImageQuality = 0.5f;
            bitmapFrameEncode.Initialize();
            bitmapFrameEncode.SetSize(RealSenseCalibration.colorImageWidth, RealSenseCalibration.colorImageHeight);
            var pixelFormatGuid = PixelFormat.FormatDontCare;
            bitmapFrameEncode.SetPixelFormat(ref pixelFormatGuid);
            bitmapFrameEncode.WriteSource(bitmapSource);

            bitmapFrameEncode.Commit();
            jpegBitmapEncoder.Commit();

            lock (jpegByteBuffer)
            {
                nJpegBytes = (int)memoryStream.Length;
                memoryStream.Seek(0, SeekOrigin.Begin);
                memoryStream.Read(jpegByteBuffer, 0, nJpegBytes);
            }
            lock (jpegFrameReady)
                foreach (var autoResetEvent in jpegFrameReady)
                    autoResetEvent.Set();

            bitmapSource.Dispose();
            memoryStream.Close();
            memoryStream.Dispose();
            stream.Dispose();
            jpegBitmapEncoder.Dispose();
            bitmapFrameEncode.Dispose();
        }

        public RealSenseHandler()
        {
            instance = this;
            pipeline = new Pipeline();
            
            using (var ctx = new Context())
            {
                var devices = ctx.QueryDevices();
                var dev = devices[0];

                Console.WriteLine("\nUsing device 0, an {0}", dev.Info[CameraInfo.Name]);
                Console.WriteLine("    Serial number: {0}", dev.Info[CameraInfo.SerialNumber]);
                Console.WriteLine("    Firmware version: {0}", dev.Info[CameraInfo.FirmwareVersion]);

                var cfg = new Config();
                cfg.EnableStream(Intel.RealSense.Stream.Depth, RealSenseCalibration.depthImageWidth, RealSenseCalibration.depthImageHeight, Format.Z16, 5);
                cfg.EnableStream(Intel.RealSense.Stream.Color, RealSenseCalibration.colorImageWidth, RealSenseCalibration.colorImageHeight, Format.Rgb8, 5);

                pipeline.Start(cfg);
            }

            var frameQueue = new FrameQueue(5); // allow max latency of 5 frames

            Task.Run(() =>
            {
                while (true)
                {
                    DepthFrame frame;
                    if (frameQueue.PollForFrame(out frame))
                    {
                        using (frame)
                        {
                            if (frame.DataSize == depthByteBuffer.Length)
                            {
                                Console.WriteLine("depth frame");
                                HandleDepthFrame(frame);
                            }
                            else
                            {
                                Console.WriteLine("color frame");
                                HandleColorFrame(frame);
                            }
                        }
                    }
                }
            });

            while (true)
            {
                using (var frames = pipeline.WaitForFrames()) 
                {
                    using (var depth = frames.DepthFrame)
                        frameQueue.Enqueue(depth);
                    using (var color = frames.ColorFrame)
                        frameQueue.Enqueue(color);
                }
                
            }
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
        AutoResetEvent rgbFrameReady = new AutoResetEvent(false);
        AutoResetEvent yuvFrameReady = new AutoResetEvent(false);
        AutoResetEvent jpegFrameReady = new AutoResetEvent(false);


        public RealSenseServer()
        {
            lock (RealSenseHandler.instance.depthFrameReady)
                RealSenseHandler.instance.depthFrameReady.Add(depthFrameReady);
            lock (RealSenseHandler.instance.bgraFrameReady)
                RealSenseHandler.instance.bgraFrameReady.Add(rgbFrameReady);
            lock (RealSenseHandler.instance.yuvFrameReady)
                RealSenseHandler.instance.yuvFrameReady.Add(yuvFrameReady);
            lock (RealSenseHandler.instance.jpegFrameReady)
                RealSenseHandler.instance.jpegFrameReady.Add(jpegFrameReady);
        }

        // Returns immediately if a frame has been made available since the last time this was called on this client;
        // otherwise blocks until one is available.
        [OperationContract]
        public byte[] LatestDepthImage()
        {
            depthFrameReady.WaitOne();
            Console.WriteLine("[LatestDepthImage]");
            // Is this copy really necessary?:
            lock (RealSenseHandler.instance.depthByteBuffer)
                Buffer.BlockCopy(RealSenseHandler.instance.depthByteBuffer, 0, depthByteBuffer, 0, RealSenseCalibration.depthImageWidth * RealSenseCalibration.depthImageHeight * 2);
            return depthByteBuffer;
        }

        [OperationContract]
        public byte[] LatestYUVImage()
        {
            yuvFrameReady.WaitOne();
            Console.WriteLine("[LatestYUVImage]");
            lock (RealSenseHandler.instance.yuvByteBuffer)
                Buffer.BlockCopy(RealSenseHandler.instance.yuvByteBuffer, 0, yuvByteBuffer, 0, RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 2);
            return yuvByteBuffer;
        }

        [OperationContract]
        public byte[] LatestRGBImage()
        {
            rgbFrameReady.WaitOne();
            Console.WriteLine("[LatestRGBImage]");
            lock (RealSenseHandler.instance.bgraByteBuffer)
                Buffer.BlockCopy(RealSenseHandler.instance.bgraByteBuffer, 0, rgbByteBuffer, 0, RealSenseCalibration.colorImageWidth * RealSenseCalibration.colorImageHeight * 4);
            return rgbByteBuffer;
        }

        [OperationContract]
        public byte[] LatestJPEGImage()
        {
            jpegFrameReady.WaitOne();
            Console.WriteLine("[LatestJPEGImage]");
            byte[] jpegByteBuffer;
            lock (RealSenseHandler.instance.jpegByteBuffer)
            {
                jpegByteBuffer = new byte[RealSenseHandler.instance.nJpegBytes];
                Buffer.BlockCopy(RealSenseHandler.instance.jpegByteBuffer, 0, jpegByteBuffer, 0, RealSenseHandler.instance.nJpegBytes);
            }
            return jpegByteBuffer;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            new RealSenseHandler();
            var serviceHost = new ServiceHost(typeof(KinectServer2));

            // discovery
            serviceHost.Description.Behaviors.Add(new ServiceDiscoveryBehavior());
            serviceHost.AddServiceEndpoint(new UdpDiscoveryEndpoint());

            serviceHost.Open();
            Console.ReadLine();
        }
    }
}