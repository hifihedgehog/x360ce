# Convert Folder with SVG image files into XAML Resource file.
using namespace System;
using namespace System.IO;
using namespace System.Xml.Linq;
using namespace System.Text.RegularExpressions;

[Reflection.Assembly]::LoadWithPartialName("System.Xml.Linq") | Out-Null;

# ----------------------------------------------------------------------------
# Get current command path.
[string]$current = $MyInvocation.MyCommand.Path;
# Get calling command path.
[string]$calling = @(Get-PSCallStack)[1].InvocationInfo.MyCommand.Path;
# If executed directly then...
if ($calling -ne "") {
    $current = $calling;
}
# ----------------------------------------------------------------------------
[FileInfo]$file = New-Object FileInfo($current);
# Set public parameters.
$global:name = $file.Basename;
$global:path = $file.Directory.FullName;
# Change current directory.
[Console]::WriteLine("Path: {0}", $path);
[Environment]::CurrentDirectory = $path;
Set-Location $path;
# ----------------------------------------------------------------------------
[DirectoryInfo]$root = New-Object DirectoryInfo($path);
$dirs = $root.GetDirectories();
# ----------------------------------------------------------------------------
function RemoveAttributes
{
    param([XElement]$Node,[string]$Name);
    foreach ($attr in $Node.Attributes())
    {
        if ($attr.Name -eq $Name)
        {
            $attr.Remove();
        }
    }
    foreach ($child in $Node.Descendants())
    {
        RemoveAttributes -Node $child -Name $Name;
    }
}
# Create regular expressions for key and names generation.
$RxAllExceptNumbersAndLetters = New-Object Regex("[^a-zA-Z0-9]", [RegexOptions]::IgnoreCase);
$UsRx = New-Object Regex("_+");
# Inkscape program location, which will be used for conversion from SVG format to XAML format.
$inkscape = "d:\Program Files\Inkscape\bin\inkscape.exe";
for ($d = 0; $d -lt $dirs.Length; $d++) {
    $dir = $dirs[$d];
    # Get files.
    $files = $dir.GetFiles("*.svg");
    # If no SVG images found then skip.
    if ($files.Length -eq 0){
        continue;
    }
    # Crate output file name.
    $fileName = $RxAllExceptNumbersAndLetters.Replace($dir.Name, "_");
    $fileName = $UsRx.Replace($fileName, "_");
    $fileNameBase = "Icons_$($fileName)";
    $fileName = "$($fileNameBase).xaml";
    $fileNameCs = "$($fileNameBase).xaml.cs";
    Write-Host "${dir} - $($files.Length) images -> $fileName";
    # Start <ResourceName>.xaml file.
    if ([File]::Exists($fileName) -ne $true)
    {
        $xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        [File]::WriteAllText($fileName, "<ResourceDictionary xmlns=`"http://schemas.microsoft.com/winfx/2006/xaml/presentation`" xmlns:x=`"$xNs`"");
        [File]::AppendAllText($fileName,"`r`nx:Class=`"x360ce.App.$($fileNameBase)`"");
        [File]::AppendAllText($fileName,"`r`nx:ClassModifier=`"public`"");
        [File]::AppendAllText($fileName,'>');
        [File]::AppendAllText($fileName,"`r`n`r`n</ResourceDictionary>");
    }
    [XDocument]$xaml = [XDocument]::Load($fileName); 
    $xaml.Root.RemoveNodes();
    # Start <ResourceName>.xaml.cs file.
    [File]::WriteAllText($fileNameCs, "using System.Windows;`r`n");
    [File]::AppendAllText($fileNameCs, "`r`n");
    [File]::AppendAllText($fileNameCs, "namespace x360ce.App`r`n");
    [File]::AppendAllText($fileNameCs, "{`r`n");
    [File]::AppendAllText($fileNameCs, "`tpartial class Icons_Default : ResourceDictionary`r`n");
    [File]::AppendAllText($fileNameCs, "`t{`r`n");
    [File]::AppendAllText($fileNameCs, "`t`tpublic Icons_Default()`r`n");
    [File]::AppendAllText($fileNameCs, "`t`t{`r`n");
    [File]::AppendAllText($fileNameCs, "`t`t`tInitializeComponent();`r`n");
    [File]::AppendAllText($fileNameCs, "`t`t}`r`n");
    [File]::AppendAllText($fileNameCs, "`r`n");
    # Process files.
   for ($f = 0; $f -lt $files.Length; $f++) {
        $file = $files[$f];
        Write-Host "$($dir.Name)\$($file.Name)";
        #& $inkscape "$($file.FullName)" --export-filename="$path\$($file.BaseName).xaml";
        $nodeXml = Get-Content "$($file.FullName)" | & $inkscape --pipe --export-type=xaml | Out-String;
        # Remove name attributes.
        [XDocument]$node = [XDocument]::Parse($nodeXml);
        # Remove "Name" attributes.
        RemoveAttributes -Node $node.Root -Name "Name";
        # Create unique key.
        $key = "Icon_$($file.BaseName)";
        # Add image XML to XAML document.
        $xaml.Root.Add($node.Root);
        # Get node which was just added.
        $ln = $xaml.Root.LastNode;
        # Give node unique name.
        $ln.SetAttributeValue([XName]::Get("Key", $xNs), $key);
        # Make sure that image copy is made when it is used.
        $ln.SetAttributeValue([XName]::Get("Shared", $xNs), "False");
        # Write unique name to code file.
        [File]::AppendAllText($fileNameCs, "`t`tpublic const string $key = nameof($key);`r`n");
    }
    # Save XAML file.
    $xaml.Save($fileName);
    # End <ResourceName>.xaml.cs file.
    [File]::AppendAllText($fileNameCs, "`r`n");
    [File]::AppendAllText($fileNameCs, "`t}`r`n");
    [File]::AppendAllText($fileNameCs, "}`r`n");
}
