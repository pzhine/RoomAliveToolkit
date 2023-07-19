using System;
using Intel.RealSense;

namespace RealSenseServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Context ctx = new Context();
            var list = ctx.QueryDevices();
            if (list.Count == 0)
            {
                throw new Exception("No RealSense devices detected");
            }
            Device dev = list[0];
            Console.WriteLine("RealSense device: " + dev.ToString());
            Console.ReadLine();
        }
    }
}
