# Pasa de una vez el SDK de .NET 11 de global.json y los paquetes de Microsoft 11.0.0-* de Directory.Packages.props,
# para el paso a la versión estable del 10/11 (D-C25). Ensayado con la rc.3 en feature/ensayo-sdk-rc3-dw.
# Uso: pwsh scripts/actualizar-sdk.ps1 -Sdk <versión> -Runtime <versión> [-Prueba]   (desde la raíz del repo)
# Con -Prueba imprime los cambios sin escribirlos. Escribe en UTF-8 sin BOM y sólo reemplaza los números, así que
# conserva el formato y los finales de línea de cada archivo.
param(
    [Parameter(Mandatory)] [string] $Sdk,
    [Parameter(Mandatory)] [string] $Runtime,
    [switch] $Prueba
)
$ErrorActionPreference = 'Stop'

# Las versiones arman una URL de NuGet: sólo la forma de una versión semántica (OWASP A03).
foreach ($v in $Sdk, $Runtime) {
    if ($v -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.\-]{0,63})?\z') { throw "Versión inválida: «$v»." }
}
if (-not (Test-Path -LiteralPath 'INComedor.sln')) { throw 'Correr desde la raíz del repo: global.json se lee del directorio actual.' }

$utf8 = [Text.UTF8Encoding]::new($false)
$rutaGlobal = Join-Path (Get-Location).Path 'global.json'
$rutaPaquetes = Join-Path (Get-Location).Path 'Directory.Packages.props'

# Un SDK que no está en el equipo dejaría el build sin probar: se corta antes de escribir nada.
$instalados = @(dotnet --list-sdks | ForEach-Object { ($_ -split ' ')[0] })
if ($Sdk -notin $instalados) {
    Write-Output "El SDK $Sdk no está instalado en este equipo (instalados: $($instalados -join ', ')). No se escribió nada."
    exit 1
}

$global = [IO.File]::ReadAllText($rutaGlobal, $utf8)
$sdkActual = [regex]::Match($global, '"sdk"\s*:\s*\{[^}]*?"version"\s*:\s*"(?<v>[^"]+)"').Groups['v']
if (-not $sdkActual.Success) { throw 'global.json no tiene sdk.version.' }

# Cada paquete sube sólo si NuGet tiene publicada esa versión: WebView.Wpf sale con MAUI, con otra cadencia y otro
# número de build, y puede no tenerla el mismo día que el runtime.
$paquetes = [IO.File]::ReadAllText($rutaPaquetes, $utf8)
$encontrados = [regex]::Matches($paquetes, '<PackageVersion\b[^>]*?\bInclude="(?<id>[^"]+)"[^>]*?\bVersion="(?<v>11\.0\.0-[^"]+)"')
if ($encontrados.Count -eq 0) { throw 'Directory.Packages.props no tiene paquetes 11.0.0-*.' }

$filas = [System.Collections.Generic.List[object]]::new()
$filas.Add([pscustomobject]@{ Paquete = 'SDK (global.json)'; Antes = $sdkActual.Value; Despues = $Sdk })
$cambios = [System.Collections.Generic.List[object]]::new()
foreach ($m in $encontrados) {
    $id = $m.Groups['id'].Value
    $antes = $m.Groups['v']
    $publicadas = (Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/$($id.ToLowerInvariant())/index.json").versions
    $despues = if ($antes.Value -eq $Runtime) { "$Runtime (ya estaba)" }
               elseif ($publicadas -contains $Runtime) { $cambios.Add($antes); $Runtime }
               else { 'sin esa versión, queda' }
    $filas.Add([pscustomobject]@{ Paquete = $id; Antes = $antes.Value; Despues = $despues })
}
($filas | Format-Table -AutoSize @{ n = 'Paquete'; e = 'Paquete' }, @{ n = 'Antes'; e = 'Antes' }, @{ n = 'Después'; e = 'Despues' } | Out-String).TrimEnd()

$enRuntime = @($filas | Select-Object -Skip 1 | Where-Object { $_.Despues -like "$Runtime*" }).Count
$sinVersion = @($filas | Select-Object -Skip 1 | Where-Object { $_.Despues -eq 'sin esa versión, queda' } | ForEach-Object Paquete)
if ($sinVersion.Count -gt 0) { Write-Output "Quedan sin subir, porque NuGet no tiene $($Runtime): $($sinVersion -join ', ')." }
if ($enRuntime -eq 0) {
    Write-Output "Ningún paquete tiene publicada la versión $Runtime en NuGet: ¿está bien escrita? No se escribió nada."
    exit 2
}
if ($Prueba) {
    Write-Output 'Modo de prueba: no se escribió nada.'
    exit 0
}

# De atrás hacia adelante, para que los índices de las coincidencias sigan valiendo.
foreach ($g in @($cambios | Sort-Object Index -Descending)) {
    $paquetes = $paquetes.Substring(0, $g.Index) + $Runtime + $paquetes.Substring($g.Index + $g.Length)
}
$global = $global.Substring(0, $sdkActual.Index) + $Sdk + $global.Substring($sdkActual.Index + $sdkActual.Length)
[IO.File]::WriteAllText($rutaGlobal, $global, $utf8)
[IO.File]::WriteAllText($rutaPaquetes, $paquetes, $utf8)
Write-Output "Escritos global.json y Directory.Packages.props: SDK $Sdk y $($cambios.Count) paquete(s) en $Runtime."
