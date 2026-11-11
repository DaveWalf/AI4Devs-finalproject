using System.Globalization;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Domain;
using static Intelektron.INComedor.Infrastructure.Sqlite.CodigosAdministracion;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Lo que una importación hace con una opción (D2 de importar-opciones-desde-excel).</summary>
public enum AccionOpcionImportada
{
    Agregada,
    Modificada,
    SinCambios,
    Quitada,
}

/// <summary>Una opción del resumen: su fila (null si se quita por no estar), su id si ya existe, su nombre y lo que pasa.</summary>
public sealed record OpcionImportada(int? Fila, Guid? OpcionId, string Nombre, AccionOpcionImportada Accion);

/// <summary>El resumen de una importación de Opciones antes de aplicarla, con las ediciones para <see cref="AdministracionLocal.GuardarOpciones"/>.</summary>
public sealed record RevisionOpciones(IReadOnlyList<ErrorImportacion> Errores, IReadOnlyList<OpcionImportada> Opciones, IReadOnlyList<EdicionOpcion> Ediciones)
{
    public bool Valida => Errores.Count == 0;

    public int Agregadas => Opciones.Count(o => o.Accion == AccionOpcionImportada.Agregada);

    public int Modificadas => Opciones.Count(o => o.Accion == AccionOpcionImportada.Modificada);

    public int SinCambios => Opciones.Count(o => o.Accion == AccionOpcionImportada.SinCambios);

    public int Quitadas => Opciones.Count(o => o.Accion == AccionOpcionImportada.Quitada);

    /// <summary>Cuántas filas siguen siendo las de la planilla base, que se agregarían como opciones (D5).</summary>
    public int FilasDeEjemplo { get; init; }
}

/// <summary>Lo que dejó una importación de Opciones aplicada: el guardado y cada opción con su id.</summary>
public sealed record ResultadoImportacionOpciones(ResultadoGuardado Guardado, IReadOnlyList<OpcionImportada> Opciones);

/// <summary>El archivo escrito, sólo su nombre, y las opciones que tiene.</summary>
public sealed record ResultadoExportacionOpciones(string Archivo, int Filas);

/// <summary>
/// Exportar Opciones, la planilla base e importarlas reemplazando el catálogo (administracion-local de
/// importar-opciones-desde-excel). El encabezado es el de D1 y se reconoce como en Personas; una fila se reconoce por su
/// nombre (D2); los valores se leen como dice D3 y el guardado es el de siempre, en una sola versión (D4). Ni el resumen ni
/// los errores muestran el valor de una celda: el campo y el motivo.
/// </summary>
public sealed partial class ImportacionOpciones
{
    public const string HojaOpciones = "Opciones";
    public const string ColumnaNombre = "Nombre";
    public const string ColumnaServicio = "Servicio";
    public const string ColumnaImporte = "Importe";
    public const string ColumnaFranjas = "Franjas";
    public const string ColumnaCargas = "Cargas";
    public const string SufijoUnidades = ": unidades";

    /// <summary>El nombre de las filas de la planilla base, seguido de su número (D5).</summary>
    public const string NombreDeEjemplo = "Opción de ejemplo";

    private static readonly CultureInfo EnEspanol = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>La hoja «Instrucciones» de la planilla base y de la exportación, una oración por fila (D5).</summary>
    private static readonly string[] Instrucciones =
    [
        "Una opción por fila en la hoja «Opciones»; la primera fila es el encabezado y no se cambia.",
        "Al importar, la planilla reemplaza todo el catálogo: la opción que no está en ella se quita, y sus consumos siguen registrados.",
        "Una opción se reconoce por su nombre: con otro nombre es una opción nueva, y la del nombre anterior se quita.",
        "«Servicio» es el nombre de un servicio del puesto, como figura en la pestaña Opciones.",
        "«Importe» es lo que paga el empleado en pesos, de 0 a 9.999.999,99; con 0, la opción no muestra importe.",
        "Cada columna «…: unidades» es cuántas unidades de ese cupo consume la opción, de 1 a 9; vacía o 0, ninguna, y cada opción necesita al menos una.",
        "Si se borra la columna de una categoría, las opciones que ya estaban conservan sus cargas de esa categoría.",
        "«Franjas» son los horarios en que se ofrece, como «11:00 a 14:00; 18:00 a 21:00»; vacía, todo el día.",
        "Las dos filas de ejemplo se pueden reemplazar o borrar: si quedan, se agregan como opciones.",
        "Antes de aplicar, el puesto muestra un resumen con las opciones nuevas, las modificadas y las que se quitan.",
    ];

    private readonly AdministracionLocal _administracion;

    public ImportacionOpciones(AdministracionLocal administracion)
    {
        _administracion = administracion;
    }

    /// <summary>El catálogo activo en <c>INComedor-opciones-AAAAMMDD-HHmmss.xlsx</c>, en el formato que se importa (D5).</summary>
    public ResultadoExportacionOpciones Exportar(string carpeta, TimeZoneInfo zona)
    {
        var vista = _administracion.Leer();
        var filas = vista.Opciones.Select(o => Fila(vista, o)).ToList();
        var nombre = EscritorPlanilla.EscribirLibro(carpeta, Raiz("INComedor-opciones-", zona),
            [new HojaPlanilla(HojaOpciones, Encabezado(vista), filas), HojaDeInstrucciones()]);
        return new ResultadoExportacionOpciones(nombre, filas.Count);
    }

    /// <summary>La planilla base: el encabezado, dos filas de ejemplo con el primer servicio y la primera categoría (D5).</summary>
    public string EscribirPlanillaBase(string carpeta, TimeZoneInfo zona)
    {
        var vista = _administracion.Leer();
        var servicio = vista.Servicios.FirstOrDefault()?.Nombre ?? string.Empty;
        var escala = Escala(vista);
        List<object> Ejemplo(string nombreEjemplo, decimal importe, string franjas) =>
        [
            nombreEjemplo, servicio, new NumeroPlanilla(importe, escala), franjas,
            .. vista.Categorias.Select((_, i) => i == 0 ? (object)new NumeroPlanilla(1, 0) : string.Empty),
        ];

        return EscritorPlanilla.EscribirLibro(carpeta, Raiz("INComedor-planilla-opciones-", zona),
        [
            new HojaPlanilla(HojaOpciones, Encabezado(vista), [Ejemplo(NombreDeEjemplo + " 1", 1500, "11:00 a 14:00"), Ejemplo(NombreDeEjemplo + " 2", 0, string.Empty)]),
            HojaDeInstrucciones(),
        ]);
    }

    /// <summary>
    /// Lee la primera hoja y arma el resumen: modificar, agregar, dejar igual o quitar cada opción (D2 y D4). Con cualquier
    /// error, el resumen sólo trae los errores y no se aplica.
    /// </summary>
    public RevisionOpciones Revisar(Stream archivo)
    {
        IReadOnlyList<IReadOnlyList<string>> filas;
        var numericas = new HashSet<(int Fila, int Columna)>();
        try
        {
            filas = LectorPlanilla.Leer(archivo, numericas);
        }
        catch (PlanillaInvalidaException e)
        {
            return Rechazo([new ErrorImportacion(0, ImportacionPersonas.CampoArchivo, e.Motivo)]);
        }

        var vista = _administracion.Leer();
        if (vista.SoloLectura)
        {
            return Rechazo([new ErrorImportacion(0, ImportacionPersonas.CampoArchivo, INWebAdministra)]);
        }

        var errores = new List<ErrorImportacion>();
        var columnas = LeerEncabezado(filas, vista, errores);
        if (errores.Count > 0)
        {
            return Rechazo(errores);
        }

        var escala = Escala(vista);
        var servicios = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var servicio in vista.Servicios)
        {
            servicios.TryAdd(ImportacionPersonas.Normalizar(servicio.Nombre), servicio.Id);
        }

        var existentes = new Dictionary<string, OpcionAdministrada>(StringComparer.Ordinal);
        var exactas = new Dictionary<string, OpcionAdministrada>(StringComparer.Ordinal);
        foreach (var opcion in vista.Opciones)
        {
            existentes.TryAdd(ImportacionPersonas.Normalizar(opcion.Nombre), opcion);
            exactas.TryAdd(opcion.Nombre, opcion);
        }

        var categoriasDelArchivo = columnas.Categorias.Select(c => c.Categoria).ToHashSet();
        var filasDeEjemplo = 0;

        var opciones = new List<OpcionImportada>();
        var ediciones = new List<EdicionOpcion>();
        var filaDeNombre = new Dictionary<string, int>(StringComparer.Ordinal);
        var repetidas = new HashSet<int>();
        var reconocidas = new HashSet<Guid>();

        for (var i = 1; i < filas.Count; i++)
        {
            var fila = filas[i];
            var numero = i + 1;
            if (fila.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Celda(int columna) => columna < fila.Count ? fila[columna].Trim() : string.Empty;
            var erroresAntes = errores.Count;

            var nombre = Celda(columnas.Nombre);
            if (nombre.StartsWith(NombreDeEjemplo + " ", StringComparison.Ordinal))
            {
                filasDeEjemplo++;
            }

            if (AdministracionLocal.ValidarTexto(nombre, AdministracionLocal.LargoNombreOpcion) is { } errorNombre)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaNombre, errorNombre));
            }
            else if (filaDeNombre.TryGetValue(ImportacionPersonas.Normalizar(nombre), out var primera))
            {
                if (repetidas.Add(primera))
                {
                    errores.Add(new ErrorImportacion(primera, ColumnaNombre, Repetido));
                }

                errores.Add(new ErrorImportacion(numero, ColumnaNombre, Repetido));
            }
            else
            {
                filaDeNombre[ImportacionPersonas.Normalizar(nombre)] = numero;
            }

            var textoServicio = Celda(columnas.Servicio);
            Guid servicioId = default;
            if (textoServicio.Length == 0)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaServicio, Requerido));
            }
            else if (!servicios.TryGetValue(ImportacionPersonas.Normalizar(textoServicio), out servicioId))
            {
                errores.Add(new ErrorImportacion(numero, ColumnaServicio, Desconocido));
            }

            var importe = LeerImporte(Celda(columnas.Importe), numericas.Contains((i, columnas.Importe)), escala, out var errorImporte);
            if (errorImporte is not null)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaImporte, errorImporte));
            }

            var cargas = new List<CargaEditada>();
            var erroresAntesDeUnidades = errores.Count;
            foreach (var (columna, categoria, titulo) in columnas.Categorias)
            {
                var valor = Celda(columna);
                if (valor.Length == 0)
                {
                    continue;
                }

                if (!valor.All(char.IsAsciiDigit))
                {
                    errores.Add(new ErrorImportacion(numero, titulo, NoSonDigitos));
                }
                else if (!int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var unidades) || unidades > AdministracionLocal.UnidadesMaximas)
                {
                    errores.Add(new ErrorImportacion(numero, titulo, FueraDeRango));
                }
                else if (unidades > 0)
                {
                    // «0» o «00»: sin carga en esa categoría.
                    cargas.Add(new CargaEditada(categoria, unidades));
                }
            }

            var existente = exactas.GetValueOrDefault(nombre) ?? existentes.GetValueOrDefault(ImportacionPersonas.Normalizar(nombre));
            if (existente is not null)
            {
                cargas.InsertRange(0, existente.Cargas.Where(c => !categoriasDelArchivo.Contains(c.CategoriaId)).Select(c => new CargaEditada(c.CategoriaId, c.Unidades)));
            }

            // «Sin cargas» sólo si ninguna celda de unidades tuvo su propio error: si no, sería el mismo error dicho dos veces.
            if (cargas.Count == 0 && errores.Count == erroresAntesDeUnidades)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaCargas, Requerido));
            }

            var franjas = LeerFranjas(Celda(columnas.Franjas), out var errorFranjas);
            if (errorFranjas is not null)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaFranjas, errorFranjas));
            }

            if (errores.Count > erroresAntes)
            {
                continue;
            }

            var edicion = new EdicionOpcion(null, nombre, servicioId, importe, cargas, franjas);
            if (existente is not null && reconocidas.Add(existente.Id))
            {
                var igual = existente.Nombre == nombre && existente.ServicioId == servicioId && existente.ImporteUnidadesMenores == importe
                    && existente.Cargas.Select(c => (c.CategoriaId, c.Unidades)).ToHashSet().SetEquals(cargas.Select(c => (c.CategoriaId, c.Unidades)))
                    && existente.Franjas.Select(f => (Hora(f.Inicio), Hora(f.Fin))).ToHashSet().SetEquals(franjas.Select(f => (f.Inicio, f.Fin)));
                opciones.Add(new OpcionImportada(numero, existente.Id, nombre, igual ? AccionOpcionImportada.SinCambios : AccionOpcionImportada.Modificada));
                if (!igual)
                {
                    ediciones.Add(edicion with { OpcionId = existente.Id });
                }
            }
            else
            {
                opciones.Add(new OpcionImportada(numero, null, nombre, AccionOpcionImportada.Agregada));
                ediciones.Add(edicion);
            }
        }

        // Un archivo sin opciones dejaría la atención sin nada que ofrecer (D4).
        if (errores.Count == 0 && opciones.Count == 0)
        {
            errores.Add(new ErrorImportacion(0, HojaOpciones, Requerido));
        }

        if (errores.Count > 0)
        {
            return Rechazo(errores);
        }

        // Reemplazar: la opción que no está en el archivo se quita del catálogo activo, sin tocar sus consumos (D2).
        foreach (var quitada in vista.Opciones.Where(o => !reconocidas.Contains(o.Id)))
        {
            opciones.Add(new OpcionImportada(null, quitada.Id, quitada.Nombre, AccionOpcionImportada.Quitada));
            ediciones.Add(new EdicionOpcion(quitada.Id, quitada.Nombre, quitada.ServicioId, quitada.ImporteUnidadesMenores,
                quitada.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(),
                quitada.Franjas.Select(f => new FranjaEditada(Hora(f.Inicio), Hora(f.Fin))).ToList(), Quitar: true));
        }

        return new RevisionOpciones([], opciones, ediciones) { FilasDeEjemplo = filasDeEjemplo };
    }

    /// <summary>
    /// Aplica un resumen sin errores en una sola versión, con el actor como un guardado a mano; si algo cambió entre el resumen
    /// y el toque, el guardado lo rechaza como siempre. Sin nada que cambiar, no crea una versión (D4).
    /// </summary>
    public ResultadoImportacionOpciones Aplicar(RevisionOpciones revision, Guid? actor)
    {
        if (!revision.Valida)
        {
            throw new InvalidOperationException("Una importación con errores no se aplica.");
        }

        if (revision.Ediciones.Count == 0)
        {
            return new ResultadoImportacionOpciones(
                new ResultadoGuardado(TipoGuardado.Aplicado, _administracion.Leer().VersionConfiguracion, null, []), revision.Opciones);
        }

        var guardado = _administracion.GuardarOpciones(revision.Ediciones, actor);
        if (guardado.Tipo != TipoGuardado.Aplicado)
        {
            return new ResultadoImportacionOpciones(guardado, revision.Opciones);
        }

        var porNombre = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var opcion in _administracion.Leer().Opciones)
        {
            porNombre.TryAdd(ImportacionPersonas.Normalizar(opcion.Nombre), opcion.Id);
        }

        return new ResultadoImportacionOpciones(guardado, revision.Opciones
            .Select(o => o.Accion == AccionOpcionImportada.Agregada && porNombre.TryGetValue(ImportacionPersonas.Normalizar(o.Nombre), out var id)
                ? o with { OpcionId = id }
                : o)
            .ToList());
    }

    private static IReadOnlyList<string> Encabezado(VistaAdministracion vista) =>
        [ColumnaNombre, ColumnaServicio, ColumnaImporte, ColumnaFranjas, .. vista.Categorias.Select(c => c.Nombre + SufijoUnidades)];

    private static IReadOnlyList<object> Fila(VistaAdministracion vista, OpcionAdministrada opcion) =>
    [
        opcion.Nombre, opcion.Servicio, new NumeroPlanilla(opcion.ImporteUnidadesMenores / Potencia(opcion.Escala), opcion.Escala), TextoFranjas(opcion.Franjas),
        .. vista.Categorias.Select(c => opcion.Cargas.FirstOrDefault(x => x.CategoriaId == c.Id) is { } carga ? (object)new NumeroPlanilla(carga.Unidades, 0) : string.Empty),
    ];

    private static HojaPlanilla HojaDeInstrucciones() =>
        new("Instrucciones", ["Cómo completar la planilla"], Instrucciones.Select(t => (IReadOnlyList<object>)new object[] { t }));

    private string Raiz(string prefijo, TimeZoneInfo zona) =>
        prefijo + TimeZoneInfo.ConvertTime(_administracion.Reloj.GetUtcNow(), zona).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    private static int Escala(VistaAdministracion vista) => vista.Opciones.FirstOrDefault()?.Escala ?? 2;

    private static decimal Potencia(int escala) => (decimal)Math.Pow(10, escala);

    private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string TextoFranjas(IEnumerable<FranjaHoraria> franjas) => string.Join("; ", franjas.Select(f => $"{Hora(f.Inicio)} a {Hora(f.Fin)}"));

    /// <summary>
    /// El importe en unidades menores (D3): un número de Excel en cultura invariante, o un texto con coma decimal de es-AR;
    /// con más decimales que la escala o fuera de 0 a 9.999.999,99, fuera de rango.
    /// </summary>
    private static long LeerImporte(string texto, bool numerico, int escala, out string? error)
    {
        error = null;
        if (texto.Length == 0)
        {
            error = Requerido;
            return 0;
        }

        // Un texto lleva coma decimal y, si tiene puntos, de miles («1.800,50»): con otra forma («1800.50», «1.8»), es-AR
        // ignoraría dónde cae el punto y leería otro importe.
        decimal valor = 0;
        var leido = numerico
            ? decimal.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
            : ImporteEnTexto().IsMatch(texto) && decimal.TryParse(texto, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, EnEspanol, out valor);
        if (!leido)
        {
            error = NoSonDigitos;
            return 0;
        }

        // El tope antes de multiplicar: un número enorme desbordaría el decimal.
        if (valor < 0 || valor > AdministracionLocal.ImporteMaximo / Potencia(escala))
        {
            error = FueraDeRango;
            return 0;
        }

        var menores = valor * Potencia(escala);
        // Un número de Excel arrastra el ruido de la coma flotante (0,30000000000000004): se lleva a la escala si es ruido.
        if (numerico && Math.Abs(menores - Math.Round(menores)) < 0.0001m)
        {
            menores = Math.Round(menores);
        }

        if (menores != decimal.Truncate(menores))
        {
            error = FueraDeRango;
            return 0;
        }

        return (long)menores;
    }

    /// <summary>Las franjas «HH:mm a HH:mm» separadas por punto y coma; vacías, todo el día (D3).</summary>
    private static List<FranjaEditada> LeerFranjas(string texto, out string? error)
    {
        error = null;
        var franjas = new List<FranjaEditada>();
        foreach (var parte in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var coincidencia = UnaFranja().Match(parte);
            if (!coincidencia.Success
                || !AdministracionLocal.LeerHoraEditada(coincidencia.Groups[1].Value.PadLeft(5, '0'), out var inicio)
                || !AdministracionLocal.LeerHoraEditada(coincidencia.Groups[2].Value.PadLeft(5, '0'), out var fin))
            {
                error = HoraInvalida;
                return [];
            }

            if (inicio == fin)
            {
                error = FranjaVacia;
                return [];
            }

            franjas.Add(new FranjaEditada(Hora(inicio), Hora(fin)));
        }

        return franjas;
    }

    private static Columnas LeerEncabezado(IReadOnlyList<IReadOnlyList<string>> filas, VistaAdministracion vista, List<ErrorImportacion> errores)
    {
        var encabezado = filas.Count > 0 ? filas[0] : [];
        var ancho = filas.Count > 0 ? filas.Max(f => f.Count) : 0;
        var categorias = new Dictionary<string, (Guid Id, string Titulo)>(StringComparer.Ordinal);
        foreach (var categoria in vista.Categorias)
        {
            categorias.TryAdd(ImportacionPersonas.Normalizar(categoria.Nombre + SufijoUnidades), (categoria.Id, categoria.Nombre + SufijoUnidades));
        }

        var fijas = new Dictionary<string, int>(StringComparer.Ordinal);
        var columnasCategoria = new List<(int, Guid, string)>();
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        string[] nombresFijos = [ColumnaNombre, ColumnaServicio, ColumnaImporte, ColumnaFranjas];

        for (var c = 0; c < ancho; c++)
        {
            var clave = ImportacionPersonas.Normalizar(c < encabezado.Count ? encabezado[c] : string.Empty);
            if (clave.Length == 0)
            {
                var columna = c;
                if (filas.Skip(1).Any(f => columna < f.Count && !string.IsNullOrWhiteSpace(f[columna])))
                {
                    errores.Add(new ErrorImportacion(1, ImportacionPersonas.LetraDeColumna(c), ImportacionPersonas.ColumnaDesconocida));
                }

                continue;
            }

            if (!vistas.Add(clave))
            {
                errores.Add(new ErrorImportacion(1, ImportacionPersonas.LetraDeColumna(c), Repetido));
            }
            else if (nombresFijos.FirstOrDefault(n => ImportacionPersonas.Normalizar(n) == clave) is { } fija)
            {
                fijas[fija] = c;
            }
            else if (categorias.TryGetValue(clave, out var categoria))
            {
                columnasCategoria.Add((c, categoria.Id, categoria.Titulo));
            }
            else
            {
                errores.Add(new ErrorImportacion(1, ImportacionPersonas.LetraDeColumna(c), ImportacionPersonas.ColumnaDesconocida));
            }
        }

        foreach (var fija in nombresFijos.Where(n => !fijas.ContainsKey(n)))
        {
            errores.Add(new ErrorImportacion(1, fija, Requerido));
        }

        return new Columnas(
            fijas.GetValueOrDefault(ColumnaNombre, -1), fijas.GetValueOrDefault(ColumnaServicio, -1),
            fijas.GetValueOrDefault(ColumnaImporte, -1), fijas.GetValueOrDefault(ColumnaFranjas, -1), columnasCategoria);
    }

    private static RevisionOpciones Rechazo(IReadOnlyList<ErrorImportacion> errores) => new(errores, [], []);

    [GeneratedRegex("^(\\d{1,2}:\\d{2})\\s*(?:a|-)\\s*(\\d{1,2}:\\d{2})\\z", RegexOptions.IgnoreCase)]
    private static partial Regex UnaFranja();

    [GeneratedRegex("^(\\d{1,3}(\\.\\d{3})+|\\d+)(,\\d+)?\\z")]
    private static partial Regex ImporteEnTexto();

    private sealed record Columnas(int Nombre, int Servicio, int Importe, int Franjas, IReadOnlyList<(int Columna, Guid Categoria, string Titulo)> Categorias);
}
