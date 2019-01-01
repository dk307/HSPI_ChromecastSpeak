param (
    [Parameter(Position=0, Mandatory=$true)]
    [ValidateSet("add","remove")]
    [string]$operation,

    [Parameter(Position=1, Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
     [string]$ipAddress,

    [Parameter(Position=2, Mandatory=$true)]
    [int]$port
)

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal( [Security.Principal.WindowsIdentity]::GetCurrent() )

if ( -not $currentPrincipal.IsInRole( [Security.Principal.WindowsBuiltInRole]::Administrator ))
{
     write-host "Error Script only works as administrator".
     return
}

$builder = New-Object System.UriBuilder
$builder.Host = $ipAddress
$builder.Port = $port
$builder.Scheme = 'http'
$serviceUrl = $builder.ToString()

$ruleName = 'HSPI Chromecast Speak Plugin'
$exitingRules = @()

$potentialRules = @(Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)
foreach($rule in $potentialRules) {
    $ruleFilter = $rule |  Get-NetFirewallPortFilter
    $addressFilter = $rule |  Get-NetFirewallAddressFilter

    if (($ruleFilter.LocalPort -eq $port) -and ($ruleFilter.Protocol -eq "TCP") -and ($addressFilter.LocalAddress -eq $ipAddress)) {
        $exitingRules += $rule
    }
}

if ($operation -eq 'add') {
    if ($exitingRules.Length -eq 0) {
        Write-Verbose -Message ('Adding Firewall rule for {0}' -f $serviceUrl) -Verbose
        New-NetFirewallRule -DisplayName $ruleName -LocalAddress $ipAddress -LocalPort $port -Protocol TCP | Write-Verbose -Verbose
    }
}

if ($operation -eq 'remove') {
    foreach($rule in $exitingRules) {
         Write-Verbose -Message ('Removing Firewall rule for {0}' -f $serviceUrl) -Verbose
         Remove-NetFirewallRule -InputObject $rule
    }
}