//MIT License
//
//Copyright(c) 2019 PHARTGAMES
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//
using SimFeedback.log;
using SimFeedback.telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Numerics;
using NoiseFilters;

namespace BNGTelemetry
{
    /// <summary>
    /// Beam NG Drive Telemetry Provider
    /// </summary>
    public sealed class BNGTelemetryProvider : AbstractTelemetryProvider
    {
        private bool isStopped = true;                                  // flag to control the polling thread
        private Thread t;
        int PORTNUM = 4444;
        private IPEndPoint _senderIP;                   // IP address of the sender for the udp connection used by the worker thread


        /// <summary>
        /// Default constructor.
        /// Every TelemetryProvider needs a default constructor for dynamic loading.
        /// Make sure to call the underlying abstract class in the constructor.
        /// </summary>
        public BNGTelemetryProvider() : base()
        {
            Author = "PEZZALUCIFER";
            Version = "v1.0";
            BannerImage = @"img\banner_bng.png"; // Image shown on top of the profiles tab
            IconImage = @"img\bng.jpg";  // Icon used in the tree view for the profile
            TelemetryUpdateFrequency = 100;     // the update frequency in samples per second
        }

        /// <summary>
        /// Name of this TelemetryProvider.
        /// Used for dynamic loading and linking to the profile configuration.
        /// </summary>
        public override string Name { get { return "bng"; } }

        public override void Init(ILogger logger)
        {
            base.Init(logger);
            Log("Initializing BNGTelemetryProvider");
        }

        /// <summary>
        /// A list of all telemetry names of this provider.
        /// </summary>
        /// <returns>List of all telemetry names</returns>
        public override string[] GetValueList()
        {
            return GetValueListByReflection(typeof(BNGAPI));
        }

        /// <summary>
        /// Start the polling thread
        /// </summary>
        public override void Start()
        {
            if (isStopped)
            {
                LogDebug("Starting BNGTelemetryProvider");
                isStopped = false;
                t = new Thread(Run);
                t.Start();
            }
        }

        /// <summary>
        /// Stop the polling thread
        /// </summary>
        public override void Stop()
        {
            LogDebug("Stopping BNGTelemetryProvider");
            isStopped = true;
            if (t != null) t.Join();
        }

        /// <summary>
        /// The thread funktion to poll the telemetry data and send TelemetryUpdated events.
        /// </summary>
        private void Run()
        {
            BNGAPI lastTelemetryData = new BNGAPI();
            lastTelemetryData.Reset();
            Vector3 lastVelocity = Vector3.Zero;
            Session session = new Session();
            Stopwatch sw = new Stopwatch();
            sw.Start();

            KalmanFilter accXFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);
            KalmanFilter accYFilter = new KalmanFilter(1, 1, 0.05f, 1, 0.1f, 0.0f);
            KalmanFilter accZFilter = new KalmanFilter(1, 1, 0.05f, 1, 0.05f, 0.0f); //heave noisy af

            NoiseFilter accXSmooth = new NoiseFilter(6, 1.0f);
            NoiseFilter accYSmooth = new NoiseFilter(6, 0.5f);
            NoiseFilter accZSmooth = new NoiseFilter(6, 1.0f);

            KalmanFilter velXFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);
            KalmanFilter velYFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);

            NoiseFilter velXSmooth = new NoiseFilter(6, 0.5f);
            NoiseFilter velYSmooth = new NoiseFilter(6, 0.5f);

            KalmanFilter yawRateFilter = new KalmanFilter(1, 1, 0.02f, 1, 0.02f, 0.0f);
            NoiseFilter yawRateSmooth = new NoiseFilter(6, 0.5f);

            NoiseFilter pitchFilter = new NoiseFilter(3);
            NoiseFilter rollFilter = new NoiseFilter(3);
            NoiseFilter yawFilter = new NoiseFilter(3);

            NoiseFilter slipAngleSmooth = new NoiseFilter(6, 0.25f);


            UdpClient socket = new UdpClient();
            socket.ExclusiveAddressUse = false;
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, PORTNUM));

            Log("Listener started (port: " + PORTNUM.ToString() + ") BNGTelemetryProvider.Thread");
            while (!isStopped)
            {
                try
                {
                    // get data from game, 
                    if (socket.Available == 0)
                    {
                        if (sw.ElapsedMilliseconds > 500)
                        {
                            IsRunning = false;
                            IsConnected = false;
                            Thread.Sleep(1000);
                        }
                        continue;
                    }
                    else
                    {
                        IsConnected = true;
                    }

                    Byte[] received = socket.Receive(ref _senderIP);


                    var alloc = GCHandle.Alloc(received, GCHandleType.Pinned);
                    BNGAPI telemetryData = (BNGAPI)Marshal.PtrToStructure(alloc.AddrOfPinnedObject(), typeof(BNGAPI));

                    // otherwise we are connected
                    IsConnected = true;

                    if (telemetryData.magic[0] == 'B' 
                     && telemetryData.magic[1] == 'N'
                     && telemetryData.magic[2] == 'G'
                     && telemetryData.magic[3] == '1')
                    {
                        IsRunning = true;

                        sw.Restart();

                        BNGAPI telemetryToSend = new BNGAPI();
                        telemetryToSend.Reset();

                        telemetryToSend.CopyFields(telemetryData);

                        telemetryToSend.accX = accXSmooth.Filter(accXFilter.Filter(telemetryData.accX));
                        telemetryToSend.accY = accYSmooth.Filter(accYFilter.Filter(telemetryData.accY));
                        telemetryToSend.accZ = accZSmooth.Filter(accZFilter.Filter(telemetryData.accZ));

                        telemetryToSend.pitchPos = pitchFilter.Filter(telemetryData.pitchPos);
                        telemetryToSend.rollPos = rollFilter.Filter(telemetryData.rollPos);
                        telemetryToSend.yawPos = yawFilter.Filter(telemetryData.yawPos);

                        telemetryToSend.velX = velXSmooth.Filter(velXFilter.Filter(telemetryData.velX));
                        telemetryToSend.velY = velYSmooth.Filter(velYFilter.Filter(telemetryData.velY));

                        telemetryToSend.yawRate = yawRateSmooth.Filter(yawRateFilter.Filter(telemetryData.yawRate));

                        telemetryToSend.yawAcc = slipAngleSmooth.Filter(telemetryToSend.CalculateSlipAngle());

                        TelemetryEventArgs args = new TelemetryEventArgs(
                            new BNGTelemetryInfo(telemetryToSend, lastTelemetryData));
                        RaiseEvent(OnTelemetryUpdate, args);

                        lastTelemetryData = telemetryData;
                    }
                    else if (sw.ElapsedMilliseconds > 500)
                    {
                        IsRunning = false;
                    }
                }
                catch (Exception e)
                {
                    LogError("BNGTelemetryProvider Exception while processing data", e);
                    IsConnected = false;
                    IsRunning = false;
                    Thread.Sleep(1000);
                }

            }

            socket.Close();
            IsConnected = false;
            IsRunning = false;
        }
    }
}
