using System.Globalization;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>El archivo escrito, sólo su nombre y nunca la ruta, y las personas que tiene.</summary>
public sealed record ResultadoExportacionPersonas(string Archivo, int Filas);

/// <summary>
/// La exportación de Personas (administracion-local de editar-raciones-en-personas, D5): las personas de la versión activa
/// con su estado, lo usado hoy y el límite de cada cupo, con las mismas columnas que la tabla
/// (<see cref="VistaAdministracion.ParesDeCupo"/>), y las opciones que habilitan, sin ningún código. La escribe
/// <see cref="EscritorPlanilla"/>, como la de Consumos; un cupo que la persona no tiene dice «—».
/// </summary>
public sealed class ExportacionPersonas
{
    private readonly AdministracionLocal _administracion;
    private readonly TimeProvider _reloj;

    public ExportacionPersonas(AdministracionLocal administracion, TimeProvider reloj)
    {
        _administracion = administracion;
        _reloj = reloj;
    }

    /// <summary>
    /// Escribe el archivo y devuelve su nombre. Sin poder escribir en <paramref name="carpeta"/>, lanza la excepción de E/S
    /// después de borrar el temporal, como la exportación de Consumos.
    /// </summary>
    public ResultadoExportacionPersonas Exportar(FormatoExportacion formato, string carpeta, TimeZoneInfo zona)
    {
        var vista = _administracion.Leer();
        var pares = vista.ParesDeCupo();
        List<string> encabezado = ["Persona", "Estado"];
        foreach (var par in pares)
        {
            encabezado.Add($"{vista.NombrePar(par)}: usado hoy");
            encabezado.Add($"{vista.NombrePar(par)}: límite");
        }

        encabezado.Add("Opciones habilitadas");
        var filas = vista.Personas.Select(persona =>
        {
            List<object> fila = [persona.Nombre, persona.Activa ? "Activa" : "De baja"];
            foreach (var par in pares)
            {
                if (persona.Cupos.FirstOrDefault(c => c.ServicioId == par.Servicio && c.CategoriaId == par.Categoria) is { } cupo)
                {
                    fila.Add(new NumeroPlanilla(cupo.UsadoHoy, 0));
                    fila.Add(new NumeroPlanilla(cupo.Limite, 0));
                }
                else
                {
                    fila.Add("—");
                    fila.Add("—");
                }
            }

            fila.Add(new NumeroPlanilla(persona.OpcionesHabilitadas.Count, 0));
            return (IReadOnlyList<object>)fila;
        }).ToList();

        var ahora = TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), zona);
        var raiz = "INComedor-personas-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var nombre = EscritorPlanilla.Escribir(carpeta, raiz, formato, "Personas", encabezado, filas);
        return new ResultadoExportacionPersonas(nombre, filas.Count);
    }
}
