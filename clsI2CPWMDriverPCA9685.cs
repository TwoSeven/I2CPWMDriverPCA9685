using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace TwoSeven.IoT.ExpansionModule
{

    /// <summary>
    /// A class to provide interaction with the NXP PCA9685 integrated circuit or expansion modules that use it 
    /// such as the Adafruit 16-Channel 12-bit PWM/Servo Driver PCA9685 module.
    /// https://www.adafruit.com/product/815
    /// 
    /// The PCA9685 is an I2C-bus controlled 16-channel PWM/LED controller.  Each output has its
    /// own 12-bit resolution (4096 steps) fixed frequency individual PWM controller that operates
    /// at a programmable frequency from a typical of 24 Hz to 1526 Hz with a duty cycle that is
    /// adjustable from 0 % to 100 %.  All outputs are set to the same PWM frequency.
    /// </summary>
    /// <remarks>
    /// Version 2015.12.27.0
    /// Developed by: Chris Leavitt.
    /// </remarks>
    class I2CPWMDriverPCA9685
    {

        #region " Fields "

        /// <summary>
        /// The I2C device instance to be controlled. 
        /// </summary>
        private I2cDevice _I2CDeviceInterface;

        /// <summary>
        /// The default 7-bit I2C bus address of the device. 
        /// </summary>
        private const byte _DefaultI2CAddress = 0x40;

        /// <summary>
        /// The number of channels supported by the expansion module.
        /// </summary>
        private const Int32 _ChannelCount = 16;

        #endregion

        #region " Enumerations "

        /// <summary>
        /// The PWM output channels available on the controller module.
        /// </summary>
        public enum PwmChannel
        {
            C0 = 0,
            C1 = 1,
            C2 = 2,
            C3 = 3,
            C4 = 4,
            C5 = 5,
            C6 = 6,
            C7 = 7,
            C8 = 8,
            C9 = 9,
            C10 = 10,
            C11 = 11,
            C12 = 12,
            C13 = 13,
            C14 = 14,
            C15 = 15, 
            ALL = 61,
        }

        /// <summary>
        /// The registers available on the control module's integrated circuit.
        /// </summary>
        private enum Register
        {

            /// <summary>
            /// Mode register 1.  Stores global device configuration.
            /// </summary>
            MODE1 = 0x00, 

            /// <summary>
            /// Mode register 2.  Stores global device configuration.
            /// </summary>
            MODE2 = 0x01, 

            /// <summary>
            /// I2C-bus sub-address 1 register.
            /// </summary>
            SUBADR1 = 0x02, 

            /// <summary>
            /// I2C-bus sub-address 2 register.
            /// </summary>
            SUBADR2 = 0x03,

            /// <summary>
            /// I2C-bus sub-address 3 register.
            /// </summary>
            SUBADR3 = 0x04,

            /// <summary>
            /// LED "all call" I2C-bus address register.
            /// </summary>
            ALLCALLADR = 0x05, 

            // Base PWM/LED output channel control registers.

            /// <summary>
            /// LED0 On output and brightness control register 1 of 2.  Stores 8 least significant digits.
            /// </summary>
            LED0_ON_L = 0x06,  

            /// <summary>
            /// LED0 On output and brightness control register 2 of 2.  Stores 4 most significant digits.
            /// </summary>
            LED0_ON_H = 0x07,  

            /// <summary>
            /// LED0 Off output and brightness control register 1 of 2.  Stores 8 least significant digits.
            /// </summary>
            LED0_OFF_L = 0x08, 

            /// <summary>
            /// LED0 Off output and brightness control register 2 of 2.  Stores 4 most significant digits.
            /// </summary>
            LED0_OFF_H = 0x09, 

            // The remaining output channels use registers 0x0A to 0x45.
            // Rather than defining the remaining channel control registers individually, all other output 
            //   channels will be controlled using multiples of the base channel registers above.
            // Formula = <LED0_Register> + 4 * <ChannelNumber>
            // Example = LED0_OFF_L + 4 * 15

            /// <summary>
            /// Loads all the LEDn_ON_L registers at once.
            /// </summary>
            ALLLED_ON_L = 0xFA, 

            /// <summary>
            /// Loads all the LEDn_ON_H registers at once.
            /// </summary>
            ALLLED_ON_H = 0xFB, 

            /// <summary>
            /// Loads all the LEDn_OFF_L registers at once.
            /// </summary>
            ALLLED_OFF_L = 0xFC,

            /// <summary>
            /// Loads all the LEDn_OFF_H registers at once.
            /// </summary>
            ALLLED_OFF_H = 0xFD, 

            /// <summary>
            /// Prescaler register for PWM output frequency.
            /// </summary>
            PRESCALE = 0xFE,

        }

        /// <summary>
        /// The commands, bit-masks, and standard values for use with the control module's integrated circuit.
        /// These are byte values that are written to the device's registers.
        /// </summary>
        private enum Command
        {

            DEFAULT_CONFIG = 0x00, // 0000 0000.  Disables PCA9685 All Call I2C address (0x70) which is normally active on start-up.

            // Mode register 1, bits 0 to 7.
            // Data sheet: 7.3.1 "Mode register 1, MODE1".
            // Bit 0.
            ALLCALL_ENABLE = 0x01, // 0000 0001.
            ALLCALL_DISABLE = 0x00, // 0000 0000.
            ALLCALL_MASK = 0x7E, // 0111 1110.

            // Bit 1.
            SUBADR3_ENABLE = 0x02, // 0000 0010.
            SUBADR3_DISABLE = 0x00, // 0000 0000.
            SUBADR3_MASK = 0x7D, // 0111 1101.

            // Bit 2.
            SUBADR2_ENABLE = 0x04, // 0000 0100.
            SUBADR2_DISABLE = 0x00, // 0000 0000.
            SUBADR2_MASK = 0x7B, // 0111 1011.

            // Bit 3.
            SUBADR1_ENABLE = 0x08, // 0000 1000.
            SUBADR1_DISABLE = 0x00, // 0000 0000.
            SUBADR1_MASK = 0x77, // 0111 0111.

            // Bit 4.
            WAKE = 0x00, // 0000 0000.
            WAKE_MASK = 0x6F, // 0110 1111.
            SLEEP = 0x10, // 0001 0000.
            SLEEP_MASK = 0x7F, // 0111 1111.

            // Bit 5.  Auto-Increment functionality not implemented.
            // Bit 6.  External clock functionality not implemented.

            // Bit 7.  If the PCA9685 is operating and the user decides to put the chip to sleep without stopping any of the 
            // PWM channels, the RESTART bit (MODE1 bit 7) will be set to 1 at the end of the PWM refresh cycle. The contents 
            // of each PWM register (i.e. channel config) are saved while in sleep mode and can be restarted once the chip is awake.
            // NOTE: This is used to resume output after sleep mode.  This is NOT used to perform a "power on reset" of the device.  
            RESTART = 0x80, // 1000 0000.

            // Mode register 2 commands/functionality not implemented.

        }

        #endregion

        #region " Properties "

        /// <summary>
        /// Gets the number of channels supported by the device.
        /// </summary>
        public Int32 ChannelCount
        {
            get { return _ChannelCount; }
        }

        /// <summary>
        /// Gets the 7-bit I2C bus address of the device.
        /// </summary>
        public Int32 I2CAddress
        {
            get { return _I2CDeviceInterface.ConnectionSettings.SlaveAddress; }
        }

        /// <summary>
        /// Gets the plug and play device identifier of the inter-integrated circuit (I2C) bus controller for the device.
        /// </summary>
        public string I2CDeviceID
        {
            get { return _I2CDeviceInterface.DeviceId; }
        }

        /// <summary>
        /// Gets the bus speed used for connecting to the inter-integrated circuit
        //  (I2C) device. The bus speed is the frequency at which to clock the I2C bus when
        //  accessing the device.
        /// </summary>
        public I2cBusSpeed I2CBusSpeed
        {
            get { return _I2CDeviceInterface.ConnectionSettings.BusSpeed; }
        }

        /// <summary>
        /// Gets the current PWM update rate.  The frequency in Hz with a valid range of 24 Hz to 1526 Hz.
        /// </summary>
        public Int32 PWMUpdateRate
        {
            get { return 25000000 / ((ReadRegister(Register.PRESCALE) + 1) * 4096); }
        }

        /// <summary>
        /// Gets the current device Mode register 1 values.
        /// This register stores device level configuration details.
        /// </summary>
        /// <returns>A string of binary characters representing the single byte of data in the register.</returns>
        /// <remarks>
        /// For diagnostic use.  See section 7.3.1 of manufacturer documentation or command constants in this class 
        /// for details on what each bit in the byte does.
        /// </remarks>
        public String Mode1Config
        {
            get { return ByteToBinaryString(ReadRegister(Register.MODE1)); } 
        }

        /// <summary>
        /// Gets the current device Mode register 2 values.
        /// This register stores device level configuration details.
        /// </summary>
        /// <returns>A string of binary characters representing the single byte of data in the register.</returns>
        /// <remarks>
        /// For diagnostic use.  See section 7.3.1 of manufacturer documentation or command constants in this class 
        /// for details on what each bit in the byte does.
        /// </remarks>
        public String Mode2Config
        {
            get { return ByteToBinaryString(ReadRegister(Register.MODE2)); }
        }

        #endregion

        #region " Constructors and Initialization "

        /// <summary>
        /// Creates a new instance of the object.
        /// </summary>
        public I2CPWMDriverPCA9685() { }

        /// <summary>
        /// Initializes the I2C bus connection to the module.
        /// </summary>
        public void Initialize()
        {
            // Run the initialization sequence synchronously.
            Task.Run(() => InitializeAsync(_DefaultI2CAddress, I2cBusSpeed.FastMode)).Wait();
        }

        /// <summary>
        /// Initializes the I2C bus connection to the module.
        /// </summary>
        /// <param name="moduleI2CAddress">The I2C address of the module.</param>
        public void Initialize(byte moduleI2CAddress)
        {
            // Run the initialization sequence synchronously.
            Task.Run(() => InitializeAsync(moduleI2CAddress, I2cBusSpeed.FastMode)).Wait();
        }

        /// <summary>
        /// Initializes the I2C bus connection to the module.
        /// </summary>
        /// <param name="moduleI2CAddress">The I2C address of the module.</param>
        /// <param name="busSpeed">The I2C interface bus speed.  Default is FastMode.</param>
        public void Initialize(byte moduleI2CAddress, I2cBusSpeed busSpeed)
        {
            // Run the initialization sequence synchronously.
            Task.Run(() => InitializeAsync(moduleI2CAddress, busSpeed)).Wait();
        }

        /// <summary>
        /// Initializes the I2C bus connection to the module asynchronously.
        /// </summary>
        /// <param name="moduleI2CAddress">The I2C address of the module.</param>
        /// <param name="busSpeed">The I2C interface bus speed.  Default is FastMode.</param>
        /// <exception cref="NotSupportedException">Thrown when an I2C controller could not be found on the system.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the specified I2C address is already by used by another process or another object within the current process.
        /// </exception>
        public async Task InitializeAsync(byte moduleI2CAddress, I2cBusSpeed busSpeed)
        {
            
            // Get a selector string that will return all I2C controllers on the system.
            string QueryString = I2cDevice.GetDeviceSelector();

            // Find the I2C bus controller device for this system with the selector string.
            DeviceInformationCollection DevInfo = await DeviceInformation.FindAllAsync(QueryString);

            // Check that an I2C controller is present on the system.
            if (DevInfo.Count == 0)
            {
                throw new NotSupportedException("No I2C controllers were found on the system");
            }

            // Configure the I2C bus connection settings.
            I2cConnectionSettings Settings = new I2cConnectionSettings(moduleI2CAddress);
            Settings.SharingMode = I2cSharingMode.Shared;
            Settings.BusSpeed = busSpeed;

            // Create an I2cDevice with our selected bus controller and I2C settings.
            _I2CDeviceInterface = await I2cDevice.FromIdAsync(DevInfo[0].Id, Settings);

            // Check that the I2C module connection was successful.
            if (_I2CDeviceInterface == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Slave address {0} on I2C controller {1} is currently in use by " +
                    "another application.  Please ensure that no other applications are using the I2C device.",
                    Settings.SlaveAddress,
                    DevInfo[0].Id));
            }

            // Set default configuration to ensure a known state.
            ResetDevice();

        }

        #endregion

        #region " Destructor (Object Disposal) "

        /// <summary>
        /// Releases unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _I2CDeviceInterface.Dispose();
        }

        #endregion

        #region " Methods (Public / Device Configuration) "

        /// <summary>
        /// Sets the PWM update rate.
        /// </summary>
        /// <param name="frequency">The frequency in Hz with a valid range of 24 Hz to 1526 Hz.</param>
        /// <remarks>
        /// Actual frequency set may vary slightly from the specified frequency parameter value due to rounding to 8 bit precision.
        /// Data sheet: 7.3.5 PWM frequency PRE_SCALE.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified frequency is out of the range supported by the integrated circuit.</exception>
        public void SetPwmUpdateRate(Int32 frequency)
        {

            // The maximum PWM frequency is ~1526 Hz if the PRE_SCALE register is set to "0x03".
            // The minimum PWM frequency is ~24 Hz if the PRE_SCALE register is set to "0xFF".
            if (frequency < 24 || frequency > 1526)
            {
                throw new ArgumentOutOfRangeException("frequency", frequency, "Value must be in the range of 24 to 1526.");
            }

            // Calculate the "prescale register" value required to accomplish the specified target frequency.
            decimal PreScale = 25000000; // 25 MHz.
            PreScale /= 4096; // 12 bit.
            PreScale /= frequency;
            PreScale -= 1;
            PreScale = Math.Round(PreScale, MidpointRounding.AwayFromZero);

            // Debug output.
            //System.Diagnostics.Debug.WriteLine("Setting PWM frequency to {0} Hz using prescale value {1}", frequency, PreScale);

            // The PRE_SCALE register can only be set when the device is in sleep mode (oscillator is disabled).
            Sleep(); 

            // Set the prescale value to change the frequency.
            WriteRegister(Register.PRESCALE, (byte)Math.Floor(PreScale));

            // Return to the normal operating mode.
            Wake();
            Restart();

        }

        /// <summary>
        /// Puts the device in sleep (low power) mode.  Oscillator off.
        /// The channel outputs cannot be controlled while in sleep mode.
        /// </summary>
        public void Sleep()
        {
            // Get the current bit values in the device's MODE 1 control/config register so those bit values can be persisted while changing the target bit.
            byte RegisterValue = ReadRegister(Register.MODE1); 

            WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SLEEP_MASK) | (byte)Command.SLEEP)); // Go to sleep.
            
            // The SLEEP bit must be logic 0 for at least 5 ms, before a logic 1 is written into the RESTART bit.
            Task.Delay(10).Wait();
        }

        /// <summary>
        /// Wakes the device from sleep (low power) mode.  Oscillator on.
        /// </summary>
        public void Wake()
        {
            // Get the current bit values in the device's MODE 1 control/config register so those bit values can be persisted while changing the target bit.
            byte RegisterValue = ReadRegister(Register.MODE1);

            WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.WAKE_MASK) | (byte)Command.WAKE)); // Wake from sleep.

            // The SLEEP bit must be logic 0 for at least 5 ms, before a logic 1 is written into the RESTART bit.
            Task.Delay(10).Wait();
        }

        /// <summary>
        /// Resumes the most recent (if any) channel output after awaking the device from sleep mode.
        /// Call this method after calling Wake() to restart any output that was active prior to sleep.
        /// </summary>
        /// <remarks>
        /// If the PCA9685 is operating and the chip is put into to sleep mode without stopping any of the 
        /// PWM channels, the RESTART bit will be set to 1. The contents of each PWM register (i.e. channel config) are saved 
        /// while in sleep mode and can be restarted by calling this method once the chip is awake.
        /// NOTE: This is used to resume output after awaking from sleep mode.  This is NOT used to perform a "power on reset" or software reset of the device. 
        /// Data sheet: 7.3.1.1 "Restart mode".
        /// </remarks>
        public void Restart()
        {
            // Get the current bit values in the device's MODE 1 control/config register so those bit values can be persisted while changing the target bit.
            byte RegisterValue = ReadRegister(Register.MODE1);
            
            WriteRegister(Register.MODE1, (RegisterValue | (byte)Command.RESTART)); // Restart.
        }

        /// <summary>
        /// Resets the device/module configuration to default settings and sets all output channels to off.
        /// Can be used to return the device to a known state.
        /// </summary>
        public void ResetDevice()
        {
            // Set default configuration to ensure a known state.
            SetFullOff(PwmChannel.ALL);
            WriteRegister(Register.MODE1, (byte)Command.DEFAULT_CONFIG);
        }

        #region " Methods (Public / Advanced Device Configuration) "

        // UNDONE: This device's "all call" and "sub address" functionality have not been fully implemented and are disabled by default.
        // It is possible to enable/disable these features in the devices configuration using methods provided by this class, but this class does not make use of the features beyond that.

        /// <summary>
        /// Enables or disables the I2C "all call" address for the device.
        /// Allow multiple modules to be controlled with a single address.  
        /// All modules will respond to the all call address if enabled.
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public void SetAllCall(Boolean enable)
        {
            byte RegisterValue = ReadRegister(Register.MODE1);

            if (enable)
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.ALLCALL_MASK) | (byte)Command.ALLCALL_ENABLE)); // Enable address.
            else
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.ALLCALL_MASK) | (byte)Command.ALLCALL_DISABLE)); // Disable address.
        }

        /// <summary>
        /// Enables or disables the I2C "sub address" for the device.
        /// Allow multiple channels on a device to be controlled with a single address.  
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public void SetSubAddr1(Boolean enable)
        {
            byte RegisterValue = ReadRegister(Register.MODE1);

            if (enable)
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR1_MASK) | (byte)Command.SUBADR1_ENABLE)); // Enable address.
            else
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR1_MASK) | (byte)Command.SUBADR1_DISABLE)); // Disable address.
        }

        /// <summary>
        /// Enables or disables the I2C "sub address" for the device.
        /// Allow multiple channels on a device to be controlled with a single address.  
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public void SetSubAddr2(Boolean enable)
        {
            byte RegisterValue = ReadRegister(Register.MODE1);

            if (enable)
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR2_MASK) | (byte)Command.SUBADR2_ENABLE)); // Enable address.
            else
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR2_MASK) | (byte)Command.SUBADR2_DISABLE)); // Disable address.
        }

        /// <summary>
        /// Enables or disables the I2C "sub address" for the device.
        /// Allow multiple channels on a device to be controlled with a single address.  
        /// </summary>
        /// <param name="enable">True to enable, false to disable.</param>
        public void SetSubAddr3(Boolean enable)
        {
            byte RegisterValue = ReadRegister(Register.MODE1);

            if (enable)
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR3_MASK) | (byte)Command.SUBADR3_ENABLE)); // Enable address.
            else
                WriteRegister(Register.MODE1, ((RegisterValue & (byte)Command.SUBADR3_MASK) | (byte)Command.SUBADR3_DISABLE)); // Disable address.
        }

        #endregion

        #endregion

        #region " Methods (Public / Device Channel Control) "

        /// <summary>
        /// Sets a channels PWM on/off range.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        /// <param name="on">The on cycle value on a scale of 0 to 4095.</param>
        /// <param name="off">The off cycle value on a scale of 0 to 4095.</param>
        /// <remarks>
        /// Data sheet: 7.3.3 "LED output and PWM control".
        /// Data sheet: 7.3.4 "ALL_LED_ON and ALL_LED_OFF control".
        /// </remarks>
        public void SetPwm(PwmChannel channel, int on, int off)
        {
            // The PWM outputs have a 12 bit precision for pulse width modulation, so two register writes (one byte each) are needed to set the total value.
            // For example: The max PWM value (4095) would be written to the device registers as byte values 00001111 and 11111111.
            //              The min PWM value (zero) would be written to the device registers as byte values 00000000 and 00000000.
            //              The mid PWM value (2048) would be written to the device registers as byte values 00001000 and 00000000.
            // This process (two register writes) must be used to set both the on and off cycle values for a total of 4 register/byte writes.
            // The 13th bit in both the On and Off registers has special meaning.  Setting this bit to 1 (decimal 4096 or above) will turn the channel fully on or off 
            //   depending on which register it is applied to.  In this case the remaining 12 less significant bits will be ignored and the channel will remain constant 
            //   on or off rather than emitting a pulse.  All other bits (14 through 16) are non-writable and reserved by the device.
            // For example: SetPwm(channel, 4096, 0) would set the "On" registers to 00010000 00000000 which would disable PWM for the channel and set it to fully/constant on.
            //              SetPwm(channel, 0, 4096) would have the same affect on the "Off" registers causing the channel to be fully/constant off.

            // Set the on cycle for the specified channel register.
            WriteRegister(Register.LED0_ON_L + 4 * (int)channel, on & 0xFF); // Set the 8 least significant bits.  0000XXXXXXXX.
            WriteRegister(Register.LED0_ON_H + 4 * (int)channel, on >> 8); // Set the remaining 4 most significant bits.  XXXX00000000.

            // Set the off cycle for the specified channel register.
            WriteRegister(Register.LED0_OFF_L + 4 * (int)channel, off & 0xFF); // Set the 8 least significant bits.  0000XXXXXXXX.
            WriteRegister(Register.LED0_OFF_H + 4 * (int)channel, off >> 8); // Set the remaining 4 most significant bits.  XXXX00000000.
        }

        /// <summary>
        /// Get a channels PWM on cycle value.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        /// <returns>The On cycle value on a scale of 0 to 4095, or 4096 for constant on.</returns>
        public Int32 GetPwmOn(PwmChannel channel)
        {
            return (ReadRegister(Register.LED0_ON_H + 4 * (int)channel) << 8) + ReadRegister(Register.LED0_ON_L + 4 * (int)channel);
        }

        /// <summary>
        /// Get a channels PWM off cycle value.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        /// <returns>The Off cycle value on a scale of 0 to 4095, or 4096 for constant off.</returns>
        public Int32 GetPwmOff(PwmChannel channel)
        {
            return (ReadRegister(Register.LED0_OFF_H + 4 * (int)channel) << 8) + ReadRegister(Register.LED0_OFF_L + 4 * (int)channel);
        }

        /// <summary>
        /// Set a channel to fully on or off.  Constant output without pulse width modulation.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        /// <param name="fullOn">If set to <c>true</c>, channel is set fully on; otherwise fully off.</param>
        public void SetFull(PwmChannel channel, bool fullOn)
        {
            if (fullOn)
                SetFullOn(channel);
            else
                SetFullOff(channel);
        }

        /// <summary>
        /// Set a channel to fully on.  Constant output without pulse width modulation.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        public void SetFullOn(PwmChannel channel)
        {
            // Set channel On registers to 0001 0000 0000 0000 and Off registers to 0.
            // Setting the 13th bit to 1 sets the channel to constant output (either on or off).
            SetPwm(channel, 4096, 0); 
        }

        /// <summary>
        /// Set a channel to fully off.  Constant output without pulse width modulation.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        public void SetFullOff(PwmChannel channel)
        {
            // Set channel On registers to 0 and Off registers to 0001 0000 0000 0000.
            // Setting the 13th bit to 1 sets the channel to constant output (either on or off).
            SetPwm(channel, 0, 4096);
        }

        /// <summary>
        /// Sets a channel PWM or LED brightness by duty cycle (percentage) value.
        /// </summary>
        /// <param name="channel">The output channel.</param>
        /// <param name="dutyCycle">The percentage of time that the channel should be on from 0 to 100.</param>
        /// <remarks>
        /// This method provides a simple way of controlling output based on a value of 0 to 100 percent.
        /// Useful when precise control of the PWM start/stop cycles (such as custom offsets or initial delays) is not required.  
        /// </remarks>
        public void SetDutyCycle(PwmChannel channel, double dutyCycle)
        {

            // Zero percent duty cycle.
            if (dutyCycle == 0)
            {
                SetFullOff(channel);
                return;
            }

            // 100 percent duty cycle.
            if (dutyCycle == 100)
            {
                SetFullOn(channel);
                return;
            }

            // N percent duty cycle.
            if (dutyCycle > 0 && dutyCycle < 100)
            {
                // Calculate and set the number of cycles required to match the specified duty cycle percentage.
                int StopCycle = (int)Math.Round(4095 * dutyCycle / 100, MidpointRounding.AwayFromZero);
                SetPwm(channel, 0, StopCycle);
                return;
            }

        }

        #endregion

        #region " Methods (Private / Device Communications) "
       
        /// <summary>
        /// Writes the specified data to the specified device register.
        /// </summary>
        /// <param name="register">The device register address.</param>
        /// <param name="data">The data to write to the device.</param>
        private void WriteRegister(Register register, int data)
        {
            WriteRegister(register, (byte)data);
        }

        /// <summary>
        /// Writes the specified data to the specified device register.
        /// </summary>
        /// <param name="register">The device register address.</param>
        /// <param name="data">The data to write to the device.</param>
        private void WriteRegister(Register register, byte data)
        {

            // Debug output.
            //System.Diagnostics.Debug.WriteLine("WriteRegister: {0}, data = {1}.", register, ByteToBinaryString(data));

            _I2CDeviceInterface.Write(new[] { (byte)register, data });

        }

        /// <summary>
        /// Reads the data from the specified device register.
        /// </summary>
        /// <param name="register">The device register address.</param>
        /// <returns>The data read from the device.</returns>
        private byte ReadRegister(Register register)
        {

            // Initialize the read/write buffers.
            byte[] RegAddrBuf = new byte[] { (byte)register }; // Device register address to write.          
            byte[] ReadBuf = new byte[1]; // Read buffer to store results read from device.

            // Read from the device.
            // We call WriteRead(), first write the address of the device register to be targeted, then read back the data from the device.
            _I2CDeviceInterface.WriteRead(RegAddrBuf, ReadBuf);

            byte Result = ReadBuf[0];
            
            // Debug output.
            //System.Diagnostics.Debug.WriteLine("ReadRegister: {0}, result = {1}.", register, ByteToBinaryString(result));

            return Result;

        }

        #endregion

        #region " Methods (Private / Utility) "

        /// <summary>
        /// Converts and formats a byte of data into a string of binary characters.
        /// For example, 0x04 would be converted to 00000100.
        /// </summary>
        /// <param name="data">The byte data to be converted into a binary string format.</param>
        /// <returns>A string of eight binary characters representing the specified byte of data</returns>
        private string ByteToBinaryString(byte data)
        {
            int Value = Convert.ToInt32(data);
            string BinaryString = string.Empty;

            while (Value > 0)
            {
                BinaryString = string.Format("{0}{1}", (Value & 1) == 1 ? "1" : "0", BinaryString);
                Value >>= 1; // Binary shift one position.
            }

            // Format with leading zeros to form a full byte length.
            BinaryString = BinaryString.PadLeft(8, '0');

            return BinaryString;
        }

        #endregion

    }

}