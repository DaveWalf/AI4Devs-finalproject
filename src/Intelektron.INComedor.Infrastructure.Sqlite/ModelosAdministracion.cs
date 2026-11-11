using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>De dónde vino la versión activa (D3 de administrar-personas-y-opciones): sin fila de origen, es local.</summary>
public enum OrigenConfiguracion
{
    Local,
    INWeb,
}

public sealed record ServicioAdministrado(Guid Id, string Nombre);

public sealed record CategoriaAdministrada(Guid Id, string Nombre);

/// <summary>
/// Un cupo de una persona tal como lo muestra el panel (D9): el límite de la regla que hoy decide ese servicio y categoría.
/// <see cref="ReglasQueSeReemplazan"/> avisa que el par tiene reglas con vigencia o prioridad que un guardado reemplaza.
/// <see cref="UsadoHoy"/> es lo usado en su categoría en la fecha de negocio actual, la «x» del «x/n» (D1 de
/// editar-raciones-en-personas).
/// </summary>
public sealed record CupoAdministrado(Guid ServicioId, string Servicio, Guid CategoriaId, string Categoria, int Limite, bool ReglasQueSeReemplazan, int UsadoHoy = 0);

/// <summary>Una persona de la versión activa, sin su código (D10): estado, cupos y las opciones que esos cupos habilitan.</summary>
public sealed record PersonaAdministrada(
    Guid Id,
    string Nombre,
    bool Activa,
    IReadOnlyList<CupoAdministrado> Cupos,
    IReadOnlyList<Guid> OpcionesHabilitadas);

public sealed record CargaAdministrada(Guid CategoriaId, string Categoria, int Unidades);

/// <summary>Una opción de la versión activa con todos sus parámetros y sus franjas.</summary>
public sealed record OpcionAdministrada(
    Guid Id,
    int Version,
    string Nombre,
    Guid ServicioId,
    string Servicio,
    string Moneda,
    int Escala,
    long ImporteUnidadesMenores,
    IReadOnlyList<CargaAdministrada> Cargas,
    IReadOnlyList<FranjaHoraria> Franjas);

/// <summary>Lo que los paneles muestran de la versión activa; con INWeb como dueño, sólo para leer (D3).</summary>
public sealed record VistaAdministracion(
    int VersionConfiguracion,
    OrigenConfiguracion Origen,
    IReadOnlyList<ServicioAdministrado> Servicios,
    IReadOnlyList<CategoriaAdministrada> Categorias,
    IReadOnlyList<PersonaAdministrada> Personas,
    IReadOnlyList<OpcionAdministrada> Opciones)
{
    public bool SoloLectura => Origen == OrigenConfiguracion.INWeb;
}

/// <summary>El límite diario de una persona para un servicio y una categoría, de 0 a 99 (D9).</summary>
public sealed record CupoEditado(Guid ServicioId, Guid CategoriaId, int Limite);

/// <summary>
/// Una persona como queda después de editarla: sin <see cref="PersonaId"/> es una persona nueva, que lleva su código;
/// con <see cref="Quitar"/>, una baja (D8). Sus cupos son la lista entera: un par que falta queda sin regla.
/// </summary>
public sealed record EdicionPersona(
    Guid? PersonaId,
    string Nombre,
    IReadOnlyList<CupoEditado> Cupos,
    string? Codigo = null,
    bool Quitar = false);

public sealed record CargaEditada(Guid CategoriaId, int Unidades);

/// <summary>Una franja como se escribe en el panel, en HH:mm de la hora local del puesto.</summary>
public sealed record FranjaEditada(string Inicio, string Fin);

/// <summary>Una opción como queda después de editarla: sin <see cref="OpcionId"/> es nueva; con <see cref="Quitar"/>, sale del catálogo.</summary>
public sealed record EdicionOpcion(
    Guid? OpcionId,
    string Nombre,
    Guid ServicioId,
    long ImporteUnidadesMenores,
    IReadOnlyList<CargaEditada> Cargas,
    IReadOnlyList<FranjaEditada> Franjas,
    bool Quitar = false);

public enum TipoGuardado
{
    Aplicado,
    Rechazado,
    Denegado,
}

/// <summary>Un campo que el guardado rechaza: la edición por su posición, el campo y el código del error, nunca el valor.</summary>
public sealed record CampoRechazado(int Indice, string Campo, string Codigo);

/// <summary>
/// El resultado de un guardado (D11): aplicado con la versión nueva activa, rechazado con sus campos o denegado porque los
/// datos los administra INWeb. <see cref="VersionActiva"/> es el número de configuración activo al terminar.
/// </summary>
public sealed record ResultadoGuardado(TipoGuardado Tipo, int VersionActiva, string? Motivo, IReadOnlyList<CampoRechazado> Campos);

/// <summary>Los códigos de motivo y de campo que devuelve un guardado y que quedan en la bitácora (D11 y D13).</summary>
public static class CodigosAdministracion
{
    public const string CampoInvalido = "INVALID_FIELD";
    public const string CodigoDuplicado = "DUPLICATE_CODE";
    public const string INWebAdministra = "INWEB_OWNS_DATA";

    public const string Requerido = "REQUIRED";
    public const string DemasiadoLargo = "TOO_LONG";
    public const string CaracterDeControl = "CONTROL_CHARACTER";
    public const string NoSonDigitos = "NOT_DIGITS";
    public const string FueraDeRango = "OUT_OF_RANGE";
    public const string Desconocido = "UNKNOWN";
    public const string Repetido = "REPEATED";
    public const string NoEditable = "NOT_EDITABLE";
    public const string HoraInvalida = "INVALID_TIME";
    public const string FranjaVacia = "EMPTY_RANGE";
}
