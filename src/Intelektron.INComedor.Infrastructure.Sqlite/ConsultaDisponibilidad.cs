using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Infrastructure.Sqlite.LecturaDecision;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Una opción del snapshot activo que la evaluación no autoriza a la persona, con el código de contrato de su motivo.</summary>
public sealed record OpcionRetenida(Guid Id, int Version, string Motivo);

/// <summary>Una categoría de cupo que la persona ya agotó en la fecha de negocio: su nombre, lo usado y su límite.</summary>
public sealed record CategoriaAgotada(Guid Id, string Nombre, int Usado, int Limite);

/// <summary>Lo que el puesto le ofrece a una persona al identificarse y por qué retiene el resto (D2 de ofrecer-solo-opciones-disponibles).</summary>
public sealed record OfertaDePersona(
    IReadOnlyList<OpcionDeCatalogo> Ofrecidas,
    IReadOnlyList<OpcionRetenida> Retenidas,
    IReadOnlyList<CategoriaAgotada> Agotadas);

/// <summary>
/// La oferta por persona: evalúa cada opción del snapshot activo con la misma evaluación que la confirmación y deja el
/// registro OPTIONS_OFFERED en la bitácora. No escribe intención, consumo, carga de cupo ni evento de salida (RB05).
/// </summary>
public sealed class ConsultaDisponibilidad
{
    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.ConsultaDisponibilidad");

    public ConsultaDisponibilidad(BaseComedor baseComedor, TimeProvider reloj)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
    }

    /// <summary>
    /// Evalúa cada opción del snapshot activo, en el orden del catálogo, con <see cref="EvaluadorDisponibilidad"/> y el reloj
    /// de ahora, dentro de una transacción de escritura corta que sólo deja el registro OPTIONS_OFFERED (D7).
    /// </summary>
    public OfertaDePersona Ofrecer(Guid sesionId, Guid personaId)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var fecha = CalculadoraFechaNegocio.Calcular(ahora, puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio);

        var personaActiva = LeerPersonaActiva(conexion, transaccion, puesto, personaId);
        var credencial = LeerCredencialDeSesion(conexion, transaccion, puesto, sesionId);
        var usos = LeerUsos(conexion, transaccion, personaId, fecha);
        var candidatasPorServicio = new Dictionary<Guid, List<ReglaCandidata>>();

        var ofrecidas = new List<OpcionDeCatalogo>();
        var retenidas = new List<OpcionRetenida>();
        var reglasAgotables = new List<ReglaPersona>();
        foreach (var opcion in ConsultaCatalogo.Leer(conexion, transaccion, puesto))
        {
            var leida = LeerOpcion(conexion, transaccion, puesto, opcion.Id)!;
            if (!candidatasPorServicio.TryGetValue(leida.ServicioId, out var candidatas))
            {
                candidatas = LeerReglasCandidatas(conexion, transaccion, puesto, personaId, leida.ServicioId);
                candidatasPorServicio[leida.ServicioId] = candidatas;
            }

            var resultado = EvaluadorDisponibilidad.Evaluar(
                personaActiva, credencial, ahora, leida.ServicioId, leida.Cargas, candidatas, personaId, fecha, usos);
            if (resultado.Decision.Autorizada)
            {
                ofrecidas.Add(opcion);
            }
            else
            {
                retenidas.Add(new OpcionRetenida(opcion.Id, opcion.Version, CodigoDeMotivo(resultado.Decision.Motivo!.Value)));
            }

            reglasAgotables.AddRange(resultado.Reglas);
        }

        var agotadas = Agotadas(conexion, transaccion, puesto, personaId, fecha, usos, reglasAgotables);
        _bitacora.Insertar(conexion, transaccion, puesto, ahora, "OPTIONS_OFFERED", new
        {
            IdentitySessionId = sesionId,
            PersonId = personaId,
            ConfigurationVersion = puesto.VersionConfiguracion,
            // La etiqueta y el precio mostrados se suman a los campos de antes, que no cambian (D10 de escribir-bitacoras-externas).
            Offered = ofrecidas.Select(o => new
            {
                MenuItemId = o.Id,
                MenuVersion = o.Version,
                MenuLabel = o.Nombre,
                Price = new { Currency = o.Moneda, Scale = o.Escala, EmployeeChargeMinor = o.ImporteUnidadesMenores },
            }).ToList(),
            Withheld = retenidas.Select(r => new { MenuItemId = r.Id, MenuVersion = r.Version, ReasonCode = r.Motivo }).ToList(),
            Exhausted = agotadas.Select(a => new { CategoryId = a.Id, Name = a.Nombre, Used = a.Usado, Limit = a.Limite }).ToList(),
        });
        transaccion.Commit();
        return new OfertaDePersona(ofrecidas, retenidas, agotadas);
    }

    /// <summary>
    /// Una categoría está agotada si alguna regla aplicable tiene límite mayor que cero y su uso del día lo alcanza; una regla
    /// cero no es cupo usado. Salen de las reglas de cada opción, sin repetirse, en el orden de la primera opción que las cobra.
    /// </summary>
    private static List<CategoriaAgotada> Agotadas(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid personaId, FechaNegocio fecha,
        IReadOnlyDictionary<ClaveCupo, int> usos, IReadOnlyList<ReglaPersona> reglas)
    {
        var agotadas = new List<CategoriaAgotada>();
        var vistas = new HashSet<ClaveCupo>();
        Dictionary<Guid, string>? nombres = null;
        foreach (var regla in reglas)
        {
            var clave = new ClaveCupo(personaId, regla.AmbitoCupoId, regla.CategoriaCupoId, fecha);
            var usado = usos.GetValueOrDefault(clave);
            if (regla.Limite <= 0 || usado < regla.Limite || !vistas.Add(clave))
            {
                continue;
            }

            nombres ??= NombresDeCategorias(conexion, transaccion, puesto);
            agotadas.Add(new CategoriaAgotada(regla.CategoriaCupoId, nombres[regla.CategoriaCupoId], usado, regla.Limite));
        }

        return agotadas;
    }

    internal static Dictionary<Guid, string> NombresDeCategorias(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto)
    {
        var nombres = new Dictionary<Guid, string>();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comcatCodigo, comcatNombre FROM tbComedorCategoriasCupo WHERE comvcCodigo = $vc",
            ("$vc", puesto.SnapshotId));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            nombres[lector.GetGuid(0)] = lector.GetString(1);
        }

        return nombres;
    }
}
