try 
{
	Install-ChocolateyZipPackage 'MiningController' '$url$'  "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

    Write-ChocolateySuccess 'MiningController'
} 
catch 
{
	Write-ChocolateyFailure 'MiningController' $($_.Exception.Message)
	throw
}
