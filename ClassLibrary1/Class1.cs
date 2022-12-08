using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Device.Location;

namespace ClassLibrary1
{
    public class MySystemLocationProgram
    {


        static GeoCoordinateWatcher watcher = new GeoCoordinateWatcher();
        private static int delay = 60000;
        public GeoLocationData location { get; set; }

       

        static void Main(string[] args)
        {

            MySystemLocationProgram pr = new MySystemLocationProgram(10000);

            pr.GetLocationProperty();
        }
        public MySystemLocationProgram(int de)
        {
            delay = de;
            watcher.TryStart(false, TimeSpan.FromMilliseconds(delay));

        }




        public double getLongitude()
        {
            GeoCoordinate coord = watcher.Position.Location;
            return coord.Longitude;
        }
        public double getLatitude()
        {
            GeoCoordinate coord = watcher.Position.Location;
            return coord.Latitude;
        }
        void GetLocationProperty()
        {

            // Do not suppress prompt, and wait 1000 milliseconds to start.

            while (true)
            {
                GeoCoordinate coord = watcher.Position.Location;

                if (coord.IsUnknown != true)
                {
                    Console.WriteLine("Lat: {0}, Long: {1}",
                        coord.Latitude,
                        coord.Longitude);
                    location = new GeoLocationData
                    {
                        lat = coord.Latitude,
                        lon = coord.Longitude,
                        alt = 0.0
                    };

                }
                else
                {
                    Console.WriteLine("Unknown latitude and longitude.");
                }
                System.Threading.Thread.Sleep(delay);
            }
        }

    }
    public class GeoLocationData
    {
        public double lat { get; set; }
        public double lon { get; set; }
        public double alt { get; set; }

    }

}
