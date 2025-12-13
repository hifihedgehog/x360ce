using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using x360ce.Engine;

namespace x360ce.App.Controls
{
	/// <summary>
	/// Interaction logic for XboxImageControl.xaml
	/// </summary>
	public partial class PadItem_General_XboxImageControl : UserControl
	{
		public PadItem_General_XboxImageControl()
		{
			InitHelper.InitTimer(this, InitializeComponent);
		}

		PadControlImager _padControlImager;
		public List<ImageInfo> _imageInfoList;
		MapTo _mapTo;

		public void InitializeImages(List<ImageInfo> imageInfoList, PadControlImager padControlImager, MapTo mapTo)
		{
			_padControlImager = padControlImager;
			_imageInfoList = imageInfoList;
			_mapTo = mapTo;
			foreach (var imageInfo in imageInfoList)
			{
				imageInfo.Path = FindName(GetNameCode(imageInfo.Code).ToString()) as Path;
				var textBox = imageInfo.ControlBindedName as TextBox;
				if (textBox != null)
				{
					textBox.MouseEnter += (sender, e) => SetNormalOverActiveRecordColor(sender, colorOver);
					textBox.MouseLeave += (sender, e) => SetNormalOverActiveRecordColor(sender, colorNormalPath);
				}
				var path = imageInfo.Path;
				if (path != null)
				{
					path.MouseEnter += (sender, e) => SetNormalOverActiveRecordColor(sender, colorOver);
					path.MouseLeave += (sender, e) => SetNormalOverActiveRecordColor(sender, colorNormalPath);
					path.MouseUp += (sender, e) => SetNormalOverActiveRecordColor(sender, colorRecord);
				}
			}
			SetHelpText();
		}

		public MapCode GetNameCode(MapCode code)
		{
			switch (code)
			{
				case MapCode.LeftThumbAxisX: return MapCode.LeftThumbRight;
				case MapCode.LeftThumbAxisY: return MapCode.LeftThumbUp;
				case MapCode.RightThumbAxisX: return MapCode.RightThumbRight;
				case MapCode.RightThumbAxisY: return MapCode.RightThumbUp;
				default: return code;
			}
		}

		public Action<SettingsMapItem> StartRecording;
		public Func<bool> StopRecording;
		public string MappingDone = "Mapping Done";

		public void SetHelpText(string text = null)
		{
			HelpTextLabel.Content = text ?? "";
			if (string.IsNullOrEmpty(text))
				return;
			ControlsHelper.BeginInvoke(() =>
			{
				HelpTextLabel.Content = "";
			}, 4000);
		}

		public SolidColorBrush colorActive = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF42C765");
		public SolidColorBrush colorNormalPath = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF6699FF");
		public SolidColorBrush colorNormalTextBox = System.Windows.Media.Brushes.White;
		public SolidColorBrush colorOver = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFCC66");
		public SolidColorBrush colorRecord = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFF6B66");

		/// <summary>
		/// Set navigation color.
		/// </summary>
		public void SetNormalOverActiveRecordColor(object sender, SolidColorBrush setColor)
		{
			SolidColorBrush senderColor = colorNormalPath;
			if (sender is Path path)
				senderColor = path.Fill as SolidColorBrush;
			else if (sender is TextBox textBox)
				senderColor = textBox.Background as SolidColorBrush;
			else if (sender is ImageInfo imageInfo)
				senderColor = imageInfo.Path.Fill as SolidColorBrush;
			else if (sender is Button button)
			{
				senderColor = colorRecord;
				sender = button.Tag as TextBox;
			}
			// Only colorRecord can change colorRecord color.
			if (senderColor == colorRecord)
			{
				if (setColor == colorRecord)
					setColor = colorNormalPath;
				else
					return;
			}
			// Sender is ImageInfo. Set Active, Normal background color.
			if (sender is ImageInfo ii)
			{
				var senderPath = ii.Path;
				var senderText = ii.ControlBindedName as TextBox;
				if (senderColor.Color != colorRecord.Color && senderColor.Color != colorOver.Color)
				{
					senderText.Background = setColor == colorNormalPath ? colorNormalTextBox : setColor;
					senderPath.Fill = setColor;
				}
			}
			// Sender is Path, TextBox. Set Normal, Over, Record background color.
			else
			{
				if (_imageInfoList == null)
				{
					X360ControllerControl_Viewbox.Opacity = 0.3;
					X360ControllerControl_Viewbox.IsHitTestVisible = false;
				}
				else
				{
					X360ControllerControl_Viewbox.Opacity = 1;
					X360ControllerControl_Viewbox.IsHitTestVisible = true;
					foreach (var item in _imageInfoList)
					{
						if (sender == item.Path || sender == item.ControlBindedName)
						{
							item.Path.Fill = setColor;
							((TextBox)item.ControlBindedName).Background = (setColor.Color == colorNormalPath.Color) ? colorNormalTextBox : setColor;
						}
					}
				}
			}
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowLoad(this))
				return;
		}

		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			if (!ControlsHelper.AllowUnload(this))
				return;
			// Moved to MainBodyControl_Unloaded().
		}

		public void ParentWindow_Unloaded()
		{
			// MainGrid.MouseMove -= MainGrid_MouseMove;
			_imageInfoList?.Clear();
			_imageInfoList = null;
			_padControlImager = null;
		}

	}
}
