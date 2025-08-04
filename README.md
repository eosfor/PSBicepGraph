# Overview

```pwsh
Import-Module -Name "/Users/andrei/repo/PSGraph/PSGraph/bin/Debug/net9.0/publish/PSGraph.dll" -Verbose
Import-Module "/Users/andrei/repo/PSBicepGraph/src/PSBicepGraph/bin/Debug/net9.0/publish/PSBicepGraph.dll" -Verbose
$g = New-BicepGraph -Path "/Users/andrei/repo/infrastructure/modules/config/wcProd-westus3-config.bicep"
$dsm = New-DSM -graph $g
$ret = Start-DSMClustering -Dsm $dsm

Export-DSM -Result $ret -Format VEGA_JSON 
Export-DSM -Result $ret -Format VEGA_JSON -Path $Env:TMPDIR/dsmPartitioned.json
Export-DSM -Result $ret -Format VEGA_HTML -Path $Env:TMPDIR/dsmPartitioned.html
```