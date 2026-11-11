using System.Globalization;
using System.Text;
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

/// <summary>
/// Los filtros de la consulta: un rango de fechas de negocio o, con <see cref="DesdeUtc"/> y <see cref="HastaUtc"/>, de
/// instantes de confirmación (D1 de buscar-y-exportar-consumos); y, opcionales, persona, servicio, opción, un texto y el
/// código de una persona (D2 y D3). El código nunca se guarda ni se registra.
/// </summary>
public sealed record FiltroConsumos(
    DateOnly Desde,
    DateOnly Hasta,
    Guid? PersonaId = null,
    Guid? ServicioId = null,
    Guid? OpcionId = null,
    OrdenConsumos Orden = OrdenConsumos.Hora,
    bool Ascendente = false,
    DateTimeOffset? DesdeUtc = null,
    DateTimeOffset? HastaUtc = null,
    string? Texto = null,
    string? Codigo = null)
{
    public static readonly TimeSpan Ventana = TimeSpan.FromHours(24);

    public static FiltroConsumos DelDia(DateOnly fecha) => new(fecha, fecha);

    /// <summary>Las últimas 24 horas hasta <paramref name="ahora"/>, por instante de confirmación (D1).</summary>
    public static FiltroConsumos UltimasHoras(DateTimeOffset ahora, DateOnly hoy) =>
        new(hoy.AddDays(-1), hoy, DesdeUtc: ahora - Ventana, HastaUtc: ahora);

    /// <summary>Si filtra por instante de confirmación y no por fecha de negocio.</summary>
    public bool PorInstante => DesdeUtc is not null && HastaUtc is not null;

    /// <summary>Sin el código: un filtro escrito en un registro o en una excepción nunca lo muestra (D3).</summary>
    public override string ToString() =>
        $"FiltroConsumos {{ Desde = {Desde:yyyy-MM-dd}, Hasta = {Hasta:yyyy-MM-dd}, PersonaId = {PersonaId}, ServicioId = {ServicioId}, "
        + $"OpcionId = {OpcionId}, Orden = {Orden}, Ascendente = {Ascendente}, DesdeUtc = {DesdeUtc:O}, HastaUtc = {HastaUtc:O}, "
        + $"Texto = {Texto}, Codigo = {(Codigo is null ? "null" : "(oculto)")} }}";
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
    EstadoTrabajoImpresion? EstadoComprobante,
    Guid? TicketId = null,
    Guid? TrabajoOriginalId = null,
    EstadoTrabajoImpresion? EstadoOriginal = null,
    long? NumeroCorrelativo = null)
{
    /// <summary>
    /// El número del comprobante, como lo muestra el papel: el correlativo si lo tiene, o los primeros ocho caracteres del
    /// comprobante (D2 de enriquecer-comprobante).
    /// </summary>
    public string? Numero => NumeroCorrelativo?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? TicketId?.ToString("D")[..8];
}

/// <summary>Un consumo con su secuencia del puesto, la marca de la exportación automática (D1 de exportar-consumos-automaticamente).</summary>
public sealed record ConsumoConSecuencia(long Secuencia, ConsumoConsultado Consumo);

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
    public const string CodigoDesconocido = "UNKNOWN_CODE";

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

    /// <summary>
    /// Los datos de impresión de un comprobante, para verlo en pantalla sin imprimirlo ni crear una copia (D4 de
    /// buscar-y-exportar-consumos): con la leyenda «COPIA EN PANTALLA» y sin trabajo. Null si no existe.
    /// </summary>
    public ComprobanteAImprimir? Comprobante(Guid ticketId)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var comando = ComandosSqlite.Crear(conexion, null,
            "SELECT comcmpDatosImpresion FROM tbComedorComprobantes WHERE comcmpCodigo = $tk", ("$tk", ticketId));
        return comando.ExecuteScalar() is string datos ? new ComprobanteAImprimir(Guid.Empty, ticketId, 0, datos, LeyendaEnPantalla) : null;
    }

    public const string LeyendaEnPantalla = "COPIA EN PANTALLA";

    /// <summary>El instante del puesto, el final de las últimas 24 horas (D1 de buscar-y-exportar-consumos).</summary>
    public DateTimeOffset Ahora() => _reloj.GetUtcNow();

    /// <summary>La fecha de negocio actual del puesto, la de los consumos «de hoy».</summary>
    public DateOnly Hoy()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        return CalculadoraFechaNegocio.Calcular(_reloj.GetUtcNow(), puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio).Valor;
    }

    public ResultadoConsumos Leer(FiltroConsumos filtro) => Leer(filtro, Tope);

    /// <summary>
    /// Los consumos con secuencia mayor que <paramref name="secuencia"/>, en orden de secuencia, hasta <paramref name="tope"/>:
    /// lo que un destino de la exportación automática todavía no recibió (D1 de exportar-consumos-automaticamente).
    /// </summary>
    public IReadOnlyList<ConsumoConSecuencia> Desde(long secuencia, int tope)
    {
        using var conexion = Abrir();
        using var comando = ComandosSqlite.Crear(conexion, null,
            $"""
            SELECT {Columnas}, con.comconSecuenciaCliente
            {Origen}
            WHERE con.comconSecuenciaCliente > $marca
            ORDER BY con.comconSecuenciaCliente
            LIMIT $tope
            """,
            ("$marca", secuencia), ("$tope", tope));
        using var lector = comando.ExecuteReader();
        var consumos = new List<ConsumoConSecuencia>();
        while (lector.Read())
        {
            consumos.Add(new ConsumoConSecuencia(lector.GetInt64(15), Fila(lector)));
        }

        return consumos;
    }

    /// <summary>Las columnas de un consumo, en el orden que lee <see cref="Fila"/>.</summary>
    private const string Columnas =
        """
        con.comconCodigo, con.comconFechaConfirmacionLocalUtc, con.comconFechaNegocio, con.comperCodigo, per.comperNombreVisible,
                   con.commenCodigo, men.commenNombre, men.comsrvCodigo, srv.comsrvNombre, con.comconPrecioAplicado, ult.comimpEstado,
                   cmp.comcmpCodigo, ori.comimpCodigo, ori.comimpEstado, json_extract(cmp.comcmpDatosImpresion, '$.TicketNumber')
        """;

    /// <summary>De dónde sale un consumo: la persona y la opción de su versión, su comprobante y sus trabajos.</summary>
    private const string Origen =
        """
        FROM tbComedorConsumos con
                    JOIN tbComedorPersonas per ON per.comvcCodigo = con.comvcCodigo AND per.comperCodigo = con.comperCodigo
                    JOIN tbComedorOpcionesMenu men ON men.comvcCodigo = con.comvcCodigo AND men.commenCodigo = con.commenCodigo
                    JOIN tbComedorTiposServicio srv ON srv.comvcCodigo = con.comvcCodigo AND srv.comsrvCodigo = men.comsrvCodigo
                    LEFT JOIN tbComedorComprobantes cmp ON cmp.comconCodigo = con.comconCodigo
                    LEFT JOIN tbComedorTrabajosImpresion ult ON ult.comcmpCodigo = cmp.comcmpCodigo
                        AND ult.comimpNumeroCopia = (SELECT MAX(comimpNumeroCopia) FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = cmp.comcmpCodigo)
                    LEFT JOIN tbComedorTrabajosImpresion ori ON ori.comcmpCodigo = cmp.comcmpCodigo AND ori.comimpNumeroCopia = 0
        """;

    private static ConsumoConsultado Fila(SqliteDataReader lector)
    {
        using var precio = JsonDocument.Parse(lector.GetString(9));
        return new ConsumoConsultado(
            lector.GetGuid(0), FormatoInstante.Leer(lector.GetString(1)),
            DateOnly.ParseExact(lector.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            lector.GetGuid(3), lector.GetString(4), lector.GetGuid(5), lector.GetString(6), lector.GetGuid(7), lector.GetString(8),
            precio.RootElement.GetProperty("Currency").GetString()!, precio.RootElement.GetProperty("Scale").GetInt32(),
            precio.RootElement.GetProperty("EmployeeChargeMinor").GetInt64(),
            lector.IsDBNull(10) ? null : TrabajosImpresion.Estado(lector.GetString(10)),
            lector.IsDBNull(11) ? null : lector.GetGuid(11),
            lector.IsDBNull(12) ? null : lector.GetGuid(12),
            lector.IsDBNull(13) ? null : TrabajosImpresion.Estado(lector.GetString(13)),
            lector.IsDBNull(14) ? null : lector.GetInt64(14));
    }

    /// <summary>La misma consulta con otro tope: la exportación lee hasta 10 000 (D6 de buscar-y-exportar-consumos).</summary>
    public ResultadoConsumos Leer(FiltroConsumos filtro, int tope)
    {
        if (filtro.Desde > filtro.Hasta || (filtro.PorInstante && filtro.DesdeUtc > filtro.HastaUtc))
        {
            return new ResultadoConsumos([], false, RangoInvertido);
        }

        if (filtro.Hasta.DayNumber - filtro.Desde.DayNumber + 1 > DiasMaximos)
        {
            return new ResultadoConsumos([], false, RangoDemasiadoLargo);
        }

        var sentido = filtro.Ascendente ? "ASC" : "DESC";
        using var conexion = Abrir();
        var codigo = string.IsNullOrWhiteSpace(filtro.Codigo) ? null : filtro.Codigo.Trim();
        if (codigo is not null && !CodigoDeAlguien(conexion, codigo))
        {
            return new ResultadoConsumos([], false, CodigoDesconocido);
        }

        using var comando = ComandosSqlite.Crear(conexion, null,
            $"""
            SELECT {Columnas}
            {Origen}
            WHERE (($ini IS NULL AND con.comconFechaNegocio BETWEEN $desde AND $hasta)
                   OR ($ini IS NOT NULL AND con.comconFechaConfirmacionLocalUtc BETWEEN $ini AND $fin))
              AND ($per IS NULL OR con.comperCodigo = $per)
              AND ($cod IS NULL OR con.comperCodigo IN (
                    SELECT cre.comperCodigo FROM tbComedorCredencialesPersonas cre
                    WHERE cre.comcreTipo = 'SERIAL_CODE' AND cre.comcreValorNormalizado = $cod))
              AND ($patron IS NULL OR normalizar(per.comperNombreVisible || ' ' || men.commenNombre || ' ' || srv.comsrvNombre || ' '
                    || COALESCE(substr(cmp.comcmpCodigo, 1, 8), '') || ' '
                    || COALESCE(json_extract(cmp.comcmpDatosImpresion, '$.TicketNumber'), '')) LIKE $patron ESCAPE '\')
              AND ($srv IS NULL OR men.comsrvCodigo = $srv)
              AND ($men IS NULL OR con.commenCodigo = $men)
            ORDER BY {Expresiones[filtro.Orden]} {sentido}, con.comconFechaConfirmacionLocalUtc DESC, con.comconSecuenciaCliente DESC
            LIMIT $limite
            """,
            ("$desde", Texto(filtro.Desde)), ("$hasta", Texto(filtro.Hasta)), ("$per", filtro.PersonaId), ("$srv", filtro.ServicioId),
            ("$men", filtro.OpcionId), ("$limite", tope + 1),
            ("$ini", filtro.PorInstante ? FormatoInstante.Texto(filtro.DesdeUtc!.Value) : null),
            ("$fin", filtro.PorInstante ? FormatoInstante.Texto(filtro.HastaUtc!.Value) : null),
            ("$cod", codigo), ("$patron", Patron(filtro.Texto)));
        using var lector = comando.ExecuteReader();
        var filas = new List<ConsumoConsultado>();
        while (lector.Read())
        {
            filas.Add(Fila(lector));
        }

        var hayMas = filas.Count > tope;
        return new ResultadoConsumos(hayMas ? filas.Take(tope).ToList() : filas, hayMas, null);
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

    /// <summary>
    /// La conexión con la intercalación del español, para ordenar los nombres como se leen («Álvaro» antes que «Beatriz»), y la
    /// función normalizar, que la búsqueda usa para no distinguir acentos ni mayúsculas (D2 de buscar-y-exportar-consumos).
    /// </summary>
    private SqliteConnection Abrir()
    {
        var conexion = _baseComedor.AbrirConexion();
        conexion.CreateCollation(Intercalacion, (a, b) => EnEspanol.Compare(a, b));
        conexion.CreateFunction<string?, string?>("normalizar", Normalizar, isDeterministic: true);
        return conexion;
    }

    /// <summary>Sin acentos (la forma descompuesta sin sus marcas) y en minúsculas.</summary>
    public static string? Normalizar(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        var sinMarcas = new StringBuilder(texto.Length);
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinMarcas.Append(caracter);
            }
        }

        return sinMarcas.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>
    /// El patrón de LIKE del texto buscado, normalizado y con sus comodines escapados: lo tipeado se busca tal cual, nunca
    /// como comodín (OWASP A03). Vacío, ninguno.
    /// </summary>
    private static string? Patron(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var escapado = Normalizar(texto.Trim())!.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return "%" + escapado + "%";
    }

    /// <summary>Si el código, normalizado como al identificar (RB01: sólo Trim()), es de alguna credencial de cualquier versión.</summary>
    private static bool CodigoDeAlguien(SqliteConnection conexion, string codigo)
    {
        using var comando = ComandosSqlite.Crear(conexion, null,
            "SELECT 1 FROM tbComedorCredencialesPersonas WHERE comcreTipo = 'SERIAL_CODE' AND comcreValorNormalizado = $cod LIMIT 1",
            ("$cod", codigo));
        return comando.ExecuteScalar() is not null;
    }

    private static string Texto(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
