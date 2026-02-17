using JocysCom.ClassLibrary.IO;
using JocysCom.ClassLibrary.Win32;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace x360ce.App.DInput
{
	public class VirtualDriverInstaller
	{

		#region Install/Uninstall ViGEmBus

		static Guid GUID_DEVINTERFACE_BUSENUM_VIGEM = new Guid("96E42B22-F5E9-42F8-B043-ED0F932F014F");
		public static SP_DRVINFO_DATA GetViGemBusDriverInfo()
		{
			var flags = DIGCF.DIGCF_PRESENT | DIGCF.DIGCF_DEVICEINTERFACE;
			var driver = DeviceDetector.GetDrivers(GUID_DEVINTERFACE_BUSENUM_VIGEM, flags).FirstOrDefault();
			return driver;
		}

		public static string[] ViGEmBusHardwareIds = { "Root\\ViGEmBus", "Nefarius\\ViGEmBus\\Gen1" };

		/// <summary>
		/// Name of the embedded ViGEmBus installer resource.
		/// </summary>
		private const string ViGEmBusInstallerResourceName = "ViGEmBus_1.22.0_x64_x86_arm64.exe";

		/// <summary>
		/// Get the temp folder used for ViGEmBus installer extraction.
		/// </summary>
		private static string GetViGEmBusTempDir()
		{
			return Path.Combine(Path.GetTempPath(), "x360ce_ViGEmBus");
		}

		/// <summary>
		/// Extract the embedded ViGEmBus bootstrapper EXE to a temp folder.
		/// Returns the full path to the extracted .exe.
		/// </summary>
		private static string ExtractViGEmBusBootstrapper()
		{
			return ExtractEmbeddedInstaller(ViGEmBusInstallerResourceName, GetViGEmBusTempDir());
		}

		/// <summary>
		/// Install ViGEmBus virtual controller driver.
		/// Extracts the embedded bootstrapper, unpacks the MSI,
		/// and runs msiexec /i silently.
		/// </summary>
		/// <remarks>Must be executed in administrative mode.</remarks>
		public static void InstallViGEmBus(ProcessWindowStyle style = ProcessWindowStyle.Hidden)
		{
			try
			{
				var exePath = ExtractViGEmBusBootstrapper();
				var extractDir = ExtractInstallerBundle(exePath, GetViGEmBusTempDir());
				var msiPath = FindMsi(extractDir,
					Environment.Is64BitOperatingSystem ? "ViGEmBus.x64.msi" : "ViGEmBus.msi",
					"ViGEmBus*.msi");

				var quietFlag = (style == ProcessWindowStyle.Hidden) ? "/qn" : "/qb";
				var arguments = string.Format("/i \"{0}\" {1} /norestart", msiPath, quietFlag);

				UacHelper.RunElevated("msiexec.exe", arguments, style, true);
			}
			finally
			{
				CleanupTempDir(GetViGEmBusTempDir());
			}
		}

		/// <summary>
		/// Uninstall ViGEmBus virtual controller driver.
		/// </summary>
		/// <remarks>Must be executed in administrative mode.</remarks>
		public static void UninstallViGEmBus(ProcessWindowStyle style = ProcessWindowStyle.Hidden)
		{
			try
			{
				var exePath = ExtractViGEmBusBootstrapper();
				var extractDir = ExtractInstallerBundle(exePath, GetViGEmBusTempDir());
				var msiPath = FindMsi(extractDir,
					Environment.Is64BitOperatingSystem ? "ViGEmBus.x64.msi" : "ViGEmBus.msi",
					"ViGEmBus*.msi");

				var quietFlag = (style == ProcessWindowStyle.Hidden) ? "/qn" : "/qb";
				var arguments = string.Format("/x \"{0}\" {1} /norestart", msiPath, quietFlag);

				UacHelper.RunElevated("msiexec.exe", arguments, style, true);
			}
			finally
			{
				CleanupTempDir(GetViGEmBusTempDir());
			}
		}

		#endregion

		#region Install/Uninstall HidHide

		/// <summary>
		/// Name of the embedded HidHide installer resource.
		/// </summary>
		private const string HidHideInstallerResourceName = "HidHide_1.5.230_x64.exe";

		/// <summary>
		/// Get the temp folder used for HidHide installer extraction.
		/// </summary>
		private static string GetHidHideTempDir()
		{
			return Path.Combine(Path.GetTempPath(), "x360ce_HidHide");
		}

		/// <summary>
		/// Extract the embedded HidHide bootstrapper EXE to a temp folder.
		/// Returns the full path to the extracted .exe.
		/// </summary>
		private static string ExtractHidHideBootstrapper()
		{
			return ExtractEmbeddedInstaller(HidHideInstallerResourceName, GetHidHideTempDir());
		}

		// ---------- HidHide MSI detection (authoritative for UI state) ----------

		private static bool TryGetHidHideMsiInfo(out string displayVersion, out string productCode)
		{
			displayVersion = null;
			productCode = null;

			// HidHide MSI is 64-bit on most systems; if x360ce runs 32-bit,
			// you MUST check both registry views.
			var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };

			foreach (var view in views)
			{
				try
				{
					using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
					using (var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false))
					{
						if (uninstallKey == null)
							continue;

						foreach (var subName in uninstallKey.GetSubKeyNames())
						{
							using (var sub = uninstallKey.OpenSubKey(subName, false))
							{
								var name = sub?.GetValue("DisplayName") as string;
								if (string.IsNullOrEmpty(name))
									continue;

								// Be flexible: "HidHide", "HID Hide", etc.
								if (name.IndexOf("HidHide", StringComparison.OrdinalIgnoreCase) < 0 &&
									name.IndexOf("HID Hide", StringComparison.OrdinalIgnoreCase) < 0)
									continue;

								displayVersion = sub.GetValue("DisplayVersion") as string;

								// Often the subkey name is the ProductCode GUID for MSI installs.
								// If it isn't, we'll still return installed=true but productCode may be null.
								if (subName.StartsWith("{") && subName.EndsWith("}"))
									productCode = subName;

								return true;
							}
						}
					}
				}
				catch
				{
					// ignore and try other view
				}
			}

			return false;
		}

		private static bool IsHidHideDriverPresent()
		{
			try
			{
				var driverPath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.System),
					"drivers", "HidHide.sys");
				if (File.Exists(driverPath))
					return true;
			}
			catch { }

			try
			{
				using (var key = Registry.LocalMachine.OpenSubKey(
					@"SYSTEM\CurrentControlSet\Services\HidHide", false))
				{
					return key != null;
				}
			}
			catch { }

			return false;
		}

		/// <summary>
		/// “Installed” = HidHide MSI product is installed (appears in installed programs).
		/// </summary>
		public static bool IsHidHideInstalled()
		{
			return TryGetHidHideMsiInfo(out _, out _);
		}

		/// <summary>
		/// Returns MSI DisplayVersion if installed, else null.
		/// </summary>
		public static string GetHidHideVersion()
		{
			if (!TryGetHidHideMsiInfo(out var v, out _))
				return null;
			return string.IsNullOrEmpty(v) ? "Installed" : v;
		}

		/// <summary>
		/// Text shown in UI.
		/// Also warns if driver remnants exist but MSI is not installed.
		/// </summary>
		public static string GetHidHideStatusText()
		{
			var msiInstalled = TryGetHidHideMsiInfo(out var v, out _);
			if (msiInstalled)
				return $"HidHide {(!string.IsNullOrEmpty(v) ? v : "Installed")}";

			// Not installed from MSI perspective — but remnants may still exist.
			if (IsHidHideDriverPresent())
				return "Not installed (remnants detected: HidHide driver/service still present)";

			return "Not installed";
		}

		/// <summary>
		/// Install HidHide device-hiding driver.
		/// Extracts the embedded bootstrapper, unpacks the MSI,
		/// and runs msiexec /i silently.
		/// </summary>
		/// <remarks>Must be executed in administrative mode.</remarks>
		public static void InstallHidHide(ProcessWindowStyle style = ProcessWindowStyle.Hidden)
		{
			try
			{
				var exePath = ExtractHidHideBootstrapper();
				var extractDir = ExtractInstallerBundle(exePath, GetHidHideTempDir());
				var msiPath = FindMsi(extractDir, "HidHide.msi", "HidHide*.msi");

				var quietFlag = (style == ProcessWindowStyle.Hidden) ? "/qn" : "/qb";
				var arguments = string.Format("/i \"{0}\" {1} /norestart", msiPath, quietFlag);

				UacHelper.RunElevated("msiexec.exe", arguments, style, true);
			}
			finally
			{
				CleanupTempDir(GetHidHideTempDir());
			}
		}

		/// <summary>
		/// Uninstall HidHide device-hiding driver.
		/// Extracts the embedded bootstrapper, unpacks the MSI,
		/// and runs msiexec /x silently.
		/// </summary>
		/// <remarks>Must be executed in administrative mode.</remarks>
		public static void UninstallHidHide(ProcessWindowStyle style = ProcessWindowStyle.Hidden)
		{
			try
			{
				var exePath = ExtractHidHideBootstrapper();
				var extractDir = ExtractInstallerBundle(exePath, GetHidHideTempDir());
				var msiPath = FindMsi(extractDir, "HidHide.msi", "HidHide*.msi");

				var quietFlag = (style == ProcessWindowStyle.Hidden) ? "/qn" : "/qb";
				var arguments = string.Format("/x \"{0}\" {1} /norestart", msiPath, quietFlag);

				UacHelper.RunElevated("msiexec.exe", arguments, style, true);
			}
			finally
			{
				CleanupTempDir(GetHidHideTempDir());
			}
		}

		#endregion

		#region Shared Installer Helpers

		/// <summary>
		/// Extract an embedded installer resource to a temp directory.
		/// Returns the full path to the extracted file.
		/// </summary>
		private static string ExtractEmbeddedInstaller(string resourceFileName, string tempDir)
		{
			var assembly = Assembly.GetEntryAssembly();
			var resourceNames = assembly.GetManifestResourceNames();

			var resourceName = resourceNames.FirstOrDefault(
				x => x.IndexOf(resourceFileName, StringComparison.OrdinalIgnoreCase) >= 0);

			if (string.IsNullOrEmpty(resourceName))
				throw new FileNotFoundException(
					string.Format("Embedded resource '{0}' not found. Available: {1}",
						resourceFileName,
						string.Join(", ", resourceNames)));

			Directory.CreateDirectory(tempDir);

			var tempPath = Path.Combine(tempDir, resourceFileName);

			using (var stream = assembly.GetManifestResourceStream(resourceName))
			{
				using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
				{
					stream.CopyTo(fs);
				}
			}

			return tempPath;
		}

		/// <summary>
		/// Run the bootstrapper's /extract to unpack the MSI and supporting
		/// files. Returns the path to the extraction folder.
		/// The target folder must exist before /extract is called.
		/// </summary>
		private static string ExtractInstallerBundle(string exePath, string tempDir)
		{
			var extractDir = Path.Combine(tempDir, "Extracted");

			// Clean any previous extraction.
			if (Directory.Exists(extractDir))
				Directory.Delete(extractDir, true);

			// /extract requires the target folder to already exist.
			Directory.CreateDirectory(extractDir);

			var psi = new ProcessStartInfo
			{
				FileName = exePath,
				Arguments = string.Format("/extract \"{0}\"", extractDir),
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using (var proc = Process.Start(psi))
			{
				proc.WaitForExit(60000);
			}

			return extractDir;
		}

		/// <summary>
		/// Find an MSI file in the extracted bundle contents.
		/// The bootstrapper extracts into a randomly-named subdirectory.
		/// </summary>
		private static string FindMsi(string extractDir, string primaryName, string fallbackPattern)
		{
			// Search for the primary MSI name first.
			var files = Directory.GetFiles(extractDir, primaryName, SearchOption.AllDirectories);
			if (files.Length > 0)
				return files[0];

			// Fallback: any matching MSI.
			files = Directory.GetFiles(extractDir, fallbackPattern, SearchOption.AllDirectories);
			if (files.Length > 0)
				return files[0];

			throw new FileNotFoundException(
				string.Format("Could not find {0} in extracted bundle at '{1}'.",
					primaryName, extractDir));
		}

		/// <summary>
		/// Clean up a temp directory.
		/// </summary>
		private static void CleanupTempDir(string tempDir)
		{
			try
			{
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, true);
			}
			catch { }
		}

		#endregion

	}
}
