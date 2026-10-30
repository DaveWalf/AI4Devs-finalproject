# Publica el puesto autocontenido para Windows x64, corre el E2E del flujo principal contra ese EXE y recién entonces
# escribe el zip y su suma (D7 de demostrar-flujo-de-punta-a-punta). Si algo falla, no queda ningún zip.
# Uso: pwsh .github/scripts/publicar-puesto.ps1 -Version <v>   (desde la raíz del repo; el E2E usa INCOMEDOR_SQLSERVER)
param([Parameter(Mandatory)] [string] $Version)
$ErrorActionPreference = 'Stop'

# La versión llega del nombre de un tag: sólo letras, dígitos, punto y guion, para que no arme otra ruta (OWASP A03).
if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z.\-]{0,63}\z') { throw "Versión inválida: «$Version»." }
if (-not (Test-Path -LiteralPath 'INComedor.sln')) { throw 'Correr desde la raíz del repo: global.json se lee del directorio actual.' }

$artefactos = Join-Path (Get-Location).Path 'artifacts'
$publicacion = Join-Path $artefactos 'publicacion\puesto'
$zip = Join-Path $artefactos "INComedor-Puesto-$Version-win-x64.zip"
$suma = "$zip.sha256"

function Invocar([string] $Que, [scriptblock] $Comando) {
    & $Comando
    if ($LASTEXITCODE -ne 0) { throw "$Que salió $LASTEXITCODE." }
}

# Lo que nunca viaja en el paquete: bases, certificados o claves, y las carpetas de comprobantes y certificados.
function Prohibidos([string[]] $Rutas) {
    @($Rutas | Where-Object {
        $_ -match '\.(db|db-wal|db-shm|pfx|key|pem)$' -or @($_ -split '[\\/]' | Where-Object { $_ -in 'comprobantes', 'certificados' }).Count -gt 0
    })
}

function RevisarPublicacion {
    $rutas = Get-ChildItem -LiteralPath $publicacion -Recurse -Force | ForEach-Object { [IO.Path]::GetRelativePath($publicacion, $_.FullName) }
    $malos = Prohibidos $rutas
    if ($malos.Count -gt 0) { throw "La publicación trae datos o secretos: $($malos -join ', ')." }
    "publicación revisada: $(@($rutas).Count) entradas, ninguna con datos ni secretos"
}

# Sin restos de una corrida anterior.
Remove-Item -LiteralPath $zip, $suma -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $publicacion) { Remove-Item -LiteralPath $publicacion -Recurse -Force }
New-Item -ItemType Directory -Force -Path $artefactos | Out-Null

# Restore y build separados, como en ci.yml. Cada paso deja su binlog en artifacts/, fuera del zip: en windows-latest,
# un restore posterior a «Dependencias vulnerables» falló con MSB4181 y sin advertencias (corridas 41132357042 y
# 41139494001), y en local no se reprodujo. release.yml sube los binlog si el job falla.
$binlog = { param([string] $Paso) '-bl:' + (Join-Path $artefactos "$Paso.binlog") }
Invocar 'El restore' { dotnet restore INComedor.sln (& $binlog 'restore') }
Invocar 'El build' { dotnet build INComedor.sln -c Release -warnaserror --no-restore (& $binlog 'build') }
Invocar 'La publicación' { dotnet publish src/Intelektron.INComedor.Kiosk -c Release -r win-x64 --self-contained true -o $publicacion (& $binlog 'publicacion') }
RevisarPublicacion

# El E2E contra el EXE publicado, con un entorno sin .NET a la vista (escenario «Abre sin .NET instalado»).
$env:INCOMEDOR_E2E_KIOSK_EXE = Join-Path $publicacion 'Intelektron.INComedor.Kiosk.exe'
try {
    Invocar 'El E2E contra el EXE publicado' { dotnet test tests/Intelektron.INComedor.E2E.Tests -c Release --no-build --filter-method '*Flujo_principal*' }
}
finally {
    Remove-Item Env:INCOMEDOR_E2E_KIOSK_EXE -ErrorAction SilentlyContinue
}

# Otra vez, por si la corrida dejó algo junto al EXE; después, el zip con las entradas en la raíz y su suma.
RevisarPublicacion
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($publicacion, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$entradas = [IO.Compression.ZipFile]::OpenRead($zip)
try { $malos = Prohibidos ($entradas.Entries | ForEach-Object FullName) } finally { $entradas.Dispose() }
if ($malos.Count -gt 0) {
    Remove-Item -LiteralPath $zip -Force
    throw "El zip traía datos o secretos: $($malos -join ', ')."
}

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($suma, "$hash  $(Split-Path $zip -Leaf)`n")
"zip: $zip · $((Get-Item -LiteralPath $zip).Length) bytes · sha256 $hash"
