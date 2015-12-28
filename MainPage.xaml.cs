using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using TwoSeven.IoT.ExpansionModule;

namespace I2CPWMDriverTest
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        /// <summary>
        /// The PWMDriver module #1 instance reference.
        /// </summary>
        private TwoSeven.IoT.ExpansionModule.I2CPWMDriverPCA9685 _PWMDriver1;

        /// <summary>
        /// The PWMDriver module #2 instance reference.
        /// </summary>
        private TwoSeven.IoT.ExpansionModule.I2CPWMDriverPCA9685 _PWMDriver2;

        /// <summary>
        /// Used by the test functions to track status.
        /// </summary>
        private Int32 _CurrentTestLevelSequence = 0;

        /// <summary>
        /// Used by the test functions to track status.
        /// </summary>
        private Boolean _StopTesting = false;

        #region " Initialization "

        /// <summary>
        /// Page entry point.
        /// </summary>
        public MainPage()
        {

            this.InitializeComponent();

            // Register for the unloaded event so we can clean up (release memory resources) on exit.
            this.Unloaded += MainPage_Unloaded;

            // Sync the update rate value with the user interface control.
            Int32 UpdateRate = (Int32)this.slidePWMFrequency.Value;

            try
            {
                // Initialize the I2C PWM driver module #1.
                LogStatus("PWM Driver module 1 initializing...");

                _PWMDriver1 = new TwoSeven.IoT.ExpansionModule.I2CPWMDriverPCA9685();
                _PWMDriver1.Initialize(0x40, Windows.Devices.I2c.I2cBusSpeed.FastMode);
                
                LogStatus("PWM Driver module 1 initialized successfully.");

                // Set the PWM frequency.
                _PWMDriver1.SetPwmUpdateRate(UpdateRate);
                LogStatus("PWM Driver module 1 set to a frequency of " + _PWMDriver1.PWMUpdateRate + " Hz.");

            }
            catch (Exception ex)
            {
                LogStatus("ERROR: Failed to initialize the PWM Driver module 1: " + ex.Message);
            }

            try
            {
                // Initialize the I2C PWM driver module #2.
                LogStatus("PWM Driver module 2 initializing...");

                // Initialize the I2C PWM driver module.
                _PWMDriver2 = new TwoSeven.IoT.ExpansionModule.I2CPWMDriverPCA9685();
                _PWMDriver2.Initialize(0x41, Windows.Devices.I2c.I2cBusSpeed.FastMode);

                LogStatus("PWM Driver module 2 initialized successfully.");

                // Set the PWM frequency.
                _PWMDriver2.SetPwmUpdateRate(UpdateRate);
                LogStatus("PWM Driver module 2 set to a frequency of " + _PWMDriver2.PWMUpdateRate + " Hz.");

            }
            catch (Exception ex)
            {
                LogStatus("ERROR: Failed to initialize the PWM Driver module 2: " + ex.Message);
            }

        }

        #endregion

        /// <summary>
        /// Page unload event handler.
        /// Cleanup on exit.
        /// </summary>
        private void MainPage_Unloaded(object sender, object args)
        {
            // Turn all output channels off.
            _PWMDriver1.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);
            _PWMDriver2.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);

            // Releases unmanaged resourced from memory.
            _PWMDriver1.Dispose();
            _PWMDriver2.Dispose();
        }

        /// <summary>
        /// Test the I2CDeviceUtilities.ScanForDevices method.
        /// This scans the whole I2C bus for any device, not just the PWM driver module.
        /// </summary>
        /// <remarks>
        /// This process takes about 1 minute.  May lock the UI.  
        /// It should probably be called using it's async method, but this is just for testing.
        /// </remarks>
        private void btnScanI2CBus_Click(object sender, RoutedEventArgs e)
        {

            TwoSeven.IoT.I2CDeviceUtilities I2CUtilities = new TwoSeven.IoT.I2CDeviceUtilities();

            System.Collections.Generic.SortedList<Int32, String> ScanResults = I2CUtilities.ScanForDevices();

            foreach (System.Collections.Generic.KeyValuePair<Int32, String> ScanResult in ScanResults)
            {
                LogStatus(String.Format("Device: 0x{0} - {1}", ScanResult.Key.ToString("X").PadLeft(2, '0'), ScanResult.Value));
            }

        }

        #region " Methods (Utility and Helper) "

        /// This region contains helper methods.  Nothing in this region has anything to do with PWM or the I2C PWM module.

        /// <summary>
        /// Calculates a value to use for PWM output tests.
        /// This is just a helper method used to output a sequence of numbers.  It has nothing to do with PWM or the I2C PWM module.
        /// </summary>
        private Int32 GetNextTestLevel()
        {

            // Reset test level sequence number when it is equal to the highest channel count on the device.
            if (_CurrentTestLevelSequence >= 16) { _CurrentTestLevelSequence = 0; }

            _CurrentTestLevelSequence += 1;

            // Limit value to a max of 2500 to help prevent LED burnout in case LEDs are being used for output testing.
            return (int)Math.Min(2500, Math.Pow(2, _CurrentTestLevelSequence));

        }

        /// <summary>
        /// Saves or displays the specified status text to various output sources.
        /// This is just a helper method used to log/display status.  It has nothing to do with PWM or the I2C PWM module.
        /// </summary>
        /// <param name="StatusText">The status message text to be saved or displayed.</param>
        private void LogStatus(string StatusText)
        {
            this.txtStatus.Text += StatusText + Environment.NewLine; // Display in the application's UI.
            System.Diagnostics.Debug.WriteLine("LogStatus: " + StatusText); // Display in VisualStudio's output window.
        }

        #endregion

        #region " Methods (Module LED Tests) "

        // This region contains tests related to global module functionality.

        /// <summary>
        /// Stops any running tests and resets the PWM output status to off for all channels.
        /// </summary>
        private async void btnStopAllTests_Click(object sender, RoutedEventArgs e)
        {

            LogStatus("Stopping test(s)...");

            _StopTesting = true;

            await System.Threading.Tasks.Task.Delay(1000);

            _PWMDriver1.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);
            _PWMDriver2.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);

            LogStatus("Outputs reset to off.");

        }

        /// <summary>
        /// Tests the PWM module by fading all outputs at once.
        /// Intended for use with LEDs.
        /// </summary>
        private async void btnAllFadeTest_Click(object sender, RoutedEventArgs e)
        {

            _StopTesting = false;
            LogStatus("Starting all fade test.");

            while (_StopTesting == false)
            {

                // Get a number to use as a test level.
                Int32 TestLevel = GetNextTestLevel();
                                
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.ALL, 0, TestLevel); // I2C PWM driver module #1.
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.ALL, 0, TestLevel); // I2C PWM driver module #2.

                await System.Threading.Tasks.Task.Delay(50);

            }

            LogStatus("Stopping all fade test.");

        }

        /// <summary>
        /// Tests the PWM module by fading outputs in a wave effect.
        /// Intended for use with LEDs.
        /// </summary>
        private async void btnWaveFadeTest_Click(object sender, RoutedEventArgs e)
        {

            _StopTesting = false;
            LogStatus("Starting wave fade test.");

            while (_StopTesting == false)
            {

                // I2C PWM driver module #1.
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C0, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C1, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C2, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C3, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C4, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C5, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C6, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C7, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C8, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C9, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C10, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C11, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C12, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C13, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C14, 0, GetNextTestLevel());
                _PWMDriver1.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C15, 0, GetNextTestLevel());

                // I2C PWM driver module #2.
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C0, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C1, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C2, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C3, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C4, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C5, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C6, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C7, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C8, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C9, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C10, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C11, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C12, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C13, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C14, 0, GetNextTestLevel());
                _PWMDriver2.SetPwm(I2CPWMDriverPCA9685.PwmChannel.C15, 0, GetNextTestLevel());

                GetNextTestLevel(); // Increment to the next number so that the "wave fade effect" will move.

                await System.Threading.Tasks.Task.Delay(50);

            }

            LogStatus("Stopping wave fade test.");

        }

        /// <summary>
        /// Tests the PWM module by fading all outputs (per module) individually one at a time.
        /// Intended for use with LEDs.
        /// </summary>
        private async void btnCycleEachOutput_Click(object sender, RoutedEventArgs e)
        {

            _StopTesting = false;
            LogStatus("Starting cycle output test.");

            while (_StopTesting == false)
            {

                for (Int32 Channel = 0; Channel <= _PWMDriver1.ChannelCount - 1; Channel++)
                {

                    for (Int32 Level = 16; Level <= 2500; Level *= 2)
                    {

                        // I2C PWM driver module #1.
                        _PWMDriver1.SetPwm((I2CPWMDriverPCA9685.PwmChannel)Channel, 0, Level);

                        // I2C PWM driver module #2.
                        _PWMDriver2.SetPwm((I2CPWMDriverPCA9685.PwmChannel)Channel, 0, Level);

                        await System.Threading.Tasks.Task.Delay(100);

                        if (_StopTesting == true) { break; }

                    }

                    _PWMDriver1.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);
                    _PWMDriver2.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);

                }

            }

            LogStatus("Stopping cycle output test.");

        }

        /// <summary>
        /// Tests the PWM module by randomly blinking all outputs individually.
        /// Intended for use with LEDs.
        /// </summary>
        private async void btnRandomLEDBlinkTest_Click(object sender, RoutedEventArgs e)
        {

            _StopTesting = false;
            LogStatus("Starting random blink test.");

            while (_StopTesting == false)
            {

                Random Rnd = new Random();

                for (Int32 Channel = 0; Channel <= _PWMDriver1.ChannelCount - 1; Channel++)
                {

                    Int32 Level = Rnd.Next(0, 2048);

                    // I2C PWM driver module #1.
                    _PWMDriver1.SetPwm((I2CPWMDriverPCA9685.PwmChannel)Channel, 0, Level);

                    // I2C PWM driver module #2.
                    _PWMDriver2.SetPwm((I2CPWMDriverPCA9685.PwmChannel)Channel, 0, Level);

                    await System.Threading.Tasks.Task.Delay(100);

                    if (_StopTesting == true) { break; }

                }

                _PWMDriver1.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);
                _PWMDriver2.SetFullOff(I2CPWMDriverPCA9685.PwmChannel.ALL);

            }

            LogStatus("Stopping random blink test.");

        }

        /// <summary>
        /// Tests the PWM module by turning on (full on, no PWM) all outputs.
        /// Intended for use with LEDs.
        /// </summary>
        private void btnAllFullOn_Click(object sender, RoutedEventArgs e)
        {

            LogStatus("Starting all full on test.");

            // I2C PWM driver module #1.
            _PWMDriver1.SetFullOn(I2CPWMDriverPCA9685.PwmChannel.ALL);

            // I2C PWM driver module #2.
            _PWMDriver2.SetFullOn(I2CPWMDriverPCA9685.PwmChannel.ALL);

            LogStatus("Stopping all full on test.");

        }

        #endregion

        #region " Methods (Module Servo Tests) "

        // This region contains tests related to global module functionality.

        // TowerPro SG90 servo.
        //    0.75 to 2.5ms PWM range for control.
        //       Neutral 0 degrees = ~1.6ms, Clockwise 90 degrees = ~0.73ms, Counter Clockwise 90 degrees = ~2.47ms.
        //    Wires: Signal = yellow, red = positive, brown = ground.

        // Servo millisecond values corresponding to left limit, right limit, and center positions.
        private const double _ServoLimitLow = 0.66666667; 
        private const double _ServoCenter = 1.66666667;
        private const double _ServoLimitHigh = 2.66666667;

        /// <summary>
        /// Calculates the required duty cycle value from the specified frequency and number of milliseconds.
        /// </summary>
        /// <param name="frequency">The frequency in Hz.</param>
        /// <param name="milliseconds">The number of milliseconds of PWM "on" time.</param>
        /// <returns>The duty cycle value.</returns>
        private double CalculateDutyCycle(double frequency, double milliseconds)
        {

            const Int32 MillisecondsPerSecond = 1000;
            double Result;

            Result = MillisecondsPerSecond / frequency; // Calculate the number of milliseconds in each PWM cycle.
            Result = Result / milliseconds; // Divide milliseconds per PWM cycle by the target "on" millisecond time.
            Result = 100 / Result; // Convert to a percentage (aka duty cycle).

            return Result;

        }

        /// <summary>
        /// Servo test.  Moves a servo connected to PWM channel 1 on module 1 in 45 degree increments.
        /// Servo duty cycle parameters, frequencies, and limits vary by manufacturer, model, and individual unit.
        /// Consider these numbers to be a starting point.  Servo limit values may need to be adjusted for the servo being used.
        /// </summary>
        private async void btnServoTest_Click(object sender, RoutedEventArgs e)
        {

            _StopTesting = false;
            LogStatus("Starting cycle output test.");

            // Servo duty cycle values corresponding to left limit, right limit, and center positions.
            double ServoDutyCycleLow = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoLimitLow);
            double ServoDutyCycleCenter = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoCenter);
            double ServoDutyCycleHigh = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoLimitHigh);

            while (_StopTesting == false)
            {

                LogStatus(String.Format("Servo adjustments: CW = {0}, Center = {1}, CCW = {2}", this.slideServoLimitCW.Value, this.slideServoCenter.Value, this.slideServoLimitCCW.Value));

                // CW 90.
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, ServoDutyCycleLow + this.slideServoLimitCW.Value);

                await System.Threading.Tasks.Task.Delay(2000);

                // CW 45.
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, (ServoDutyCycleLow + ServoDutyCycleCenter) / 2 + this.slideServoLimitCW.Value);

                await System.Threading.Tasks.Task.Delay(2000);

                // Center.
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, ServoDutyCycleCenter + this.slideServoCenter.Value);

                await System.Threading.Tasks.Task.Delay(2000);

                // CCW 45.
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, (ServoDutyCycleCenter + ServoDutyCycleHigh) / 2 + this.slideServoLimitCCW.Value);

                await System.Threading.Tasks.Task.Delay(2000);

                // CCW 90.
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, ServoDutyCycleHigh + this.slideServoLimitCCW.Value);

                await System.Threading.Tasks.Task.Delay(2000);

            }

        }

        /// <summary>
        /// Servo test.  Moves a servo connected to PWM channel 1 on module 1 based on the value of a slider control on the user interface.
        /// Servo duty cycle parameters, frequencies, and limits vary by manufacturer, model, and individual unit.
        /// Consider these numbers to be a starting point.  Servo limit values may need to be adjusted for the servo being used.
        /// </summary>
        private void slideServoPosition_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {

            if (_StopTesting)
            {
                // Servo duty cycle values corresponding to left limit, right limit, and center positions.
                double ServoDutyCycleLow = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoLimitLow);
                double ServoDutyCycleCenter = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoCenter);
                double ServoDutyCycleHigh = CalculateDutyCycle(_PWMDriver1.PWMUpdateRate, _ServoLimitHigh);

                double ServoPosition = ServoDutyCycleCenter + this.slideServoCenter.Value; // Set to the center position (including calibration value) initially.
                double ServoDirectionPercent = Math.Abs(this.slideServoPosition.Value) * 0.01; // Get the percentage away from the center.

                // Calculate value for low (normally clockwise) position.
                if (this.slideServoPosition.Value > 0)
                {
                    ServoPosition -= (ServoDutyCycleCenter - ServoDutyCycleLow) * ServoDirectionPercent + (this.slideServoLimitCW.Value * ServoDirectionPercent);
                }

                // Calculate value for high (normally counter clockwise) position.
                if (this.slideServoPosition.Value < 0)
                {
                    ServoPosition += (ServoDutyCycleHigh - ServoDutyCycleCenter) * ServoDirectionPercent + (this.slideServoLimitCCW.Value * ServoDirectionPercent);
                }

                // Set the position;
                _PWMDriver1.SetDutyCycle(I2CPWMDriverPCA9685.PwmChannel.C0, ServoPosition);
                    
                // Debug output.
                LogStatus(String.Format("Servo frequency = {0} Hz., Duty cycle = {1}.  Servo adjustments: CW = {2}, Center = {3}, CCW = {4}", 
                    _PWMDriver1.PWMUpdateRate, ServoPosition, this.slideServoLimitCW.Value, this.slideServoCenter.Value, this.slideServoLimitCCW.Value));

            } else {
                LogStatus("Stop all other tests before attempting to manually control a servo channel.");
            }
                   
        }

        #endregion

        #region " Methods (Module Config/Control Tests) "

        // This region contains tests related to global module functionality.

        /// <summary>
        /// Sets the PWM update rate/frequency.
        /// </summary>
        private void slideFrequency_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {

            if (_PWMDriver1 != null)
            {
                _PWMDriver1.SetPwmUpdateRate((int)this.slidePWMFrequency.Value);
                LogStatus(String.Format("_PWMDriver1 PWM frequency = {0} Hz.", _PWMDriver1.PWMUpdateRate));

                _PWMDriver2.SetPwmUpdateRate((int)this.slidePWMFrequency.Value);
                LogStatus(String.Format("_PWMDriver2 PWM frequency = {0} Hz.", _PWMDriver2.PWMUpdateRate));
            }

        }

        /// <summary>
        /// Tests the ResetDevice functionality.
        /// </summary>
        private void btnResetDevice_Click(object sender, RoutedEventArgs e)
        {

            LogStatus(String.Format("_PWMDriver1 -> ResetDevice"));
            LogStatus(String.Format("Before _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));
            _PWMDriver1.ResetDevice(); // I2C PWM driver module #1.
            LogStatus(String.Format("After _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));

            LogStatus(String.Format("_PWMDriver2 -> ResetDevice"));
            LogStatus(String.Format("Before _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));
            _PWMDriver2.ResetDevice(); // I2C PWM driver module #2.
            LogStatus(String.Format("After _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));

        }

        /// <summary>
        /// Tests the Sleep functionality.
        /// </summary>
        private void btnSleep_Click(object sender, RoutedEventArgs e)
        {

            LogStatus(String.Format("_PWMDriver1 -> Sleep"));
            LogStatus(String.Format("Before _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));
            _PWMDriver1.Sleep(); // I2C PWM driver module #1.
            LogStatus(String.Format("After _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));

            LogStatus(String.Format("_PWMDriver2 -> Sleep"));
            LogStatus(String.Format("Before _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));
            _PWMDriver2.Sleep(); // I2C PWM driver module #2.
            LogStatus(String.Format("After _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));

        }

        /// <summary>
        /// Tests the Wake functionality.
        /// </summary>
        private void btnWake_Click(object sender, RoutedEventArgs e)
        {

            LogStatus(String.Format("_PWMDriver1 -> Wake"));
            LogStatus(String.Format("Before _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));
            _PWMDriver1.Wake(); // I2C PWM driver module #1.
            LogStatus(String.Format("After _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));

            LogStatus(String.Format("_PWMDriver2 -> Wake"));
            LogStatus(String.Format("Before _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));
            _PWMDriver2.Wake(); // I2C PWM driver module #2.
            LogStatus(String.Format("After _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));

        }

        /// <summary>
        /// Tests the Restart functionality.
        /// This resets any output that was active prior to sleep mode.  
        /// It does not restart the hardware module/device.
        /// </summary>
        private void btnRestart_Click(object sender, RoutedEventArgs e)
        {

            // NOTE: if test loops are currently running (async or background) from one of the other test methods, the test loop may update the channel output 
            // as soon as the module is awake (using Wake method) and the channel output will begin again.  This will make it difficult to see the full 
            // sleep, wake, restart sequence since the restart functionality will be disabled as soon as a channel is updated by the already running test loop.  
            // To test the full sleep, wake, restart sequence; stop any tests that use continuous loops and use a fixed output test instead.  
            // For example: Stop All Tests >> Start Test (all full on) >> Sleep >> Wake >> Restart.  
            //   The output (all full on) should resume only after "Restart" is run. 

            LogStatus(String.Format("_PWMDriver1 -> Restart"));
            LogStatus(String.Format("Before _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));
            _PWMDriver1.Restart(); // I2C PWM driver module #1.
            LogStatus(String.Format("After _PWMDriver1 Mode1 / Mode 2: {0} / {1}", _PWMDriver1.Mode1Config, _PWMDriver1.Mode2Config));

            LogStatus(String.Format("_PWMDriver2 -> Restart"));
            LogStatus(String.Format("Before _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));
            _PWMDriver2.Restart(); // I2C PWM driver module #2.
            LogStatus(String.Format("After _PWMDriver2 Mode1 / Mode 2: {0} / {1}", _PWMDriver2.Mode1Config, _PWMDriver2.Mode2Config));

        }

        #endregion

    }

}
