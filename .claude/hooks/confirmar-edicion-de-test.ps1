# PreToolUse de Claude Code: pide confirmación antes de editar o sobrescribir un archivo que ya
# existe en tests/. Es la regla de AGENTS.md «No modificar un test existente sin aprobación
# explícita de David» hecha código. Un test nuevo (Write sobre una ruta que no existe) pasa.
# Ante cualquier error pide confirmación: un hook que falla no deja pasar la edición en silencio.
# Guardado en UTF-8 con BOM: powershell.exe 5.1 lee como ANSI un .ps1 sin BOM y rompe los acentos.
$ErrorActionPreference = 'Stop'

function Pedir-Confirmacion([string] $motivo) {
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
    @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'ask'
            permissionDecisionReason = $motivo
        }
    } | ConvertTo-Json -Compress -Depth 3
    exit 0
}

try {
    $evento = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $ruta = $evento.tool_input.file_path
    if (-not $ruta) { $ruta = $evento.tool_input.notebook_path }
    if (-not $ruta) { exit 0 }

    $tests = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\tests')).TrimEnd('\') + '\'
    $destino = [IO.Path]::GetFullPath($ruta)
    if (-not $destino.StartsWith($tests, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
    if (-not (Test-Path -LiteralPath $destino -PathType Leaf)) { exit 0 }

    Pedir-Confirmacion "Test existente: $destino. AGENTS.md pide la aprobación explícita de David para modificarlo."
}
catch {
    Pedir-Confirmacion "El hook de tests no pudo evaluar la edición ($($_.Exception.Message)); se pide confirmación."
}
