// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

using System.Diagnostics;
using DotNetty.Common.Utilities;

namespace IOT_CENTRAL_LOCATION
{
    public class Worker : BackgroundService
    {   
        
        // Environmental variables for Dps
        private const string scopeId = "0ne00847F2F";
        private const string globalEndpoint = "global.azure-devices-provisioning.net";
        private const string primaryKey = "18HDoM8vXem3DCB6J7TEL+gx/BPigYV5QmaOJVfH5jffU3C2LJlEVS4w1Id3/veIfYRZJWecZPDUHSJ3B4R7ew==";
        private const string ModelId = "dtmi:newiotcentralfile:ok_31b;1";



        private readonly ILogger<Worker> _logger;
        private static string deviceId = "";
        private static string derivedKey = "";
        private DeviceClient deviceClient = null;

        private static int delay = 6000;


        private static const string pw ="Add-Type -AssemblyName System.Device #Required to access System.Device.Location namespace\n"
    		+ "$GeoWatcher = New-Object System.Device.Location.GeoCoordinateWatcher #Create the required object\n"
    		+ "$GeoWatcher.Start() #Begin resolving current locaton\n"
    		+ "\n"
    		+ "while (($GeoWatcher.Status -ne 'Ready') -and ($GeoWatcher.Permission -ne 'Denied')) {\n"
    		+ "    Start-Sleep -Milliseconds 100 #Wait for discovery.\n"
    		+ "}  \n"
    		+ "\n"
    		+ "if ($GeoWatcher.Permission -eq 'Denied'){\n"
    		+ "    Write-Error 'Access Denied for Location Information'\n"
    		+ "} else {\n"
    		+ "    $GeoWatcher.Position.Location | Select Latitude,Longitude #Select the relevent results.\n"
    		+ "}";



        /// <summary>
        /// The content type for a plug and play compatible telemetry message.
        /// </summary>
        public const string ContentApplicationJson = "application/json";



        public Worker(ILogger<Worker> logger)
        {

            

            deviceId = System.Net.Dns.GetHostName();
            derivedKey = ComputeDerivedSymmetricKey(primaryKey, deviceId);

            _logger = logger;


            using var cts = new CancellationTokenSource();







            try
            {
                deviceClient =  currentDeviceClientAsync( logger, cts.Token).Result;
                // var sample = new TemperatureControllerSample(deviceClient, logger);

                // await sample.PerformOperationsAsync(cts.Token);

                // PerformOperationsAsync is designed to run until cancellation has been explicitly requested, either through
                // cancellation token expiration or by Console.CancelKeyPress.
                // As a result, by the time the control reaches the call to close the device client, the cancellation token source would
                // have already had cancellation requested.
                // Hence, if you want to pass a cancellation token to any subsequent calls, a new token needs to be generated.
                // For device client APIs, you can also call them without a cancellation token, which will set a default
                // cancellation timeout of 4 minutes: https://github.com/Azure/azure-iot-sdk-csharp/blob/64f6e9f24371bc40ab3ec7a8b8accbfb537f0fe1/iothub/device/src/InternalClient.cs#L1922
                
                // await deviceClient.CloseAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ProvisioningTransportException)
            {
                // User canceled the operation. Nothing to do here.
            }
        }

        private static async Task<DeviceClient> currentDeviceClientAsync(ILogger logger, CancellationToken cancellationToken) {
            DeviceClient deviceClient = await SetupDeviceClientAsync(logger, cancellationToken);
            return deviceClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (deviceClient != null)
                {
                
                        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                        


                        using Message msg = CreateMessage();
                        await deviceClient.SendEventAsync(msg, stoppingToken);
                }
                await Task.Delay(delay, stoppingToken);

                _logger.LogInformation("Pwoersell value = {}", runScriot(pw));


            }
        }


        /// <summary>
        /// Compute a symmetric key for the provisioned device from the enrollment group symmetric key used in attestation.
        /// </summary>
        /// <param name="enrollmentKey">Enrollment group symmetric key.</param>
        /// <param name="registrationId">The registration Id of the key to create.</param>
        /// <returns>The key for the specified device Id registration in the enrollment group.</returns>
        /// <seealso>
        /// https://docs.microsoft.com/en-us/azure/iot-edge/how-to-auto-provision-symmetric-keys?view=iotedge-2018-06#derive-a-device-key
        /// </seealso>
        private static string ComputeDerivedSymmetricKey(string enrollmentKey, string registrationId)
        {
            if (string.IsNullOrWhiteSpace(enrollmentKey))
            {
                return enrollmentKey;
            }

            using var hmac = new HMACSHA256(Convert.FromBase64String(enrollmentKey));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(registrationId)));
        }





        private static async Task<DeviceClient> SetupDeviceClientAsync( ILogger logger, CancellationToken cancellationToken)
        {
            DeviceClient deviceClient;
                logger.LogDebug($"Initializing via DPS");
                    DeviceRegistrationResult dpsRegistrationResult = await ProvisionDeviceAsync(cancellationToken);
                    var authMethod = new DeviceAuthenticationWithRegistrySymmetricKey(dpsRegistrationResult.DeviceId, derivedKey);
                    deviceClient = InitializeDeviceClient(dpsRegistrationResult.AssignedHub, authMethod);
                    
            return deviceClient;
        }

        // Provision a device via DPS, by sending the PnP model Id as DPS payload.
        private static async Task<DeviceRegistrationResult> ProvisionDeviceAsync( CancellationToken cancellationToken)
        {
            using SecurityProvider symmetricKeyProvider = new SecurityProviderSymmetricKey(deviceId, derivedKey, null);
            using ProvisioningTransportHandler mqttTransportHandler = new ProvisioningTransportHandlerMqtt();
            ProvisioningDeviceClient pdc = ProvisioningDeviceClient.Create(globalEndpoint, scopeId, symmetricKeyProvider, mqttTransportHandler);

            var pnpPayload = new ProvisioningRegistrationAdditionalData
            {
                JsonData = PnpConvention.CreateDpsPayload(ModelId),
            };
            return await pdc.RegisterAsync(pnpPayload, cancellationToken);
        }

        // Initialize the device client instance using connection string based authentication, over Mqtt protocol (TCP, with fallback over Websocket) and
        // setting the ModelId into ClientOptions.
        private static DeviceClient InitializeDeviceClient(string deviceConnectionString)
        {
            var options = new ClientOptions
            {
                ModelId = ModelId,
            };

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt, options);

            return deviceClient;
        }

        // Initialize the device client instance using symmetric key based authentication, over Mqtt protocol (TCP, with fallback over Websocket)
        // and setting the ModelId into ClientOptions.
        private static DeviceClient InitializeDeviceClient(string hostname, IAuthenticationMethod authenticationMethod)
        {
            var options = new ClientOptions
            {
                ModelId = ModelId,
            };

            DeviceClient deviceClient = DeviceClient.Create(hostname, authenticationMethod, TransportType.Mqtt, options);

            return deviceClient;
        }




        /// <summary>
        /// Create a plug and play compatible telemetry message.
        /// </summary>
        /// <param name="componentName">The name of the component in which the telemetry is defined. Can be null for telemetry defined under the root interface.</param>
        /// <param name="telemetryPairs">The unserialized name and value telemetry pairs, as defined in the DTDL interface. Names must be 64 characters or less. For more details see
        /// <see href="https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md#telemetry"/>.</param>
        /// <param name="encoding">The character encoding to be used when encoding the message body to bytes. This defaults to utf-8.</param>
        /// <returns>A plug and play compatible telemetry message, which can be sent to IoT Hub. The caller must dispose this object when finished.</returns>
        public static Message CreateMessage()
        {

            const string telemetryName = "Tracking";
            GeoLocationData location = new GeoLocationData {
                lat = 23.676,
                lon = 78.454,
                alt = 0.0
            };
            
            //location.

            IDictionary<string, object> telemetryPairs = new Dictionary<string, object> { { telemetryName, location } };



            Encoding messageEncoding = Encoding.UTF8;
            string payload = JsonConvert.SerializeObject(telemetryPairs);
            var message = new Message(messageEncoding.GetBytes(payload))
            {
                ContentEncoding = messageEncoding.WebName,
                ContentType = ContentApplicationJson,
            };

 

            return message;
        }

        public class GeoLocationData
        {
            public double lat { get; set; }
            public double lon { get; set; }
            public double alt { get; set; }

        }


        public static string runScriot(string script) {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                 script)
                {
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();

            var reader = process.StandardOutput;
            return reader.ReadToEnd();
        }
        

    }
}