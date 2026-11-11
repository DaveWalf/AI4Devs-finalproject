using System.Globalization;
using System.Text;
using static Intelektron.INComedor.Infrastructure.Sqlite.CodigosAdministracion;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Cómo se aplica una importación: modifica y agrega, o además da de baja a quien no está (D4).</summary>
public enum ModoImportacion
{
    ModificarYAgregar,
    Reemplazar,
}

/// <summary>Lo que una importación hace con una persona.</summary>
public enum AccionImportada
{
    Agregada,
    Modificada,
    SinCambios,
    DadaDeBaja,
}

/// <summary>Un error de una fila del archivo (la 1 es el encabezado; la 0, el archivo entero): el campo y su motivo, nunca el valor.</summary>
public sealed record ErrorImportacion(int Fila, string Campo, string Motivo);

/// <summary>Una persona del resumen: su fila (null si se da de baja por no estar), su id si ya existe, su nombre y lo que pasa.</summary>
public sealed record PersonaImportada(int? Fila, Guid? PersonaId, string Nombre, AccionImportada Accion);

/// <summary>El resumen de una importación antes de aplicarla.</summary>
public sealed record RevisionImportacion(
    IReadOnlyList<ErrorImportacion> Errores,
    IReadOnlyList<PersonaImportada> Personas,
    IReadOnlyList<PersonaImportada> DeBajaAlReemplazar,
    IReadOnlyList<EdicionPersona> Ediciones,
    IReadOnlyList<EdicionPersona> Bajas)
{
    public bool Valida => Errores.Count == 0;

    public int Agregadas => Personas.Count(p => p.Accion == AccionImportada.Agregada);

    public int Modificadas => Personas.Count(p => p.Accion == AccionImportada.Modificada);

    public int SinCambios => Personas.Count(p => p.Accion == AccionImportada.SinCambios);

    /// <summary>Cuántos códigos venían guardados como número: Excel les pudo quitar los ceros del principio (D3).</summary>
    public int CodigosComoNumero { get; init; }

    /// <summary>Cuántas filas siguen siendo las de la planilla de ejemplo, que se agregarían como personas (D4).</summary>
    public int FilasDeEjemplo { get; init; }

    /// <summary>El código de cada fila agregada, sólo en memoria para hallar su id al aplicar: nunca se muestra ni se registra.</summary>
    internal IReadOnlyDictionary<int, string> CodigosAgregados { get; init; } = new Dictionary<int, string>();
}

/// <summary>Lo que dejó una importación aplicada: el guardado y cada persona con su id.</summary>
public sealed record ResultadoImportacion(ResultadoGuardado Guardado, IReadOnlyList<PersonaImportada> Personas);

/// <summary>La versión activa y lo que la importación consulta de ella (ver <see cref="AdministracionLocal.LeerParaImportar"/>).</summary>
internal sealed record PadronImportacion(
    VistaAdministracion Vista,
    IReadOnlyDictionary<string, Guid> Vigentes,
    IReadOnlySet<string> Usados,
    IReadOnlySet<Guid> ConPermiso);

/// <summary>
/// La importación de Personas desde un <c>.xlsx</c> (administracion-local de importar-personas-desde-excel).
/// <see cref="Revisar"/> lee la planilla y arma el resumen sin escribir nada (D2 a D5); <see cref="Aplicar"/> lo guarda con
/// <see cref="AdministracionLocal.GuardarPersonas"/>, que valida otra vez, en una sola versión; y
/// <see cref="EscribirPlanillaDeEjemplo"/> escribe la planilla para completar (D6). Ningún error ni resumen lleva un código:
/// los códigos viven en memoria hasta guardar.
/// </summary>
public sealed class ImportacionPersonas
{
    public const string ColumnaDesconocida = "UNKNOWN_COLUMN";
    public const string CodigoDadoDeBaja = "INACTIVE_CODE";

    /// <summary>Un código de más de 15 dígitos guardado como número: Excel ya le cambió los últimos (D3).</summary>
    public const string CodigoComoNumero = "NUMERIC_CODE";

    /// <summary>Dos filas con dos códigos distintos de la misma persona (D3).</summary>
    public const string MismaPersona = "SAME_PERSON";

    /// <summary>El nombre de las filas de la planilla de ejemplo, seguido de su número (D6).</summary>
    public const string NombreDeEjemplo = "Persona de ejemplo";
    public const string ColumnaNombre = "Nombre";
    public const string ColumnaCodigo = "Código";
    public const string SufijoLimite = ": límite";
    public const string HojaPersonas = "Personas";
    public const string HojaInstrucciones = "Instrucciones";
    public const string CampoArchivo = "Archivo";

    /// <summary>La hoja «Instrucciones» de la planilla de ejemplo, una oración por fila (D6).</summary>
    private static readonly string[] Instrucciones =
    [
        "Una persona por fila en la hoja «Personas»; la primera fila es el encabezado y no se cambia.",
        "«Código» es el número con que la persona se identifica: sólo dígitos, hasta 20, con sus ceros a la izquierda.",
        "Un código que ya está en el puesto modifica a esa persona; un código nuevo la agrega.",
        "El código de una persona dada de baja no se puede usar: no se reasigna.",
        "Cada columna «…: límite» es cuántas raciones por día puede retirar la persona, de 0 a 99; vacía, la persona no tiene ese cupo.",
        "Si se borra una columna de cupo, ese cupo queda como estaba.",
        "Antes de aplicar, el puesto muestra un resumen: «Modificar y agregar» deja a las demás personas como están y «Reemplazar» da de baja a las que no están en la planilla, salvo a los operadores y al administrador.",
        "Lo consumido hoy sigue contando contra los límites nuevos.",
        "Las dos filas de ejemplo se pueden reemplazar o borrar.",
    ];

    private readonly AdministracionLocal _administracion;

    public ImportacionPersonas(AdministracionLocal administracion)
    {
        _administracion = administracion;
    }

    /// <summary>
    /// Lee la primera hoja y arma el resumen: una fila se reconoce por su código (D3), una columna de cupo que falta deja ese
    /// cupo como estaba y una celda vacía lo quita (D2). Con cualquier error, el resumen sólo trae los errores y no se aplica.
    /// </summary>
    public RevisionImportacion Revisar(Stream archivo)
    {
        IReadOnlyList<IReadOnlyList<string>> filas;
        var numericas = new HashSet<(int Fila, int Columna)>();
        try
        {
            filas = LectorPlanilla.Leer(archivo, numericas);
        }
        catch (PlanillaInvalidaException e)
        {
            return Rechazo([new ErrorImportacion(0, CampoArchivo, e.Motivo)]);
        }

        var padron = _administracion.LeerParaImportar();
        var vista = padron.Vista;
        if (vista.SoloLectura)
        {
            return Rechazo([new ErrorImportacion(0, CampoArchivo, INWebAdministra)]);
        }

        var errores = new List<ErrorImportacion>();
        var columnas = LeerEncabezado(filas, vista, errores);
        if (errores.Count > 0)
        {
            return Rechazo(errores);
        }

        var actuales = vista.Personas.ToDictionary(p => p.Id);
        var paresDelArchivo = columnas.Cupos.Select(c => c.Par).ToHashSet();
        var personas = new List<PersonaImportada>();
        var ediciones = new List<EdicionPersona>();
        var codigosAgregados = new Dictionary<int, string>();
        var filaDeCodigo = new Dictionary<string, int>(StringComparer.Ordinal);
        var repetidas = new HashSet<int>();
        var reconocidas = new HashSet<Guid>();
        var codigosComoNumero = 0;
        var filasDeEjemplo = 0;

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

            if (AdministracionLocal.ValidarTexto(nombre, AdministracionLocal.LargoNombrePersona) is { } errorNombre)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaNombre, errorNombre));
            }

            var codigo = Celda(columnas.Codigo);
            var codigoNumerico = numericas.Contains((i, columnas.Codigo));
            Guid? id = null;
            if (AdministracionLocal.ValidarCodigo(codigo) is { } errorCodigo)
            {
                errores.Add(new ErrorImportacion(numero, ColumnaCodigo, errorCodigo));
            }
            else if (codigoNumerico && codigo.Length > 15)
            {
                // Excel guarda un número con 15 cifras: los dígitos que siguen ya no son los que se escribieron.
                errores.Add(new ErrorImportacion(numero, ColumnaCodigo, CodigoComoNumero));
            }
            else if (filaDeCodigo.TryGetValue(codigo, out var primera))
            {
                // Un código repetido es error de las dos filas (D3).
                if (repetidas.Add(primera))
                {
                    errores.Add(new ErrorImportacion(primera, ColumnaCodigo, Repetido));
                }

                errores.Add(new ErrorImportacion(numero, ColumnaCodigo, Repetido));
            }
            else
            {
                filaDeCodigo[codigo] = numero;
                if (padron.Vigentes.TryGetValue(codigo, out var existente))
                {
                    // Dos códigos de la misma persona en dos filas: la segunda la repite con otro código.
                    if (!reconocidas.Add(existente))
                    {
                        errores.Add(new ErrorImportacion(numero, ColumnaCodigo, MismaPersona));
                    }

                    id = existente;
                }
                else if (padron.Usados.Contains(codigo))
                {
                    errores.Add(new ErrorImportacion(numero, ColumnaCodigo, CodigoDadoDeBaja));
                }
            }

            var cupos = new List<CupoEditado>();
            foreach (var (columna, titulo, par) in columnas.Cupos)
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
                else if (!int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var limite) || limite > AdministracionLocal.LimiteMaximo)
                {
                    errores.Add(new ErrorImportacion(numero, titulo, FueraDeRango));
                }
                else
                {
                    cupos.Add(new CupoEditado(par.Servicio, par.Categoria, limite));
                }
            }

            if (errores.Count > erroresAntes)
            {
                continue;
            }

            if (codigoNumerico)
            {
                codigosComoNumero++;
            }

            if (id is { } personaId)
            {
                var actual = actuales[personaId];
                List<CupoEditado> finales =
                [
                    .. actual.Cupos.Where(c => !paresDelArchivo.Contains((c.ServicioId, c.CategoriaId)))
                        .Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)),
                    .. cupos,
                ];
                var igual = nombre == actual.Nombre && MismosCupos(actual.Cupos, finales);
                personas.Add(new PersonaImportada(numero, personaId, nombre, igual ? AccionImportada.SinCambios : AccionImportada.Modificada));
                if (!igual)
                {
                    ediciones.Add(new EdicionPersona(personaId, nombre, finales));
                }
            }
            else
            {
                personas.Add(new PersonaImportada(numero, null, nombre, AccionImportada.Agregada));
                ediciones.Add(new EdicionPersona(null, nombre, cupos, codigo));
                codigosAgregados[numero] = codigo;
            }
        }

        // Una planilla sin personas es un error del archivo: «Reemplazar» con ella daría de baja a todos.
        if (errores.Count == 0 && personas.Count == 0)
        {
            errores.Add(new ErrorImportacion(0, HojaPersonas, Requerido));
        }

        if (errores.Count > 0)
        {
            return Rechazo(errores);
        }

        // «Reemplazar» da de baja a las activas que no están, salvo a quien tiene un permiso de operador o de administrador (D4).
        var presentes = personas.Where(p => p.PersonaId is not null).Select(p => p.PersonaId!.Value).ToHashSet();
        var quedanAfuera = vista.Personas.Where(p => p.Activa && !presentes.Contains(p.Id) && !padron.ConPermiso.Contains(p.Id)).ToList();
        var deBaja = quedanAfuera.Select(p => new PersonaImportada(null, p.Id, p.Nombre, AccionImportada.DadaDeBaja)).ToList();
        var bajas = quedanAfuera
            .Select(p => new EdicionPersona(p.Id, p.Nombre, p.Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList(), Quitar: true))
            .ToList();

        return new RevisionImportacion([], personas, deBaja, ediciones, bajas)
        {
            CodigosAgregados = codigosAgregados,
            CodigosComoNumero = codigosComoNumero,
            FilasDeEjemplo = filasDeEjemplo,
        };
    }

    /// <summary>
    /// Aplica un resumen sin errores en el modo elegido, en una sola versión, con el actor como un guardado a mano. Si algo
    /// cambió entre el resumen y el toque, el guardado lo rechaza como siempre. Sin nada que cambiar, no crea una versión.
    /// </summary>
    public ResultadoImportacion Aplicar(RevisionImportacion revision, ModoImportacion modo, Guid? actor)
    {
        if (!revision.Valida)
        {
            throw new InvalidOperationException("Una importación con errores no se aplica.");
        }

        var ediciones = new List<EdicionPersona>(revision.Ediciones);
        var personas = new List<PersonaImportada>(revision.Personas);
        if (modo == ModoImportacion.Reemplazar)
        {
            ediciones.AddRange(revision.Bajas);
            personas.AddRange(revision.DeBajaAlReemplazar);
        }

        if (ediciones.Count == 0)
        {
            return new ResultadoImportacion(new ResultadoGuardado(TipoGuardado.Aplicado, _administracion.Leer().VersionConfiguracion, null, []), personas);
        }

        var guardado = _administracion.GuardarPersonas(ediciones, actor);
        if (guardado.Tipo != TipoGuardado.Aplicado)
        {
            return new ResultadoImportacion(guardado, personas);
        }

        var vigentes = _administracion.LeerParaImportar().Vigentes;
        return new ResultadoImportacion(guardado, personas
            .Select(p => p.Accion == AccionImportada.Agregada && p.Fila is { } fila
                && revision.CodigosAgregados.TryGetValue(fila, out var codigo) && vigentes.TryGetValue(codigo, out var id)
                    ? p with { PersonaId = id }
                    : p)
            .ToList());
    }

    /// <summary>
    /// Escribe <c>INComedor-planilla-personas-AAAAMMDD-HHmmss.xlsx</c> en la carpeta (D6): la hoja «Personas» con el encabezado
    /// de D2, dos filas sintéticas con códigos que nadie usó en el puesto y el código con formato de texto, y la hoja
    /// «Instrucciones». Devuelve el nombre del archivo, nunca la ruta.
    /// </summary>
    public string EscribirPlanillaDeEjemplo(string carpeta, TimeZoneInfo zona)
    {
        var padron = _administracion.LeerParaImportar();
        var vista = padron.Vista;
        var pares = vista.ParesDeCupo();
        List<string> encabezado = [ColumnaNombre, ColumnaCodigo, .. pares.Select(p => vista.NombrePar(p) + SufijoLimite)];
        var codigos = CodigosLibres(padron.Usados).Take(2).ToList();

        var primera = new List<object> { "Persona de ejemplo 1", codigos[0] };
        primera.AddRange(pares.Select(_ => (object)new NumeroPlanilla(1, 0)));
        var segunda = new List<object> { "Persona de ejemplo 2", codigos[1] };
        segunda.AddRange(pares.Select((_, i) => i == 0 ? (object)new NumeroPlanilla(2, 0) : string.Empty));

        var ahora = TimeZoneInfo.ConvertTime(_administracion.Reloj.GetUtcNow(), zona);
        var raiz = "INComedor-planilla-personas-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return EscritorPlanilla.EscribirLibro(carpeta, raiz,
        [
            new HojaPlanilla(HojaPersonas, encabezado, [primera, segunda], [1]),
            new HojaPlanilla(HojaInstrucciones, ["Cómo completar la planilla"], Instrucciones.Select(t => (IReadOnlyList<object>)new object[] { t })),
        ]);
    }

    /// <summary>
    /// Las columnas de la fila 1 (D2): sin distinguir mayúsculas, espacios de más ni tildes; lo que falta o sobra va a errores.
    /// Una columna que sobra o se repite se nombra por su letra, porque su título lo escribe cualquiera y puede ser un código.
    /// </summary>
    private static Columnas LeerEncabezado(IReadOnlyList<IReadOnlyList<string>> filas, VistaAdministracion vista, List<ErrorImportacion> errores)
    {
        var encabezado = filas.Count > 0 ? filas[0] : [];
        var ancho = filas.Count > 0 ? filas.Max(f => f.Count) : 0;
        // Dos cupos que se escriben igual salvo mayúsculas o tildes quedan con el primero: el segundo no se puede nombrar aparte.
        var cupos = new Dictionary<string, (Guid Servicio, Guid Categoria)>(StringComparer.Ordinal);
        foreach (var par in vista.ParesDeCupo())
        {
            cupos.TryAdd(Normalizar(vista.NombrePar(par) + SufijoLimite), par);
        }

        int? nombre = null;
        int? codigo = null;
        var columnasCupo = new List<(int, string, (Guid, Guid))>();
        var vistas = new HashSet<string>(StringComparer.Ordinal);

        for (var c = 0; c < ancho; c++)
        {
            var titulo = c < encabezado.Count ? encabezado[c].Trim() : string.Empty;
            var clave = Normalizar(titulo);
            if (clave.Length == 0)
            {
                // Una columna sin título sólo es un error si tiene algo escrito.
                var columna = c;
                if (filas.Skip(1).Any(f => columna < f.Count && !string.IsNullOrWhiteSpace(f[columna])))
                {
                    errores.Add(new ErrorImportacion(1, LetraDeColumna(c), ColumnaDesconocida));
                }

                continue;
            }

            if (!vistas.Add(clave))
            {
                errores.Add(new ErrorImportacion(1, LetraDeColumna(c), Repetido));
            }
            else if (clave == Normalizar(ColumnaNombre))
            {
                nombre = c;
            }
            else if (clave == Normalizar(ColumnaCodigo))
            {
                codigo = c;
            }
            else if (cupos.TryGetValue(clave, out var par))
            {
                columnasCupo.Add((c, titulo, par));
            }
            else
            {
                errores.Add(new ErrorImportacion(1, LetraDeColumna(c), ColumnaDesconocida));
            }
        }

        if (nombre is null)
        {
            errores.Add(new ErrorImportacion(1, ColumnaNombre, Requerido));
        }

        if (codigo is null)
        {
            errores.Add(new ErrorImportacion(1, ColumnaCodigo, Requerido));
        }

        return new Columnas(nombre ?? -1, codigo ?? -1, columnasCupo);
    }

    private static RevisionImportacion Rechazo(IReadOnlyList<ErrorImportacion> errores) => new(errores, [], [], [], []);

    private static bool MismosCupos(IReadOnlyList<CupoAdministrado> actuales, IReadOnlyList<CupoEditado> finales) =>
        actuales.Select(c => (c.ServicioId, c.CategoriaId, c.Limite)).ToHashSet()
            .SetEquals(finales.Select(c => (c.ServicioId, c.CategoriaId, c.Limite)));

    private static string Normalizar(string texto)
    {
        var sinTildes = new StringBuilder();
        foreach (var letra in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(letra);
            }
        }

        return string.Join(' ', sinTildes.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    /// <summary>La letra de una columna desde 0, como la muestra Excel (la 0 es «A»; la 26, «AA»).</summary>
    private static string LetraDeColumna(int columna)
    {
        var letras = string.Empty;
        for (var n = columna + 1; n > 0; n = (n - 1) / 26)
        {
            letras = (char)('A' + ((n - 1) % 26)) + letras;
        }

        return letras;
    }

    /// <summary>Códigos de ejemplo desde 1001 que ninguna versión del puesto usó (D6: ningún código de una persona del puesto).</summary>
    private static IEnumerable<string> CodigosLibres(IReadOnlySet<string> usados)
    {
        for (var n = 1001; ; n++)
        {
            var codigo = n.ToString(CultureInfo.InvariantCulture);
            if (!usados.Contains(codigo))
            {
                yield return codigo;
            }
        }
    }

    private sealed record Columnas(int Nombre, int Codigo, IReadOnlyList<(int Columna, string Titulo, (Guid Servicio, Guid Categoria) Par)> Cupos);
}
