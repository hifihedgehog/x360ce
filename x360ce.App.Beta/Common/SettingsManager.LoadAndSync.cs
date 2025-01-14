﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;
using x360ce.Engine;

namespace x360ce.App
{

	/// <summary>
	/// region Load, Monitor and Sync settings between controls and properties.
	/// </summary>
	public partial class SettingsManager
	{

		private static void Property_Changed(object sender, PropertyChangedEventArgs e)
			// Update control from property.
			=> Sync(Options, e.PropertyName);

		private static void Form_Control_Changed(object sender, EventArgs e)
			// Update property from control.
			=> Sync(sender, Options);

		private static void Windows_Control_Changed(object sender, System.Windows.RoutedEventArgs e)
			// Update property from control.
			=> Sync(sender, Options);

		private static object LoadAndSyncLock = new object();
		private static bool IsOptionsPropertyChangedEnabled;

		public static void LoadAndMonitor(INotifyPropertyChanged source, string sourceProperty, System.Windows.Controls.Control control, System.Windows.DependencyProperty controlProperty = null)
		{
			if (controlProperty == null)
			{
				if (control is System.Windows.Controls.CheckBox)
					controlProperty = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty;
				if (control is System.Windows.Controls.ComboBox)
					controlProperty = System.Windows.Controls.Primitives.Selector.SelectedValueProperty;
			}
			var binding = new System.Windows.Data.Binding(sourceProperty);
			binding.Source = source;
			binding.IsAsync = true;
			control.SetBinding(controlProperty, binding);
		}

		public static void LoadAndMonitor(INotifyPropertyChanged source, string sourceProperty, System.Windows.Forms.Control control, string controlProperty = null)
		{
			ConvertEventHandler format = null;
			ConvertEventHandler parse = null;
			var pi = source.GetType().GetProperty(sourceProperty);
			if (controlProperty == null)
			{
				if (control is System.Windows.Forms.CheckBox)
				{
					if (pi.PropertyType == typeof(EnabledState))
					{
						controlProperty = nameof(System.Windows.Forms.CheckBox.CheckState);
						// Convert to control type.
						format = (sender, e) =>
						{
							var value = (EnabledState)e.Value;
							e.Value = value == EnabledState.None
								? CheckState.Indeterminate
								: value == EnabledState.Enabled
									? CheckState.Checked
									: CheckState.Unchecked;
						};
						// Convert to source type.
						parse = (sender, e) =>
						{
							var value = (CheckState)e.Value;
							e.Value = value == CheckState.Indeterminate
								? EnabledState.None
								: value == CheckState.Checked
									? EnabledState.Enabled
									: EnabledState.Disabled;

						};
					}
					else
					{
						controlProperty = nameof(System.Windows.Forms.CheckBox.Checked);
					}
				}
			}
			var binding = new System.Windows.Forms.Binding(controlProperty, source, sourceProperty);
			binding.DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged;
			binding.ControlUpdateMode = ControlUpdateMode.OnPropertyChanged;
			if (parse != null)
			{
				binding.FormattingEnabled = true;
				binding.Format += format;
				binding.Parse += parse;
			}
			control.DataBindings.Add(binding);
		}

		public static void LoadAndMonitor(Expression<Func<Options, object>> setting, object control, object dataSource = null)
		{
			var o = Options;
			lock (LoadAndSyncLock)
			{
				// If not monitoring changes of Options then...
				if (!IsOptionsPropertyChangedEnabled)
				{
					// Enable monitoring.
					o.PropertyChanged += Property_Changed;
					IsOptionsPropertyChangedEnabled = true;
				}
			}
			//// Add control to maps.
			//if (control is Control c)
			AddMap(setting, control);
			// Load settings into control.
			var body = (setting.Body as MemberExpression)
				 ?? (((UnaryExpression)setting.Body).Operand as MemberExpression);
			var propertyName = body.Member.Name;
			// Attach list of possible values.
			// Load property value into control.
			Sync(Options, propertyName);
			if (control is ListControl lc)
			{
				if (dataSource != null)
					lc.DataSource = dataSource;
			}
			else if (control is ListBox lb)
			{
				if (dataSource != null)
					lb.DataSource = dataSource;
			}
			// Attach monitoring events.
			else if (control is CheckBox chb)
			{
				chb.CheckStateChanged += Form_Control_Changed;
			}
			else if (control is ComboBox cob)
			{
				cob.TextChanged += Form_Control_Changed;
				cob.SelectedIndexChanged += Form_Control_Changed;
			}
			else if (control is TextBox txb)
			{
				txb.TextChanged += Form_Control_Changed;
			}
			else if (control is NumericUpDown nud)
			{
				nud.ValueChanged += Form_Control_Changed;
			}
			else if (control is System.Windows.Controls.CheckBox wcCheckBox)
			{
				wcCheckBox.Checked += Windows_Control_Changed;
			}
			else
			{
				throw new Exception(string.Format("Type '{0}' not implemented", control.GetType().FullName));
			}

			// This will trigger update of control from the property.
			// Set ComboBox and attach event later, in order to prevent changing of original value.
			//JocysCom.ClassLibrary.Controls.ControlsHelper.BeginInvoke(() => {
			//	o.OnPropertyChanged(propertyName);
			//});

		}

		/// <summary>
		/// Set property value from control if different.
		/// </summary>
		/// <param name="control"></param>
		/// <param name="destination"></param>
		public static void Sync(object control, object destination)
		{
			var map = Current.SettingsMap.FirstOrDefault(x => x.Control == control);
			if (map == null)
				return;
			var pi = map.Property;
			var oldValue = pi.GetValue(destination, null);
			// Update properties from various controls.
			object newValue = null;
			if (map.Control is TextBox textBox)
			{
				newValue = textBox.Text;
			}
			else if (control is CheckBox checkBox)
			{
				newValue = checkBox.Checked;
				if (pi.PropertyType == typeof(EnabledState))
				{
					// If CheckBox is in third state then...
					newValue = checkBox.CheckState == CheckState.Indeterminate
						? EnabledState.None
						: checkBox.Checked ? EnabledState.Enabled : EnabledState.Disabled;
				}
			}
			else if (map.Control is ComboBox comboBox)
			{
				newValue = pi.PropertyType == typeof(string)
					? comboBox.Text : comboBox.SelectedItem;
			}
			else
			{
				throw new Exception(string.Format("Type '{0}' not implemented", control.GetType().FullName));
			}
			if (!Equals(oldValue, newValue))
				pi.SetValue(destination, newValue, null);
		}

		/// <summary>
		/// Set control value from property if different.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="destination"></param>
		public static void Sync(object source, string propertyName)
		{
			var map = Current.SettingsMap.FirstOrDefault(x => x.Property.Name == propertyName);
			if (map == null)
				return;
			var propValue = map.Property.GetValue(source, null);
			// Update Control from property.
			if (map.Control is TextBox textBox)
			{
				var value = string.Format("{0}", propValue);
				if (!Equals(textBox.Text, value))
					textBox.Text = value;
			}
			else if (map.Control is CheckBox checkBox)
			{
				if (map.Property.PropertyType == typeof(EnabledState))
				{
					var value = (EnabledState)propValue;
					var checkState = CheckState.Indeterminate;
					if (value == EnabledState.Enabled)
						checkState = CheckState.Checked;
					if (value == EnabledState.Disabled)
						checkState = CheckState.Unchecked;
					if (!Equals(checkBox.CheckState, checkState))
						checkBox.CheckState = checkState;
				}
				else
				{
					if (!Equals(checkBox.Checked, propValue))
						checkBox.Checked = (bool)propValue;
				}
			}
			else if (map.Control is ComboBox comboBox)
			{
				if (map.Property.PropertyType == typeof(string))
				{
					var value = string.Format("{0}", propValue);
					if (!Equals(comboBox.Text, value))
						comboBox.Text = value;
				}
				else
				{
					if (!Equals(comboBox.SelectedItem, propValue))
						comboBox.SelectedItem = propValue;
				}
				return;
			}
			else if (map.Control is ListBox lbx)
			{
				if (!Equals(lbx.DataSource, propValue))
					lbx.DataSource = propValue;
			}
			else if (map.Control is NumericUpDown nud)
			{
				var newValue = Convert.ToDecimal(propValue);
				if (!Equals(nud.Value, newValue))
					nud.Value = newValue;
			}
			else
			{
				throw new Exception(string.Format("Type '{0}' not implemented", map.Control.GetType().FullName));
			}
		}

	}
}
