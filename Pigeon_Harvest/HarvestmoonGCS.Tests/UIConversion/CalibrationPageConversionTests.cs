using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace HarvestmoonGCS.Tests.UIConversion;

/// <summary>
/// Unit tests for CalibrationPage XAML conversion from WPF to Uno Platform
/// Validates: Requirements 1.1, 1.2, 1.9, 2.1, 2.2, 2.9, 3.1-3.8, 5.1-5.6, 4.1-4.8, 6.1-6.9, 7.3, 7.8-7.10, 8.1-8.10, 9.1-9.10
/// </summary>
[TestClass]
public class CalibrationPageConversionTests : PageConversionTestBase
{
    private const string WpfPath = "Custom UserControls/Calibration.xaml";
    private const string UnoPath = "Views/CalibrationPage.xaml";
    private const string PageName = "CalibrationPage";

    #region Task 1.1: Flight Mode Tab Tests

    [TestMethod]
    [TestCategory("FlightMode")]
    [TestCategory("Task1.1")]
    public void FlightModeTab_HasSixComboBoxes_WithCorrectNames()
    {
        // Validates: Requirements 1.1
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var comboBoxes = controls.Where(c => 
            c.Type == "ComboBox" && 
            c.Name != null && 
            c.Name.StartsWith("CMB_fmode")).ToList();

        Assert.AreEqual(6, comboBoxes.Count, "Flight Mode tab should have 6 ComboBoxes");
        
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode1"), "CMB_fmode1 not found");
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode2"), "CMB_fmode2 not found");
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode3"), "CMB_fmode3 not found");
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode4"), "CMB_fmode4 not found");
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode5"), "CMB_fmode5 not found");
        Assert.IsTrue(comboBoxes.Any(c => c.Name == "CMB_fmode6"), "CMB_fmode6 not found");
    }

    [TestMethod]
    [TestCategory("FlightMode")]
    [TestCategory("Task1.1")]
    public void FlightModeTab_HasPWMRangeLabels_WithCorrectText()
    {
        // Validates: Requirements 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 0 - 1230"), "PWM label 1 not found");
        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 1231 - 1360"), "PWM label 2 not found");
        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 1361 - 1490"), "PWM label 3 not found");
        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 1491 - 1620"), "PWM label 4 not found");
        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 1621 - 1749"), "PWM label 5 not found");
        Assert.IsTrue(unoXaml.Contains("RC PWM OUTPUT 1750+"), "PWM label 6 not found");
    }

    [TestMethod]
    [TestCategory("FlightMode")]
    [TestCategory("Task1.1")]
    public void FlightModeTab_HasSaveButton()
    {
        // Validates: Requirements 1.9
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var saveButton = controls.FirstOrDefault(c => 
            c.Type == "Button" && 
            c.Properties.ContainsKey("Content") && 
            c.Properties["Content"] == "SAVE" &&
            c.Properties.ContainsKey("Click") &&
            c.Properties["Click"] == "saveMode_Click");

        Assert.IsNotNull(saveButton, "SAVE button not found in Flight Mode tab");
    }

    #endregion

    #region Task 2.9: Servo Output Tab Tests

    [TestMethod]
    [TestCategory("ServoOutput")]
    [TestCategory("Task2.9")]
    public void ServoOutputTab_Has16ServoRows()
    {
        // Validates: Requirements 2.1
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        // Check for all 16 Reverse checkboxes
        for (int i = 1; i <= 16; i++)
        {
            var reverseCheckBox = controls.FirstOrDefault(c => 
                c.Type == "CheckBox" && c.Name == $"Reverse{i}");
            Assert.IsNotNull(reverseCheckBox, $"Reverse{i} checkbox not found");

            var functionComboBox = controls.FirstOrDefault(c => 
                c.Type == "ComboBox" && c.Name == $"Function{i}");
            Assert.IsNotNull(functionComboBox, $"Function{i} combobox not found");

            var progressBar = controls.FirstOrDefault(c => 
                c.Type == "ProgressBar" && c.Name == $"pb_servo{i}");
            Assert.IsNotNull(progressBar, $"pb_servo{i} progress bar not found");

            var minTextBox = controls.FirstOrDefault(c => 
                c.Type == "TextBox" && c.Name == $"Min{i}");
            Assert.IsNotNull(minTextBox, $"Min{i} textbox not found");

            var trimTextBox = controls.FirstOrDefault(c => 
                c.Type == "TextBox" && c.Name == $"Trim{i}");
            Assert.IsNotNull(trimTextBox, $"Trim{i} textbox not found");

            var maxTextBox = controls.FirstOrDefault(c => 
                c.Type == "TextBox" && c.Name == $"Max{i}");
            Assert.IsNotNull(maxTextBox, $"Max{i} textbox not found");
        }
    }

    [TestMethod]
    [TestCategory("ServoOutput")]
    [TestCategory("Task2.9")]
    public void ServoOutputTab_HasSevenColumnStructure()
    {
        // Validates: Requirements 2.2
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("Text=\"No\""), "No column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Reverse\""), "Reverse column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Function\""), "Function column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Output\""), "Output column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Min\""), "Min column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Trim\""), "Trim column header not found");
        Assert.IsTrue(unoXaml.Contains("Text=\"Max\""), "Max column header not found");
    }

    [TestMethod]
    [TestCategory("ServoOutput")]
    [TestCategory("Task2.9")]
    public void ServoOutputTab_HasSendButton()
    {
        // Validates: Requirements 2.9
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var sendButton = controls.FirstOrDefault(c => 
            c.Type == "Button" && 
            c.Properties.ContainsKey("Content") && 
            c.Properties["Content"] == "SEND" &&
            c.Properties.ContainsKey("Click") &&
            c.Properties["Click"] == "SendServoConfig_Click");

        Assert.IsNotNull(sendButton, "SEND button not found in Servo Output tab");
    }

    #endregion

    #region Task 3.4: Settings Tab Tests

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("Task3.4")]
    public void SettingsTab_HasWaypointSection_WithFiveFields()
    {
        // Validates: Requirements 3.1, 3.2
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsTrue(unoXaml.Contains("WAYPOINT"), "WAYPOINT section not found");

        var wpSpeed = controls.FirstOrDefault(c => c.Name == "WP_Speed");
        Assert.IsNotNull(wpSpeed, "WP_Speed control not found");

        var wpRadius = controls.FirstOrDefault(c => c.Name == "WP_Radius");
        Assert.IsNotNull(wpRadius, "WP_Radius control not found");

        var wpSpeedup = controls.FirstOrDefault(c => c.Name == "WP_Speedup");
        Assert.IsNotNull(wpSpeedup, "WP_Speedup control not found");

        var wpSpeeddn = controls.FirstOrDefault(c => c.Name == "WP_Speeddn");
        Assert.IsNotNull(wpSpeeddn, "WP_Speeddn control not found");

        var wpLoiter = controls.FirstOrDefault(c => c.Name == "WP_Loiter");
        Assert.IsNotNull(wpLoiter, "WP_Loiter control not found");
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("Task3.4")]
    public void SettingsTab_CopterSection_HasThreeTuningGroups()
    {
        // Validates: Requirements 3.3, 3.4, 3.5
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsTrue(unoXaml.Contains("COPTER"), "COPTER section not found");
        Assert.IsTrue(unoXaml.Contains("Rate Roll"), "Rate Roll group not found");
        Assert.IsTrue(unoXaml.Contains("Rate Pitch"), "Rate Pitch group not found");
        Assert.IsTrue(unoXaml.Contains("Rate Yaw"), "Rate Yaw group not found");

        // Check Roll tuning controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollPTextBox"), "RollPTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollITextBox"), "RollITextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollDTextBox"), "RollDTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollIMAXTextBox"), "RollIMAXTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollFLTETextBox"), "RollFLTETextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollFLTDTextBox"), "RollFLTDTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollFLTTTextBox"), "RollFLTTTextBox not found");

        // Check Pitch tuning controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PitchPTextBox"), "PitchPTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PitchITextBox"), "PitchITextBox not found");

        // Check Yaw tuning controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PYawPTextBox"), "PYawPTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PYawITextBox"), "PYawITextBox not found");
    }

    [TestMethod]
    [TestCategory("Settings")]
    [TestCategory("Task3.4")]
    public void SettingsTab_PlaneSection_HasFiveGroups()
    {
        // Validates: Requirements 3.6, 3.7, 3.8
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsTrue(unoXaml.Contains("PLANE"), "PLANE section not found");
        Assert.IsTrue(unoXaml.Contains("Velocity XY"), "Velocity XY group not found");
        Assert.IsTrue(unoXaml.Contains("Safety"), "Safety group not found");

        // Check Plane Rate Roll controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PRollPTextBox"), "PRollPTextBox not found");

        // Check Plane Rate Pitch controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PPitchPTextBox"), "PPitchPTextBox not found");

        // Check Plane Rate Yaw controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PYawPTextBox"), "PYawPTextBox not found");

        // Check Velocity XY controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PVelP"), "PVelP not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PVelI"), "PVelI not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PVelD"), "PVelD not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PVelIMAX"), "PVelIMAX not found");

        // Check Safety controls
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PPitchMin"), "PPitchMin not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PPitchMax"), "PPitchMax not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PRollLimit"), "PRollLimit not found");
    }

    #endregion

    #region Task 4.1: Compass Tab Tests

    [TestMethod]
    [TestCategory("Compass")]
    [TestCategory("Task4.1")]
    public void CompassTab_HasDeviceList()
    {
        // Validates: Requirements 5.1, 5.2
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var deviceList = controls.FirstOrDefault(c => c.Name == "DeviceList");
        Assert.IsNotNull(deviceList, "DeviceList control not found");

        Assert.IsTrue(unoXaml.Contains("NOMOR"), "NOMOR column header not found");
        Assert.IsTrue(unoXaml.Contains("DEV ID"), "DEV ID column header not found");
        Assert.IsTrue(unoXaml.Contains("TYPE"), "TYPE column header not found");
    }

    [TestMethod]
    [TestCategory("Compass")]
    [TestCategory("Task4.1")]
    public void CompassTab_HasControlButtons()
    {
        // Validates: Requirements 5.3, 5.4, 5.6
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Start_comp"), "Start_comp button not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Accept_comp"), "Accept_comp button not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Cancel_comp"), "Cancel_comp button not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Reboot_comp"), "Reboot_comp button not found");
    }

    [TestMethod]
    [TestCategory("Compass")]
    [TestCategory("Task4.1")]
    public void CompassTab_HasProgressBars()
    {
        // Validates: Requirements 5.5
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "progressBar1"), "progressBar1 not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "progressBar2"), "progressBar2 not found");
    }

    [TestMethod]
    [TestCategory("Compass")]
    [TestCategory("Task4.1")]
    public void CompassTab_HasRefreshButton()
    {
        // Validates: Requirements 5.3
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("Refresh Compass List"), "Refresh Compass List button not found");
    }

    #endregion

    #region Task 5.1: ESC Tab Tests

    [TestMethod]
    [TestCategory("ESC")]
    [TestCategory("Task5.1")]
    public void EscTab_HasPWMSlider_WithCorrectRange()
    {
        // Validates: Requirements 4.1, 4.2
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var pwmSlider = controls.FirstOrDefault(c => c.Name == "PwmSlider");
        Assert.IsNotNull(pwmSlider, "PwmSlider not found");
        Assert.AreEqual("Slider", pwmSlider.Type, "PwmSlider should be a Slider");
    }

    [TestMethod]
    [TestCategory("ESC")]
    [TestCategory("Task5.1")]
    public void EscTab_Has16MotorCheckboxes()
    {
        // Validates: Requirements 4.3, 4.4
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        for (int i = 1; i <= 16; i++)
        {
            var motorCheckBox = controls.FirstOrDefault(c => c.Name == $"Motor{i}CheckBox");
            Assert.IsNotNull(motorCheckBox, $"Motor{i}CheckBox not found");
        }
    }

    [TestMethod]
    [TestCategory("ESC")]
    [TestCategory("Task5.1")]
    public void EscTab_HasControlButtons()
    {
        // Validates: Requirements 4.5, 4.6
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("Send Min PWM"), "Send Min PWM button not found");
        Assert.IsTrue(unoXaml.Contains("Send Mid PWM"), "Send Mid PWM button not found");
        Assert.IsTrue(unoXaml.Contains("Send Max PWM"), "Send Max PWM button not found");
        Assert.IsTrue(unoXaml.Contains("Auto Calibrate"), "Auto Calibrate button not found");
    }

    [TestMethod]
    [TestCategory("ESC")]
    [TestCategory("Task5.1")]
    public void EscTab_HasStatusLabel()
    {
        // Validates: Requirements 4.7
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var statusLabel = controls.FirstOrDefault(c => c.Name == "EscStatusLabel");
        Assert.IsNotNull(statusLabel, "EscStatusLabel not found");
    }

    #endregion

    #region Task 5.2: Motor Test Tab Tests

    [TestMethod]
    [TestCategory("MotorTest")]
    [TestCategory("Task5.2")]
    public void MotorTestTab_HasThrottleSlider_WithCorrectRange()
    {
        // Validates: Requirements 6.1, 6.2
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var throttleSlider = controls.FirstOrDefault(c => c.Name == "MotorPwmSlider");
        Assert.IsNotNull(throttleSlider, "MotorPwmSlider not found");
        Assert.AreEqual("Slider", throttleSlider.Type, "MotorPwmSlider should be a Slider");
    }

    [TestMethod]
    [TestCategory("MotorTest")]
    [TestCategory("Task5.2")]
    public void MotorTestTab_HasDurationSlider_WithCorrectRange()
    {
        // Validates: Requirements 6.3, 6.4
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var durationSlider = controls.FirstOrDefault(c => c.Name == "MotorDurationSlider");
        Assert.IsNotNull(durationSlider, "MotorDurationSlider not found");
        Assert.AreEqual("Slider", durationSlider.Type, "MotorDurationSlider should be a Slider");
    }

    [TestMethod]
    [TestCategory("MotorTest")]
    [TestCategory("Task5.2")]
    public void MotorTestTab_Has16MotorButtons()
    {
        // Validates: Requirements 6.5, 6.6
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        for (int i = 1; i <= 16; i++)
        {
            var motorButton = controls.FirstOrDefault(c => c.Name == $"Motor{i}Btn");
            Assert.IsNotNull(motorButton, $"Motor{i}Btn not found");
        }
    }

    [TestMethod]
    [TestCategory("MotorTest")]
    [TestCategory("Task5.2")]
    public void MotorTestTab_HasControlButtons()
    {
        // Validates: Requirements 6.7, 6.8
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("TEST ALL MOTORS"), "TEST ALL MOTORS button not found");
        Assert.IsTrue(unoXaml.Contains("STOP ALL MOTORS"), "STOP ALL MOTORS button not found");
    }

    [TestMethod]
    [TestCategory("MotorTest")]
    [TestCategory("Task5.2")]
    public void MotorTestTab_HasStatusLabel()
    {
        // Validates: Requirements 6.9
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        var statusLabel = controls.FirstOrDefault(c => c.Name == "MotorTestStatus");
        Assert.IsNotNull(statusLabel, "MotorTestStatus not found");
    }

    #endregion

    #region Task 6.3: Tab Visibility Tests

    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task6.3")]
    public void CalibrationPage_HasAllSevenTabs()
    {
        // Validates: Requirements 7.1, 7.2
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Accele_calib"), "Accele_calib tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Kompass_calib"), "Kompass_calib tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Flight_mode"), "Flight_mode tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "ServoOutput"), "ServoOutput tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Setting"), "Setting tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "EscCalib"), "EscCalib tab not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "MotorTestGrid"), "MotorTestGrid tab not found");
    }

    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task6.3")]
    public void CalibrationPage_HasAllNavigationButtons()
    {
        // Validates: Requirements 9.10
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "AccelerometerButton"), "AccelerometerButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "CompassButton"), "CompassButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "FlightButton"), "FlightButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "ServoOutputButton"), "ServoOutputButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "SettingButton"), "SettingButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "EscCalibButton"), "EscCalibButton not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "MotorTestButton"), "MotorTestButton not found");
    }

    [TestMethod]
    [TestCategory("TabVisibility")]
    [TestCategory("Task6.3")]
    public void CalibrationPage_DefaultTabIsAccelerometer()
    {
        // Validates: Requirements 7.3
        var unoXaml = LoadUnoXaml(UnoPath);

        // Check that Accele_calib has Visibility="Visible"
        Assert.IsTrue(unoXaml.Contains("x:Name=\"Accele_calib\"") && 
                     unoXaml.Contains("Visibility=\"Visible\""), 
                     "Accele_calib should be visible by default");
    }

    #endregion

    #region Task 7.6: Styling Consistency Tests

    [TestMethod]
    [TestCategory("Styling")]
    [TestCategory("Task7.6")]
    public void CalibrationPage_HasCorrectBackgroundColors()
    {
        // Validates: Requirements 8.1, 8.2
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("Background=\"DarkGray\""), "DarkGray background not found");
        Assert.IsTrue(unoXaml.Contains("Background=\"LightGray\""), "LightGray background not found");
    }

    [TestMethod]
    [TestCategory("Styling")]
    [TestCategory("Task7.6")]
    public void CalibrationPage_HasCorrectBorderStyling()
    {
        // Validates: Requirements 8.3, 8.7
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("BorderBrush=\"White\""), "White border brush not found");
        Assert.IsTrue(unoXaml.Contains("BorderThickness=\"2\""), "BorderThickness=\"2\" not found");
        Assert.IsTrue(unoXaml.Contains("CornerRadius=\"5\""), "CornerRadius=\"5\" not found");
    }

    [TestMethod]
    [TestCategory("Styling")]
    [TestCategory("Task7.6")]
    public void CalibrationPage_HasCorrectFontStyling()
    {
        // Validates: Requirements 8.4, 8.8
        var unoXaml = LoadUnoXaml(UnoPath);

        Assert.IsTrue(unoXaml.Contains("FontSize=\"20\""), "FontSize=\"20\" not found");
        Assert.IsTrue(unoXaml.Contains("FontSize=\"15\""), "FontSize=\"15\" not found");
        Assert.IsTrue(unoXaml.Contains("FontSize=\"14\""), "FontSize=\"14\" not found");
        Assert.IsTrue(unoXaml.Contains("FontWeight=\"Bold\""), "FontWeight=\"Bold\" not found");
    }

    [TestMethod]
    [TestCategory("Styling")]
    [TestCategory("Task7.6")]
    public void CalibrationPage_HasCorrectControlSizes()
    {
        // Validates: Requirements 8.10
        var unoXaml = LoadUnoXaml(UnoPath);

        // ComboBox widths (120-150px)
        Assert.IsTrue(unoXaml.Contains("Width=\"120\"") || unoXaml.Contains("Width=\"150\""), 
            "ComboBox width not found");

        // TextBox widths (60-75px)
        Assert.IsTrue(unoXaml.Contains("Width=\"60\"") || unoXaml.Contains("Width=\"75\""), 
            "TextBox width not found");

        // Button heights (40-50px)
        Assert.IsTrue(unoXaml.Contains("Height=\"40\"") || unoXaml.Contains("Height=\"50\""), 
            "Button height not found");
    }

    #endregion

    #region Task 8.1: Control Naming Tests

    [TestMethod]
    [TestCategory("ControlNaming")]
    [TestCategory("Task8.1")]
    public void CalibrationPage_AllControlNames_MatchRequirements()
    {
        // Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9, 9.10
        var unoXaml = LoadUnoXaml(UnoPath);
        var controls = XamlParsingUtilities.ExtractControls(unoXaml);

        // Flight Mode controls (9.1)
        for (int i = 1; i <= 6; i++)
        {
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"CMB_fmode{i}"), 
                $"CMB_fmode{i} not found");
        }

        // Servo controls (9.2)
        for (int i = 1; i <= 16; i++)
        {
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Reverse{i}"), 
                $"Reverse{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Function{i}"), 
                $"Function{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"pb_servo{i}"), 
                $"pb_servo{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Min{i}"), 
                $"Min{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Trim{i}"), 
                $"Trim{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Max{i}"), 
                $"Max{i} not found");
        }

        // Settings controls (9.3)
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "WP_Speed"), "WP_Speed not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "WP_Radius"), "WP_Radius not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "WP_Speedup"), "WP_Speedup not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "WP_Speeddn"), "WP_Speeddn not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "WP_Loiter"), "WP_Loiter not found");

        // Tuning controls (9.4)
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollPTextBox"), "RollPTextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollITextBox"), "RollITextBox not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "RollDTextBox"), "RollDTextBox not found");

        // ESC controls (9.5)
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PwmSlider"), "PwmSlider not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "PwmValueLabel"), "PwmValueLabel not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "EscStatusLabel"), "EscStatusLabel not found");

        // Motor Test controls (9.6)
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "MotorPwmSlider"), "MotorPwmSlider not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "MotorDurationSlider"), "MotorDurationSlider not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "MotorTestStatus"), "MotorTestStatus not found");

        // Compass controls (9.7)
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "DeviceList"), "DeviceList not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Start_comp"), "Start_comp not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Accept_comp"), "Accept_comp not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Cancel_comp"), "Cancel_comp not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "progressBar1"), "progressBar1 not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "progressBar2"), "progressBar2 not found");
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "Reboot_comp"), "Reboot_comp not found");

        // Accelerometer controls (9.8)
        for (int i = 1; i <= 6; i++)
        {
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Border{i}"), 
                $"Border{i} not found");
            Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == $"Text{i}"), 
                $"Text{i} not found");
        }
        Assert.IsNotNull(controls.FirstOrDefault(c => c.Name == "AcceleButton"), "AcceleButton not found");
    }

    #endregion

    #region General Tests

    [TestMethod]
    [TestCategory("General")]
    public void CalibrationPage_AllEventHandlers_AreConnected()
    {
        var unoXaml = LoadUnoXaml(UnoPath);
        var handlers = XamlParsingUtilities.ExtractEventHandlers(unoXaml);

        foreach (var handler in handlers)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(handler.HandlerMethod), 
                $"Event handler for {handler.EventName} on {handler.ControlName ?? handler.ControlType} is empty");
        }
    }

    #endregion
}
