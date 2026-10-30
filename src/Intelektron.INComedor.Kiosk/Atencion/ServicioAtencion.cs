using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Prepara la base de la atención y arma los servicios que usa la máquina de estados (D5, D7 y D10 de
/// atender-seleccion-y-confirmacion): siembra la demostración si hace falta, agrega los datos de atención y envía los
/// trabajos que una caída dejó en cola.
/// </summary>
public sealed class ServicioAtencion
{
    private BaseComedor? _base;

    public ServicioAtencion(string rutaBase, TimeProvider reloj, IImpresoraComprobantes impresora)
    {
        RutaBase = rutaBase;
        Reloj = reloj;
        Impresora = impresora;
    }

    public string RutaBase { get; }

    public TimeProvider Reloj { get; }

    public IImpresoraComprobantes Impresora { get; }

    public EstadoPreparacion Estado { get; private set; } = EstadoPreparacion.ErrorAlmacenamiento;

    public BaseComedor Base => _base ?? throw new InvalidOperationException("Preparar() debe devolver Lista antes de atender.");

    public IdentificacionPorCodigo Identificacion { get; private set; } = null!;

    public ConsultaCatalogo Catalogo { get; private set; } = null!;

    public UnidadConfirmacion Unidad { get; private set; } = null!;

    public TrabajosImpresion Trabajos { get; private set; } = null!;

    public Asistencia Asistencia { get; private set; } = null!;

    /// <summary>
    /// Abre la base, la siembra y retoma los trabajos en cola (RB17). Una versión de esquema desconocida o un error
    /// de SQLite se informan como estado, sin tocar el archivo; cualquier otra excepción sigue de largo.
    /// </summary>
    public EstadoPreparacion Preparar()
    {
        try
        {
            var baseComedor = BaseComedor.Abrir(RutaBase);
            SemillaDemostracion.Aplicar(baseComedor);
            SemillaAtencion.Aplicar(baseComedor);
            _base = baseComedor;
            Identificacion = new IdentificacionPorCodigo(baseComedor, Reloj);
            Catalogo = new ConsultaCatalogo(baseComedor);
            Unidad = new UnidadConfirmacion(baseComedor, Reloj);
            Trabajos = new TrabajosImpresion(baseComedor, Reloj, Impresora);
            Asistencia = new Asistencia(baseComedor, Reloj, Trabajos);
            Trabajos.EnviarPendientes();
            Estado = EstadoPreparacion.Lista;
        }
        catch (EsquemaDesconocidoException)
        {
            Estado = EstadoPreparacion.ErrorAlmacenamiento;
        }
        catch (SqliteException)
        {
            Estado = EstadoPreparacion.ErrorAlmacenamiento;
        }

        return Estado;
    }
}
