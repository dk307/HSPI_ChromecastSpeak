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

$builder = New-Object System.UriBuilder
$builder.Host = $ipAddress
$builder.Port = $port
$builder.Scheme = 'http'
$serviceUrl = $builder.ToString()

$serviceUser = [String]::Join("\", $env:userdomain, $env:username)

if( -not (netsh http show urlacl url=$serviceUrl | Where-Object { $_ -match [regex]::Escape($serviceUrl) }) )
{
    if ($operation -eq 'add') {
        Write-Verbose -Message ('Granting {0} permission to listen on {1}' -f $serviceUser, $serviceUrl) -Verbose
        netsh http add urlacl url=$serviceUrl  user=$serviceUser| Write-Verbose -Verbose
    }
} else {
    if ($operation -eq 'remove') {
        Write-Verbose -Message ('Removing listen on {0}' -f $serviceUrl) -Verbose
        netsh http delete urlacl url=$serviceUrl | Write-Verbose -Verbose
    }
}

$ruleName = 'Homeseer Chromecast Speak'
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
        New-NetFirewallRule -DisplayName 'Homeseer Chromecast Speak' -LocalAddress $ipAddress -LocalPort $port -Protocol TCP -Program "System" | Write-Verbose -Verbose
    }
}

if ($operation -eq 'remove') {
    foreach($rule in $exitingRules) {
         Write-Verbose -Message ('Removing Firewall rule for {0}' -f $serviceUrl) -Verbose
         Remove-NetFirewallRule -InputObject $rule
    }
}