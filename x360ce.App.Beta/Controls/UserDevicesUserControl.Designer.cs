﻿namespace x360ce.App.Controls
{
	partial class UserDevicesUserControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UserDevicesUserControl));
			this.DevicesDataGridView = new System.Windows.Forms.DataGridView();
			this.IsOnlineColumn = new System.Windows.Forms.DataGridViewImageColumn();
			this.ConnectionClassColumn = new System.Windows.Forms.DataGridViewImageColumn();
			this.IsEnabledColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
			this.MySidColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.MyDeviceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.MyFileColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.DeviceIdColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.IsHiddenColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
			this.ControllersToolStrip = new System.Windows.Forms.ToolStrip();
			this.RefreshButton = new System.Windows.Forms.ToolStripButton();
			this.ControllerDeleteButton = new System.Windows.Forms.ToolStripButton();
			this.HardwareButton = new System.Windows.Forms.ToolStripButton();
			this.AddDemoDevice = new System.Windows.Forms.ToolStripButton();
			this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
			this.EnumeratedDevicesButton = new System.Windows.Forms.ToolStripMenuItem();
			this.HiddenDevicesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.UnhideAllDevicesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.synchronizeToHidGuardianToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ShowSystemDevicesButton = new System.Windows.Forms.ToolStripButton();
			((System.ComponentModel.ISupportInitialize)(this.DevicesDataGridView)).BeginInit();
			this.ControllersToolStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// DevicesDataGridView
			// 
			this.DevicesDataGridView.AllowUserToAddRows = false;
			this.DevicesDataGridView.AllowUserToDeleteRows = false;
			this.DevicesDataGridView.AllowUserToResizeRows = false;
			this.DevicesDataGridView.BackgroundColor = System.Drawing.Color.White;
			this.DevicesDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.DevicesDataGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
			dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
			this.DevicesDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
			this.DevicesDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.DevicesDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.IsOnlineColumn,
            this.ConnectionClassColumn,
            this.IsEnabledColumn,
            this.MySidColumn,
            this.MyDeviceColumn,
            this.MyFileColumn,
            this.DeviceIdColumn,
            this.IsHiddenColumn});
			dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
			dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
			dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
			this.DevicesDataGridView.DefaultCellStyle = dataGridViewCellStyle5;
			this.DevicesDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.DevicesDataGridView.GridColor = System.Drawing.SystemColors.ControlLight;
			this.DevicesDataGridView.Location = new System.Drawing.Point(0, 50);
			this.DevicesDataGridView.Margin = new System.Windows.Forms.Padding(6);
			this.DevicesDataGridView.Name = "DevicesDataGridView";
			this.DevicesDataGridView.ReadOnly = true;
			dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle6.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle6.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
			this.DevicesDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle6;
			this.DevicesDataGridView.RowHeadersVisible = false;
			this.DevicesDataGridView.RowHeadersWidth = 82;
			this.DevicesDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.DevicesDataGridView.Size = new System.Drawing.Size(1434, 788);
			this.DevicesDataGridView.TabIndex = 0;
			this.DevicesDataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DevicesDataGridView_CellClick);
			this.DevicesDataGridView.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.DevicesDataGridView_CellFormatting);
			this.DevicesDataGridView.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.DevicesDataGridView_DataError);
			// 
			// IsOnlineColumn
			// 
			this.IsOnlineColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.IsOnlineColumn.DataPropertyName = "IsOnline";
			dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
			dataGridViewCellStyle2.NullValue = null;
			this.IsOnlineColumn.DefaultCellStyle = dataGridViewCellStyle2;
			this.IsOnlineColumn.HeaderText = "";
			this.IsOnlineColumn.MinimumWidth = 24;
			this.IsOnlineColumn.Name = "IsOnlineColumn";
			this.IsOnlineColumn.ReadOnly = true;
			this.IsOnlineColumn.Width = 24;
			// 
			// ConnectionClassColumn
			// 
			this.ConnectionClassColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.ConnectionClassColumn.DataPropertyName = "ConnectionClass";
			dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
			this.ConnectionClassColumn.DefaultCellStyle = dataGridViewCellStyle3;
			this.ConnectionClassColumn.HeaderText = "";
			this.ConnectionClassColumn.MinimumWidth = 24;
			this.ConnectionClassColumn.Name = "ConnectionClassColumn";
			this.ConnectionClassColumn.ReadOnly = true;
			this.ConnectionClassColumn.Width = 24;
			// 
			// IsEnabledColumn
			// 
			this.IsEnabledColumn.DataPropertyName = "IsEnabled";
			this.IsEnabledColumn.HeaderText = "";
			this.IsEnabledColumn.MinimumWidth = 10;
			this.IsEnabledColumn.Name = "IsEnabledColumn";
			this.IsEnabledColumn.ReadOnly = true;
			this.IsEnabledColumn.Width = 24;
			// 
			// MySidColumn
			// 
			this.MySidColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.MySidColumn.DataPropertyName = "InstanceId";
			dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
			this.MySidColumn.DefaultCellStyle = dataGridViewCellStyle4;
			this.MySidColumn.HeaderText = "Instance ID";
			this.MySidColumn.MinimumWidth = 10;
			this.MySidColumn.Name = "MySidColumn";
			this.MySidColumn.ReadOnly = true;
			this.MySidColumn.Width = 154;
			// 
			// MyDeviceColumn
			// 
			this.MyDeviceColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.MyDeviceColumn.DataPropertyName = "HidManufacturer";
			this.MyDeviceColumn.HeaderText = "Vendor Name";
			this.MyDeviceColumn.MinimumWidth = 10;
			this.MyDeviceColumn.Name = "MyDeviceColumn";
			this.MyDeviceColumn.ReadOnly = true;
			this.MyDeviceColumn.Width = 177;
			// 
			// MyFileColumn
			// 
			this.MyFileColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.MyFileColumn.DataPropertyName = "ProductName";
			this.MyFileColumn.HeaderText = "Product Name";
			this.MyFileColumn.MinimumWidth = 10;
			this.MyFileColumn.Name = "MyFileColumn";
			this.MyFileColumn.ReadOnly = true;
			this.MyFileColumn.Width = 181;
			// 
			// DeviceIdColumn
			// 
			this.DeviceIdColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
			this.DeviceIdColumn.DataPropertyName = "HidDeviceId";
			this.DeviceIdColumn.HeaderText = "HID Device ID (HID Guardian)";
			this.DeviceIdColumn.MinimumWidth = 10;
			this.DeviceIdColumn.Name = "DeviceIdColumn";
			this.DeviceIdColumn.ReadOnly = true;
			// 
			// IsHiddenColumn
			// 
			this.IsHiddenColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
			this.IsHiddenColumn.DataPropertyName = "IsHidden";
			this.IsHiddenColumn.HeaderText = "Hide";
			this.IsHiddenColumn.MinimumWidth = 10;
			this.IsHiddenColumn.Name = "IsHiddenColumn";
			this.IsHiddenColumn.ReadOnly = true;
			this.IsHiddenColumn.Width = 63;
			// 
			// ControllersToolStrip
			// 
			this.ControllersToolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
			this.ControllersToolStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
			this.ControllersToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.RefreshButton,
            this.ControllerDeleteButton,
            this.HardwareButton,
            this.AddDemoDevice,
            this.toolStripDropDownButton1,
            this.ShowSystemDevicesButton});
			this.ControllersToolStrip.Location = new System.Drawing.Point(0, 0);
			this.ControllersToolStrip.Name = "ControllersToolStrip";
			this.ControllersToolStrip.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
			this.ControllersToolStrip.Size = new System.Drawing.Size(1434, 50);
			this.ControllersToolStrip.TabIndex = 1;
			this.ControllersToolStrip.Text = "MySettingsToolStrip";
			// 
			// RefreshButton
			// 
			this.RefreshButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.RefreshButton.Image = global::x360ce.App.Properties.Resources.refresh_16x16;
			this.RefreshButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.RefreshButton.Name = "RefreshButton";
			this.RefreshButton.Size = new System.Drawing.Size(130, 44);
			this.RefreshButton.Text = "&Refresh";
			this.RefreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
			// 
			// ControllerDeleteButton
			// 
			this.ControllerDeleteButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
			this.ControllerDeleteButton.Image = global::x360ce.App.Properties.Resources.delete_16x16;
			this.ControllerDeleteButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.ControllerDeleteButton.Name = "ControllerDeleteButton";
			this.ControllerDeleteButton.Size = new System.Drawing.Size(121, 44);
			this.ControllerDeleteButton.Text = "&Delete";
			this.ControllerDeleteButton.Click += new System.EventHandler(this.ControllerDeleteButton_Click);
			// 
			// HardwareButton
			// 
			this.HardwareButton.Image = global::x360ce.App.Properties.Resources.hardware_16x16;
			this.HardwareButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.HardwareButton.Name = "HardwareButton";
			this.HardwareButton.Size = new System.Drawing.Size(167, 44);
			this.HardwareButton.Text = "Hardware...";
			this.HardwareButton.Click += new System.EventHandler(this.HardwareButton_Click);
			// 
			// AddDemoDevice
			// 
			this.AddDemoDevice.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
			this.AddDemoDevice.Image = ((System.Drawing.Image)(resources.GetObject("AddDemoDevice.Image")));
			this.AddDemoDevice.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.AddDemoDevice.Name = "AddDemoDevice";
			this.AddDemoDevice.Size = new System.Drawing.Size(213, 44);
			this.AddDemoDevice.Text = "Add Demo Device";
			this.AddDemoDevice.Click += new System.EventHandler(this.AddDemoDevice_Click);
			// 
			// toolStripDropDownButton1
			// 
			this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
			this.toolStripDropDownButton1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.EnumeratedDevicesButton,
            this.HiddenDevicesMenuItem,
            this.UnhideAllDevicesMenuItem,
            this.synchronizeToHidGuardianToolStripMenuItem});
			this.toolStripDropDownButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButton1.Image")));
			this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
			this.toolStripDropDownButton1.Size = new System.Drawing.Size(195, 44);
			this.toolStripDropDownButton1.Text = "HID Guardian...";
			// 
			// EnumeratedDevicesButton
			// 
			this.EnumeratedDevicesButton.Name = "EnumeratedDevicesButton";
			this.EnumeratedDevicesButton.Size = new System.Drawing.Size(460, 44);
			this.EnumeratedDevicesButton.Text = "Show Enumerated Devices";
			this.EnumeratedDevicesButton.Click += new System.EventHandler(this.ShowEnumeratedDevicesMenuItem_Click);
			// 
			// HiddenDevicesMenuItem
			// 
			this.HiddenDevicesMenuItem.Name = "HiddenDevicesMenuItem";
			this.HiddenDevicesMenuItem.Size = new System.Drawing.Size(460, 44);
			this.HiddenDevicesMenuItem.Text = "Show Hidden Devices";
			this.HiddenDevicesMenuItem.Click += new System.EventHandler(this.ShowHiddenDevicesMenuItem_Click);
			// 
			// UnhideAllDevicesMenuItem
			// 
			this.UnhideAllDevicesMenuItem.Name = "UnhideAllDevicesMenuItem";
			this.UnhideAllDevicesMenuItem.Size = new System.Drawing.Size(460, 44);
			this.UnhideAllDevicesMenuItem.Text = "Unhide All Devices";
			this.UnhideAllDevicesMenuItem.Click += new System.EventHandler(this.UnhideAllDevicesMenuItem_Click);
			// 
			// synchronizeToHidGuardianToolStripMenuItem
			// 
			this.synchronizeToHidGuardianToolStripMenuItem.Name = "synchronizeToHidGuardianToolStripMenuItem";
			this.synchronizeToHidGuardianToolStripMenuItem.Size = new System.Drawing.Size(460, 44);
			this.synchronizeToHidGuardianToolStripMenuItem.Text = "Synchronize To HID Guardian";
			this.synchronizeToHidGuardianToolStripMenuItem.Click += new System.EventHandler(this.synchronizeToHidGuardianToolStripMenuItem_Click);
			// 
			// ShowSystemDevicesButton
			// 
			this.ShowSystemDevicesButton.Image = global::x360ce.App.Properties.Resources.checkbox_unchecked_16x16;
			this.ShowSystemDevicesButton.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.ShowSystemDevicesButton.Name = "ShowSystemDevicesButton";
			this.ShowSystemDevicesButton.Size = new System.Drawing.Size(281, 44);
			this.ShowSystemDevicesButton.Text = "Show System Devices";
			this.ShowSystemDevicesButton.Click += new System.EventHandler(this.ShowSystemDevicesButton_Click);
			// 
			// UserDevicesUserControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.DevicesDataGridView);
			this.Controls.Add(this.ControllersToolStrip);
			this.Margin = new System.Windows.Forms.Padding(6);
			this.Name = "UserDevicesUserControl";
			this.Size = new System.Drawing.Size(1434, 838);
			this.Load += new System.EventHandler(this.ControllersUserControl_Load);
			((System.ComponentModel.ISupportInitialize)(this.DevicesDataGridView)).EndInit();
			this.ControllersToolStrip.ResumeLayout(false);
			this.ControllersToolStrip.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.DataGridView DevicesDataGridView;
		private System.Windows.Forms.ToolStrip ControllersToolStrip;
		private System.Windows.Forms.ToolStripButton ControllerDeleteButton;
		private System.Windows.Forms.ToolStripButton RefreshButton;
		private System.Windows.Forms.ToolStripButton HardwareButton;
		private System.Windows.Forms.ToolStripButton AddDemoDevice;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem EnumeratedDevicesButton;
        private System.Windows.Forms.ToolStripMenuItem HiddenDevicesMenuItem;
        private System.Windows.Forms.ToolStripMenuItem UnhideAllDevicesMenuItem;
		private System.Windows.Forms.ToolStripMenuItem synchronizeToHidGuardianToolStripMenuItem;
		private System.Windows.Forms.DataGridViewImageColumn IsOnlineColumn;
		private System.Windows.Forms.DataGridViewImageColumn ConnectionClassColumn;
		private System.Windows.Forms.DataGridViewCheckBoxColumn IsEnabledColumn;
		private System.Windows.Forms.DataGridViewTextBoxColumn MySidColumn;
		private System.Windows.Forms.DataGridViewTextBoxColumn MyDeviceColumn;
		private System.Windows.Forms.DataGridViewTextBoxColumn MyFileColumn;
		private System.Windows.Forms.DataGridViewTextBoxColumn DeviceIdColumn;
		private System.Windows.Forms.DataGridViewCheckBoxColumn IsHiddenColumn;
		private System.Windows.Forms.ToolStripButton ShowSystemDevicesButton;
	}
}
