using System.Globalization;
using System.Text.Json;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Las columnas por las que se puede ordenar la consulta de consumos: una lista cerrada (D9).</summary>
public enum OrdenConsumos
{
    Hora,
    Persona,
    Opcion,
    Servicio,
    Cargo,
    Estado,
}

/// <summary>Los filtros de la consulta: un rango de fechas de negocio y, opcionales, persona, servicio y opción.</summary>
public sealed record FiltroConsumos(
    DateOnly Desde,
    DateOnly Hasta,
    Guid? PersonaId = null,
    Guid? ServicioId = null,
    Guid? OpcionId = null,
    OrdenConsumos Orden = OrdenConsumos.Hora,
    bool Ascendente = false)
{
    public static FiltroConsumos DelDia(DateOnly fecha) => new(fecha, fecha);
}

/// <summary>Un consumo registrado, con la persona y la opción como eran en la versión del consumo y su precio aplicado.</summary>
public sealed record ConsumoConsultado(
    Guid ConsumoId,
    DateTimeOffset ConfirmadoUtc,
    DateOnly FechaNegocio,
    Guid PersonaId,
    string Persona,
    Guid OpcionId,
    string Opcion,
    Guid ServicioId,
    string Servicio,
    string Moneda,
    int Escala,
    long CargoUnidadesMenores,
    EstadoTrabajoImpresion? EstadoComprobante);

/// <summary>Hasta 500 filas, si hay más, o el motivo por el que el filtro se rechazó sin consultar.</summary>
public sealed record ResultadoConsumos(IReadOnlyList<ConsumoConsultado> Filas, bool HayMas, string? Rechazo);

/// <summary>Una persona del filtro: el nombre de su versión más reciente y si está de baja.</summary>
public sealed record PersonaParaFiltro(Guid Id, string Nombre, bool DeBaja);

/// <summary>Un servicio o una opción del filtro, con el nombre de su versión más reciente.</summary>
public sealed record ElementoParaFiltro(Guid Id, string Nombre);

/// <summary>
/// La consulta de consumos de Configuración (consulta-consumos de configurar-puesto-por-pestanas, D9): une el consumo con la
/// persona, la opción y el servicio de la versión del consumo, y con el último trabajo de su comprobante. Todos los valores
/// van parametrizados y el orden sale de una lista cerrada que se traduce a una expresión fija, nunca de un texto de la
/// pantalla (OWASP A03). Pide una fila más que el tope para saber si hay más. Sólo lee: no cambia ningún hecho.
/// </summary>
public sealed class ConsultaConsumos
{
    public const int Tope = 500;
    public const int DiasMaximos = 366;
    public const string RangoInvertido = "INVERTED_RANGE";
    public const string RangoDemasiadoLargo = "RANGE_TOO_LONG";

    private const string Intercalacion = "ES_AR";

    private static readonly StringComparer EnEspanol = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), ignoreCase: true);

    private static readonly Dictionary<OrdenConsumos, string> Expresiones = new()
    {
        [OrdenConsumos.Hora] = "con.comconFechaConfirmacionLocalUtc",
        [OrdenConsumos.Persona] = $"per.comperNombreVisible COLLATE {Intercalacion}",
        [OrdenConsumos.Opcion] = $"men.commenNombre COLLATE {Intercalacion}",
        [OrdenConsumos.Servicio] = $"srv.comsrvNombre COLLATE {Intercalacion}",
        [OrdenConsumos.Cargo] = "json_extract(con.comconPrecioAplicado, '$.EmployeeChargeMinor')",
        [OrdenConsumos.Estado] = "ult.comimpEstado",
    };

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;

    public ConsultaConsumos(BaseComedor baseComedor, TimeProvider reloj)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
    }

    /// <summary>La fecha de negocio actual del puesto, la de los consumos «de hoy».</summary>
    public DateOnly Hoy()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        return CalculadoraFechaNegocio.Calcular(_reloj.GetUtcNow(), puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio).Valor;
    }

    public ResultadoConsumos Leer(FiltroConsumos filtro)
    {
        if (filtro.Desde > filtro.Hasta)
        {
            return new ResultadoConsumos([], false, RangoInvertido);
        }

        if (filtro.Hasta.DayNumber - filtro.Desde.DayNumber + 1 > DiasMaximos)
        {
            return new ResultadoConsumos([], false, RangoDemasiadoLargo);
        }

        var sentido = filtro.Ascendente ? "ASC" : "DESC";
        using var conexion = Abrir();
        using var comando = ComandosSqlite.Crear(conexion, null,
            $"""
            SELECT con.comconCodigo, con.comconFechaConfirmacionLocalUtc, con.comconFechaNegocio, con.comperCodigo, per.comperNombreVisible,
                   con.commenCodigo, men.commenNombre, men.comsrvCodigo, srv.comsrvNombre, con.comconPrecioAplicado, ult.comimpEstado
            FROM tbComedorConsumos con
            JOIN tbComedorPersonas per ON per.comvcCodigo = con.comvcCodigo AND per.comperCodigo = con.comperCodigo
            JOIN tbComedorOpcionesMenu men ON men.comvcCodigo = con.comvcCodigo AND men.commenCodigo = con.commenCodigo
            JOIN tbComedorTiposServicio srv ON srv.comvcCodigo = con.comvcCodigo AND srv.comsrvCodigo = men.comsrvCodigo
            LEFT JOIN tbComedorComprobantes cmp ON cmp.comconCodigo = con.comconCodigo
            LEFT JOIN tbComedorTrabajosImpresion ult ON ult.comcmpCodigo = cmp.comcmpCodigo
                AND ult.comimpNumeroCopia = (SELECT MAX(comimpNumeroCopia) FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = cmp.comcmpCodigo)
            WHERE con.comconFechaNegocio BETWEEN $desde AND $hasta
              AND ($per IS NULL OR con.comperCodigo = $per)
              AND ($srv IS NULL OR men.comsrvCodigo = $srv)
              AND ($men IS NULL OR con.commenCodigo = $men)
            ORDER BY {Expresiones[filtro.Orden]} {sentido}, con.comconFechaConfirmacionLocalUtc DESC, con.comconSecuenciaCliente DESC
            LIMIT $limite
            """,
            ("$desde", Texto(filtro.Desde)), ("$hasta", Texto(filtro.Hasta)), ("$per", filtro.PersonaId), ("$srv", filtro.ServicioId),
            ("$men", filtro.OpcionId), ("$limite", Tope + 1));
        using var lector = comando.ExecuteReader();
        var filas = new List<ConsumoConsultado>();
        while (lector.Read())
        {
            using var precio = JsonDocument.Parse(lector.GetString(9));
            filas.Add(new ConsumoConsultado(
                lector.GetGuid(0), FormatoInstante.Leer(lector.GetString(1)),
                DateOnly.ParseExact(lector.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                lector.GetGuid(3), lector.GetString(4), lector.GetGuid(5), lector.GetString(6), lector.GetGuid(7), lector.GetString(8),
                precio.RootElement.GetProperty("Currency").GetString()!, precio.RootElement.GetProperty("Scale").GetInt32(),
                precio.RootElement.GetProperty("EmployeeChargeMinor").GetInt64(),
                lector.IsDBNull(10) ? null : TrabajosImpresion.Estado(lector.GetString(10))));
        }

        var hayMas = filas.Count > Tope;
        return new ResultadoConsumos(hayMas ? filas.Take(Tope).ToList() : filas, hayMas, null);
    }

    /// <summary>
    /// Las personas de la versión activa y de las anteriores, cada una con el nombre de su versión más reciente y la marca de
    /// baja (D8 de administrar-personas-y-opciones: la baja no borra la persona).
    /// </summary>
    public IReadOnlyList<PersonaParaFiltro> Personas() =>
        LeerUltimos(
            """
            SELECT per.comperCodigo, per.comperNombreVisible, per.comperActivo
            FROM tbComedorPersonas per JOIN tbComedorVersionesConfiguracion ver ON ver.comvcCodigo = per.comvcCodigo
            WHERE ver.comvcVersionConfiguracion = (
                SELECT MAX(v2.comvcVersionConfiguracion) FROM tbComedorPersonas p2
                JOIN tbComedorVersionesConfiguracion v2 ON v2.comvcCodigo = p2.comvcCodigo WHERE p2.comperCodigo = per.comperCodigo)
            """,
            l => new PersonaParaFiltro(l.GetGuid(0), l.GetString(1), l.GetInt64(2) == 0)).OrderBy(p => p.Nombre, EnEspanol).ToList();

    public IReadOnlyList<ElementoParaFiltro> Servicios() =>
        LeerUltimos(
            """
            SELECT srv.comsrvCodigo, srv.comsrvNombre
            FROM tbComedorTiposServicio srv JOIN tbComedorVersionesConfiguracion ver ON ver.comvcCodigo = srv.comvcCodigo
            WHERE ver.comvcVersionConfiguracion = (
                SELECT MAX(v2.comvcVersionConfiguracion) FROM tbComedorTiposServicio s2
                JOIN tbComedorVersionesConfiguracion v2 ON v2.comvcCodigo = s2.comvcCodigo WHERE s2.comsrvCodigo = srv.comsrvCodigo)
            """,
            l => new ElementoParaFiltro(l.GetGuid(0), l.GetString(1))).OrderBy(s => s.Nombre, EnEspanol).ToList();

    public IReadOnlyList<ElementoParaFiltro> Opciones() =>
        LeerUltimos(
            """
            SELECT men.commenCodigo, men.commenNombre
            FROM tbComedorOpcionesMenu men JOIN tbComedorVersionesConfiguracion ver ON ver.comvcCodigo = men.comvcCodigo
            WHERE ver.comvcVersionConfiguracion = (
                SELECT MAX(v2.comvcVersionConfiguracion) FROM tbComedorOpcionesMenu m2
                JOIN tbComedorVersionesConfiguracion v2 ON v2.comvcCodigo = m2.comvcCodigo WHERE m2.commenCodigo = men.commenCodigo)
            """,
            l => new ElementoParaFiltro(l.GetGuid(0), l.GetString(1))).OrderBy(o => o.Nombre, EnEspanol).ToList();

    private List<T> LeerUltimos<T>(string sql, Func<SqliteDataReader, T> leer)
    {
        using var conexion = Abrir();
        using var comando = ComandosSqlite.Crear(conexion, null, sql);
        using var lector = comando.ExecuteReader();
        var filas = new List<T>();
        while (lector.Read())
        {
            filas.Add(leer(lector));
        }

        return filas;
    }

    /// <summary>La conexión con la intercalación del español, para ordenar los nombres como se leen («Álvaro» antes que «Beatriz»).</summary>
    private SqliteConnection Abrir()
    {
        var conexion = _baseComedor.AbrirConexion();
        conexion.CreateCollation(Intercalacion, (a, b) => EnEspanol.Compare(a, b));
        return conexion;
    }

    private static string Texto(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
