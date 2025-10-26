using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using x360ce.App.Input.Devices;
using x360ce.App.Input.States;

namespace x360ce.App.Input.Triggers
{
    /// <summary>
    /// Handles device selection events and generates device information display.
    /// Extracts detailed device inputs and formats them for display.
    /// </summary>
    internal class DevicesTab_DeviceSelectedInput
    {
        private readonly UnifiedInputDeviceManager _unifiedInputDeviceInfoInternal;
        private readonly DirectInputState _statesDirectInput = new DirectInputState();
        private readonly XInputState _statesXInput = new XInputState();
        private readonly GamingInputState _statesGamingInput = new GamingInputState();

        /// <summary>
        /// Initializes a new instance with reference to the unified device collection.
        /// </summary>
        /// <param name="unifiedDeviceInfo">The unified device collection containing all device lists</param>
        public DevicesTab_DeviceSelectedInput(UnifiedInputDeviceManager unifiedInputDevice)
        {
            _unifiedInputDeviceInfoInternal = unifiedInputDevice ?? throw new ArgumentNullException(nameof(unifiedInputDevice));
        }

        /// <summary>
        /// Gets axe, slider, button, key and pov information from device state as XAML elements for display.
        /// Extracts properties from the appropriate device list based on input type.
        /// </summary>
        /// <param name="inputType">The input type (DirectInput, RawInput, etc.)</param>
        /// <param name="interfacePath">The device interface path for identification</param>
        /// <returns>UIElement containing formatted device information or null if device not found</returns>
        public UIElement GetDeviceInputAsXamlElements(UnifiedInputDeviceInfo unifiedInputDeviceInfo)
        {
            // Get device state for live values (may be null if device is offline)
            var deviceStateAsList = GetDeviceStateAsList(unifiedInputDeviceInfo);

            // Create layout based on device capabilities, not state list length
            return CreateInputLayout(unifiedInputDeviceInfo, deviceStateAsList);
        }


        /// <summary>
		/// Retrieves device information properties from the appropriate device list.
		/// </summary>
		/// <param name="inputType">The input type identifier</param>
		/// <param name="interfacePath">The device interface path</param>
		/// <returns>List of property name-value pairs</returns>
		private InputStateAsList GetDeviceStateAsList(UnifiedInputDeviceInfo unifiedInputDeviceInfo)
        {
            var inputType = unifiedInputDeviceInfo.InputType;
            var interfacePath = unifiedInputDeviceInfo.InterfacePath;

            InputStateAsList listState = null;

            switch (inputType)
            {
                //case "PnPInput":
                //    PnPInputDeviceInfo pnpInputDeviceInfo = _unifiedInputDeviceInfo.PnPInputDeviceInfoList?
                //        .FirstOrDefault(d => string.Equals(d.HardwareIds, interfacePath, StringComparison.OrdinalIgnoreCase) ||
                //                             string.Equals(d.DeviceInstanceId, interfacePath, StringComparison.OrdinalIgnoreCase));
                //    break;
                case "RawInput":
                    RawInputDeviceInfo riInfo = _unifiedInputDeviceInfoInternal.RawInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest RawInput device state (non-blocking) using singleton
                    var riState = RawInputState.Instance.GetRawInputState(riInfo);
                    // Convert RawInput state to ListTypeState format (non-blocking)
                    listState = RawInputStateToList.ConvertRawInputStateToList(riState, riInfo);
                    break;

                case "DirectInput":
                    DirectInputDeviceInfo directInputDeviceInfo = _unifiedInputDeviceInfoInternal.DirectInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest DirectInput device state (non-blocking)
                    var diState = _statesDirectInput.GetDirectInputState(directInputDeviceInfo);
                    // Convert DirectInput state to ListTypeState format (non-blocking)
                    listState = DirectInputStateToList.ConvertDirectInputStateToList(diState);
                    break;

                case "XInput":
                    XInputDeviceInfo xInputDeviceInfo = _unifiedInputDeviceInfoInternal.XInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest XInput device state (non-blocking)
                    var xiState = _statesXInput.GetXInputState(xInputDeviceInfo);
                    // Convert XInput state to ListTypeState format (non-blocking)
                    if (xiState.HasValue)
                        listState = XInputStateToList.ConvertXInputStateToList(xiState.Value);
                    break;

                case "GamingInput":
                    GamingInputDeviceInfo gamingInputDeviceInfo = _unifiedInputDeviceInfoInternal.GamingInputDeviceInfoList?
                        .FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
                    // Get the latest GamingInput device state (non-blocking)
                    var giState = _statesGamingInput.GetGamingInputState(gamingInputDeviceInfo);
                    // Convert GamingInput state to ListTypeState format (non-blocking)
                    listState = GamingInputStateToList.ConvertGamingInputStateToList(giState);
                    break;
            }

            return listState;
        }

        /// <summary>
        /// Creates layout for device inputs using device capabilities as the source of truth.
        /// Displays live values from state if available, or default values if state is null.
        /// </summary>
        /// <param name="deviceInfo">Device information containing capability counts</param>
        /// <param name="inputStateAsList">Optional live state values (may be null if device offline)</param>
        /// <returns>UIElement containing the formatted layout</returns>
        private UIElement CreateInputLayout(UnifiedInputDeviceInfo deviceInfo, InputStateAsList inputStateAsList)
        {
            // Check if device has any inputs
            if (deviceInfo.AxeCount == 0 && deviceInfo.SliderCount == 0 && deviceInfo.ButtonCount == 0 && deviceInfo.KeyCount == 0 && deviceInfo.PovCount == 0)
                return null;

            var mainStackPanel = new StackPanel();

            // Create UI elements based on device capabilities, not state list length
            CreateInputGroup(mainStackPanel, "Axes", deviceInfo.AxeCount, inputStateAsList?.Axes);
            CreateInputGroup(mainStackPanel, "Sliders", deviceInfo.SliderCount, inputStateAsList?.Sliders);
            CreateInputGroup(mainStackPanel, "Buttons", deviceInfo.ButtonCount, inputStateAsList?.Buttons);
            CreateInputGroup(mainStackPanel, "Keys", deviceInfo.KeyCount, inputStateAsList?.Buttons);
            CreateInputGroup(mainStackPanel, "POVs", deviceInfo.PovCount, inputStateAsList?.POVs);

            return mainStackPanel;
        }

        public List<(Label, Label)> SelectedDeviceAxisLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceSliderLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceButtonLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDeviceKeyLabels = new List<(Label, Label)>();
        public List<(Label, Label)> SelectedDevicePovLabels = new List<(Label, Label)>();

        // Track the currently selected device interface path
        private string _currentSelectedDeviceInterfacePath;

        /// <summary>
        /// Sets the currently selected device interface path.
        /// Called when a device is selected in the UI.
        /// </summary>
        /// <param name="interfacePath">The interface path of the selected device</param>
        public void SetSelectedDevice(string interfacePath)
        {
            _currentSelectedDeviceInterfacePath = interfacePath;
        }

        SolidColorBrush colorActive = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF42C765");
        SolidColorBrush colorBackgroundDark = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFDEDEDE");

        /// <summary>
        /// Updates the value labels for the selected device with current state values.
        /// Only updates if the device matches the currently selected device.
        /// </summary>
        /// <param name="deviceInterfacePath">The interface path of the device being updated</param>
        /// <param name="listState">The current state values for the device</param>
        public void UpdateValueLabels(string deviceInterfacePath, InputStateAsList listState)
        {
            // Only update if this is the currently selected device
            if (string.IsNullOrEmpty(_currentSelectedDeviceInterfacePath) ||
                !string.Equals(_currentSelectedDeviceInterfacePath, deviceInterfacePath, StringComparison.OrdinalIgnoreCase))
                return;

            if (listState == null)
                return;

            // Update axes values
            if (listState.Axes != null && SelectedDeviceAxisLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Axes.Count, SelectedDeviceAxisLabels.Count); i++)
                {
                    SelectedDeviceAxisLabels[i].Item2.Content = listState.Axes[i].ToString();
                    //SelectedDeviceSliderLabels[i].Item1.Background = (listState.Axes[i] < 30000) || (listState.Axes[i] > 40000) ? colorActive : colorBackgroundDark;
                }
            }

            // Update slider values
            if (listState.Sliders != null && SelectedDeviceSliderLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Sliders.Count, SelectedDeviceSliderLabels.Count); i++)
                {
                    SelectedDeviceSliderLabels[i].Item2.Content = listState.Sliders[i].ToString();
                    SelectedDeviceSliderLabels[i].Item1.Background = listState.Sliders[i] > 10000 ? colorActive : colorBackgroundDark;
                }
            }

            // Update button values
            if (listState.Buttons != null && SelectedDeviceButtonLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Buttons.Count, SelectedDeviceButtonLabels.Count); i++)
                {
                    SelectedDeviceButtonLabels[i].Item2.Content = listState.Buttons[i].ToString();
                    SelectedDeviceButtonLabels[i].Item1.Background = listState.Buttons[i] > 0 ? colorActive : colorBackgroundDark;
                }
            }

            // Update key values (keys use the same button list)
            if (listState.Buttons != null && SelectedDeviceKeyLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.Buttons.Count, SelectedDeviceKeyLabels.Count); i++)
                {
                    SelectedDeviceKeyLabels[i].Item2.Content = listState.Buttons[i].ToString();
                    SelectedDeviceKeyLabels[i].Item1.Background = listState.Buttons[i] > 0 ? colorActive : colorBackgroundDark;
                }
            }

            // Update POV values
            if (listState.POVs != null && SelectedDevicePovLabels.Count > 0)
            {
                for (int i = 0; i < Math.Min(listState.POVs.Count, SelectedDevicePovLabels.Count); i++)
                {
                    SelectedDevicePovLabels[i].Item2.Content = listState.POVs[i].ToString();
                    SelectedDevicePovLabels[i].Item1.Background = listState.POVs[i] > -1 ? colorActive : colorBackgroundDark;
                }
            }
        }

        /// <summary>
        /// Creates a group of input elements (axes, sliders, buttons, or POVs).
        /// </summary>
        /// <param name="parentPanel">Parent panel to add the group to</param>
        /// <param name="groupName">Name of the input group</param>
        /// <param name="count">Number of inputs from device capabilities</param>
        /// <param name="values">Optional live values from device state</param>
        private void CreateInputGroup(StackPanel parentPanel, string groupName, int count, System.Collections.Generic.List<int> values)
        {
            if (count == 0)
                return;

            // Clear the appropriate list before creating new labels
            switch (groupName)
            {
                case "Axes":
                    SelectedDeviceAxisLabels.Clear();
                    break;
                case "Sliders":
                    SelectedDeviceSliderLabels.Clear();
                    break;
                case "Buttons":
                    SelectedDeviceButtonLabels.Clear();
                    break;
                case "Keys":
                    SelectedDeviceKeyLabels.Clear();
                    break;
                case "POVs":
                    SelectedDevicePovLabels.Clear();
                    break;
            }

            // GroupBox
            var groupBox = new GroupBox { Header = groupName };

            // UniformGrid
            var uniformGrid = new UniformGrid();

            for (int i = 0; i < count; i++)
            {
                // StackPanel
                var stackPanel = new StackPanel();

                // Input Label - shows zero-based index (0,1,2,3,4,5...)
                var inputLabel = new Label
                {
                    Content = i.ToString(),
                };
                stackPanel.Children.Add(inputLabel);

                // Value Label - shows live value or "N/A" if state unavailable
                string displayValue = "N/A";
                if (values != null && i < values.Count)
                {
                    displayValue = values[i].ToString();
                }

                var valueLabel = new Label
                {
                    IsHitTestVisible = false,
                    FontSize = 8,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    Content = displayValue,
                    Background = Brushes.Transparent,
                };
                stackPanel.Children.Add(valueLabel);
                uniformGrid.Children.Add(stackPanel);

                // Add the value label to the appropriate list
                switch (groupName)
                {
                    case "Axes":
                        SelectedDeviceAxisLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Sliders":
                        SelectedDeviceSliderLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Buttons":
                        SelectedDeviceButtonLabels.Add((inputLabel, valueLabel));
                        break;
                    case "Keys":
                        SelectedDeviceKeyLabels.Add((inputLabel, valueLabel));
                        break;
                    case "POVs":
                        SelectedDevicePovLabels.Add((inputLabel, valueLabel));
                        break;
                }
            }

            groupBox.Content = uniformGrid;
            parentPanel.Children.Add(groupBox);
        }
    }

}

