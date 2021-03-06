# Go through each directory in the current directory and build the nuget package for it
# The user will be prompted for what version number to use

function Get-Directory([string[]]$path, [string[]]$include, [switch]$recurse) 
{ 
    Get-ChildItem -Path $path -Include $include -Recurse:$recurse | `
         Where-Object { $_.PSIsContainer } 
} 

Write-Host "This will build all of the nuget packages in this directory"
Write-Host ""

$cur_directory = $(Get-Location)
$version_holder = "<version></version>"
$notes_holder = "<releaseNotes></releaseNotes>"
$new_version = Read-Host "What version number should we use? (e.g. 1.0.0.3)"

$new_notes = Read-Host "Any release notes?"

Write-Host ""

$directories = Get-Directory $(Get-Location), "SharpRepository.*"
$uniqueDirectories = @()
foreach($directory in $directories)
{
	$dir_fullname = $directory.fullname
	
	if (!($uniqueDirectories -contains $dir_fullname) )
	{ 
		Write-Host "Processing " $dir_fullname
		
		$template_path = ($directory.fullname + "\" + $directory + ".nuspec.template")
		$nuspec_path = ($directory.fullname + "\" + $directory + ".nuspec")
		
		# update the version number
		((Get-Content $template_path) -creplace $version_holder, ("<version>" + $new_version + "</version>")) | Set-Content $nuspec_path
		((Get-Content $nuspec_path) -creplace $notes_holder, ("<releaseNotes>" + $new_notes + "</releaseNotes>")) | Set-Content $nuspec_path
		
		# copy the nuspec file over to the main project directory because I only know how to create the nuget package from that diretory, but that's just me i think :)
		Copy-Item $nuspec_path "..\$directory\$directory.nuspec"
		
		# see if there is a config transformation file
		if (Test-Path ($directory.fullname + "\content\config.transform"))
		{
			Copy-Item ($directory.fullname + "\content\config.transform") "..\$directory\content\config.transform"
		}
		
		# see if there is a Install.ps1 to deal with
		if (Test-Path ($directory.fullname + "\tools\Install.ps1"))
		{
			Copy-Item ($directory.fullname + "\tools\Install.ps1") "..\$directory\tools\Install.ps1"
		}
		
		# build the nuget package
		#& "C\NuGet\nuget.exe pack ..\$directory\$directory.csproj -Prop Configuration=Release"
		Invoke-Expression "& `"nuget.exe`" pack ..\$directory\$directory.csproj -Prop Configuration=Release"
		
		# move the nuget package back to the nuget directories
		Move-Item "$cur_directory\$directory.$new_version.nupkg" "$dir_fullname\$directory.$new_version.nupkg" -force
		
		# delete the files that we copied over to have the paths be picked up automatically for packaging
		Remove-Item "..\$directory\$directory.nuspec"

		if (Test-Path "..\$directory\content\config.transform")
		{
			Remove-Item "..\$directory\content\config.transform"
		}
		
		if (Test-Path "..\$directory\tools\Install.ps1")
		{
			Remove-Item "..\$directory\tools\Install.ps1"
		}
		
		# add the full path to the list of already processed directories, this is because the array for this loop has dups
		$uniqueDirectories += $directory.fullname
	}
}

Read-Host -prompt "Finished.  Press Enter key to exit"