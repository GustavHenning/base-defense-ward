# Send one command to the DevHarness debug socket and print the reply.
# Usage: tools\vh.ps1 "place BaseDefenseWard 0 4"
param([Parameter(Mandatory=$true, Position=0)][string]$Line, [int]$TimeoutMs = 20000, [int]$Port = 52380)
$c = New-Object System.Net.Sockets.TcpClient
$c.Connect("127.0.0.1", $Port)
$s = $c.GetStream(); $s.ReadTimeout = $TimeoutMs
$w = New-Object System.IO.StreamWriter($s); $w.AutoFlush = $true
$r = New-Object System.IO.StreamReader($s)
$w.WriteLine($Line)
while ($true) { $l = $r.ReadLine(); if ($null -eq $l -or $l -eq "<<END>>") { break }; Write-Output $l }
$c.Close()
