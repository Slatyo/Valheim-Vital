param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug','Release')]
    [System.String]$Target,

    [Parameter(Mandatory)]
    [System.String]$TargetPath,

    [Parameter(Mandatory)]
    [System.String]$TargetAssembly,

    [Parameter(Mandatory)]
    [System.String]$ValheimPath,

    [Parameter(Mandatory)]
    [System.String]$ProjectPath,

    [System.String]$DeployPath
)

# Make sure Get-Location is the script path
Push-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Path)

# Test some preliminaries
("$TargetPath",
 "$ValheimPath"
) | % {
    if (!(Test-Path "$_")) {Write-Error -ErrorAction Stop -Message "$_ folder is missing"}
}

# Plugin name without ".dll"
$name = "$TargetAssembly" -Replace('.dll')

# Create the mdb file if pdb2mdb exists
$pdb = "$TargetPath\$name.pdb"
$pdb2mdb = "$(Get-Location)\..\libraries\Debug\pdb2mdb.exe"
if ((Test-Path -Path "$pdb") -and (Test-Path -Path "$pdb2mdb")) {
    Write-Host "Create mdb file for plugin $name"
    Invoke-Expression "& `"$pdb2mdb`" `"$TargetPath\$TargetAssembly`""
}

# Main Script
Write-Host "Publishing for $Target from $TargetPath"

if ($Target.Equals("Debug")) {
    if ($DeployPath.Equals("")){
      $DeployPath = "$ValheimPath\BepInEx\plugins"
    }

    $plug = New-Item -Type Directory -Path "$DeployPath\$name" -Force
    Write-Host "Copy $TargetAssembly to $plug"
    Copy-Item -Path "$TargetPath\$name.dll" -Destination "$plug" -Force
    if (Test-Path -Path "$TargetPath\$name.pdb") {
        Copy-Item -Path "$TargetPath\$name.pdb" -Destination "$plug" -Force
    }
    if (Test-Path -Path "$TargetPath\$name.dll.mdb") {
        Copy-Item -Path "$TargetPath\$name.dll.mdb" -Destination "$plug" -Force
    }
    if (Test-Path -Path "$TargetPath\$name.xml") {
        Copy-Item -Path "$TargetPath\$name.xml" -Destination "$plug" -Force
    }
}

if($Target.Equals("Release")) {
    Write-Host "Packaging for ThunderStore..."
    $Package="Package"
    $PackagePath="$ProjectPath\$Package"

    Write-Host "$PackagePath\$TargetAssembly"
    New-Item -Type Directory -Path "$PackagePath\plugins" -Force
    Copy-Item -Path "$TargetPath\$TargetAssembly" -Destination "$PackagePath\plugins\$TargetAssembly" -Force
    if (Test-Path -Path "$ProjectPath\README.md") {
        Copy-Item -Path "$ProjectPath\README.md" -Destination "$PackagePath\README.md" -Force
    }
    Compress-Archive -Path "$PackagePath\*" -DestinationPath "$TargetPath\$TargetAssembly.zip" -Force
}

# Pop Location
Pop-Location
