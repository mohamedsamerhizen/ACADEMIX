param()
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$edge='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (!(Test-Path -LiteralPath $edge)) { throw 'Installed Edge renderer not found.' }
$cache=Join-Path $env:TEMP 'academix-svg-render-20261031'
New-Item -ItemType Directory -Force $cache | Out-Null
$results=@()
foreach($svg in Get-ChildItem -LiteralPath (Join-Path $root 'docs/assets/graphics') -Filter '*.svg') {
  [xml]$xml=Get-Content -LiteralPath $svg.FullName -Raw
  $height=[int]$xml.DocumentElement.height
  $html=Join-Path $cache ($svg.BaseName+'.html')
  $image=[Convert]::ToBase64String([IO.File]::ReadAllBytes($svg.FullName))
  [IO.File]::WriteAllText($html,"<!doctype html><html><head><meta charset='utf-8'><style>html,body{margin:0;padding:0;width:1200px;height:$($height)px;overflow:hidden}img{display:block;width:1200px;height:$($height)px}</style></head><body><img src='data:image/svg+xml;base64,$image'></body></html>")
  $png=Join-Path $svg.DirectoryName ($svg.BaseName+'.png')
  $profile=Join-Path $cache 'edge-profile'
  $arguments=@('--headless=new','--disable-gpu','--disable-extensions','--no-default-browser-check','--virtual-time-budget=1000','--no-first-run','--hide-scrollbars',"--user-data-dir=`"$profile`"",'--force-device-scale-factor=1',"--window-size=1200,$height",'--disable-features=msEdgeSidebarV2',"--screenshot=`"$png`"",([Uri]$html).AbsoluteUri)
  $p=Start-Process -FilePath $edge -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $cache 'renderer.log')
  if (!$p.WaitForExit(15000)) { $p.Kill($true); $p.WaitForExit() }
  if (!(Test-Path -LiteralPath $png)) { throw "Missing rendered image: $png" }
  $results += [pscustomobject]@{asset=$svg.Name; width=1200; height=$height; pngBytes=(Get-Item -LiteralPath $png).Length}
}
$results | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root 'docs/assets/graphics/render-manifest.json')
Write-Output "Rendered $($results.Count) SVG assets to local PNG fallbacks."

