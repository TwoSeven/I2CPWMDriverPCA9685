using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace TwoSeven.IoT
{

    /// <summary>
    /// A class to provide common functionality for interaction with inter-integrated circuit (I2C) connected devices.
    /// </summary>
    /// <remarks>
    /// Version 2015.12.26.0
    /// Developed by: Chris Leavitt.
    /// </remarks>
    class I2CDeviceUtilities
    {

        /// <summary>
        /// Stores the results collected during an I2C bus scan for devices.
        /// A sorted list of device addresses (keys) and results (values).
        /// </summary>
        private SortedList<Int32, String> _ScanResults;

        /// <summary>
        /// Scans all 7 bit addresses on the I2C bus for devices.
        /// </summary>
        /// <returns>
        /// A sorted list of device addresses (keys) and results (values).
        /// Possible results are as follows:
        ///   - No device found.
        ///   - Found - Responding. = A device exists at the address and is ready for communication.
        ///   - Found - No response. = A device exists at the address, but it did not respond to a communication attempt.
        ///   - Found - Already in use. = A device exists at the address, but it may already be in use by an existing connection.
        /// </returns>
        public SortedList<Int32, String> ScanForDevices()
        {

            // Run the initialization sequence synchronously.
            Task.Run(() => ScanForDevicesAsync()).Wait();

            return _ScanResults;

        }

        /// <summary>
        /// Scans all 7 bit addresses on the I2C bus for devices.
        /// </summary>
        /// <returns>
        /// An async task, which returns a sorted list of device addresses (keys) and results (values).
        /// Possible results are as follows:
        ///   - No device found.
        ///   - Found - Responding. = A device exists at the address and is ready for communication.
        ///   - Found - No response. = A device exists at the address, but it did not respond to a communication attempt.
        ///   - Found - Already in use. = A device exists at the address, but it may already be in use by an existing connection.
        /// </returns>
        public async Task<SortedList<Int32, String>> ScanForDevicesAsync()
        {

            _ScanResults = new System.Collections.Generic.SortedList<Int32, String>();
             
            for (Int32 I2CAddress = 0; I2CAddress <= 127; I2CAddress += 1)
            {

                // Get a selector string that will return all I2C controllers on the system.
                string QueryString = I2cDevice.GetDeviceSelector();

                // Find the I2C bus controller device for this system with the selector string.
                DeviceInformationCollection DevInfo = await DeviceInformation.FindAllAsync(QueryString);

                // Check that an I2C controller is present on the system.
                if (DevInfo.Count == 0)
                {
                    throw new Exception("No I2C controllers were found on the system");
                }

                // Configure the I2C bus connection settings.
                I2cConnectionSettings Settings = new I2cConnectionSettings(I2CAddress);
                Settings.SharingMode = I2cSharingMode.Exclusive;
                Settings.BusSpeed = I2cBusSpeed.FastMode;

                // Create an I2cDevice with our selected bus controller and I2C settings.
                using (I2cDevice I2CDeviceInterface = await I2cDevice.FromIdAsync(DevInfo[0].Id, Settings))
                {

                    // Check that the I2C module connection was successful.
                    if (I2CDeviceInterface == null)
                    {
                        // Device found, but already in use.
                        _ScanResults.Add(I2CAddress, "Found - Already in use.");

                    }
                    else {

                        try
                        {
                            // Attempt to communicate with the device.
                            byte[] ReadBuf = new byte[1];
                            I2CDeviceInterface.Read(ReadBuf);

                            if (ReadBuf != null)
                                // Device found and responding to connections.
                                _ScanResults.Add(I2CAddress, "Found - Responding.");
                            else
                                // Device found, but not responding.
                                _ScanResults.Add(I2CAddress, "Found - No response.");

                        }
                        catch
                        {
                            // Device not found at current address.
                            _ScanResults.Add(I2CAddress, "No device found.");
                        }

                    }

                }

            }

            return _ScanResults;

        }

    }

}