# Falla si la solución tiene un paquete NuGet vulnerable, directo o transitivo.
# `dotnet list package --vulnerable` sale 0 aunque encuentre uno: el veredicto sale de su JSON.
# Uso: pwsh .github/scripts/auditar-dependencias.ps1 [-Solucion INComedor.sln]   (desde la raíz del repo)
param([string] $Solucion = 'INComedor.sln')
$ErrorActionPreference = 'Stop'

$salida = dotnet list $Solucion package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) { Write-Output $salida; throw "dotnet list package salió $LASTEXITCODE." }
$informe = ($salida -join "`n") | ConvertFrom-Json

# Una corrida que no auditó nada también sale 0: tienen que estar todos los proyectos de la solución, sin
# `problems` (un restore fallido). Un proyecto sin vulnerabilidades viene sin `frameworks`: eso no es un error.
$esperados = @(dotnet sln $Solucion list | Where-Object { $_ -match '\.csproj$' }).Count
$auditados = @($informe.projects).Count
$conProblemas = @($informe.projects | Where-Object { $_.problems })
if ($esperados -eq 0 -or $auditados -ne $esperados -or $conProblemas.Count -gt 0) {
    Write-Output ($informe | ConvertTo-Json -Depth 8)
    throw "Se auditaron $auditados de $esperados proyectos; $($conProblemas.Count) con problemas."
}

$vulnerables = @(
    foreach ($p in $informe.projects) {
        foreach ($f in $p.frameworks) {
            foreach ($paquete in @($f.topLevelPackages) + @($f.transitivePackages)) {
                foreach ($v in @($paquete.vulnerabilities | Where-Object { $_ })) {
                    '{0} · {1} {2} · {3} · {4}' -f (Split-Path $p.path -Leaf), $paquete.id, $paquete.resolvedVersion, $v.severity, $v.advisoryurl
                }
            }
        }
    }
)
"proyectos auditados: $auditados de $esperados · paquetes vulnerables: $($vulnerables.Count)"
if ($vulnerables.Count -gt 0) {
    $vulnerables | ForEach-Object { "::error::$_" }
    exit 1
}
