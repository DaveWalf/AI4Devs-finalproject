using System.IO;
using System.Text;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

public enum ModoImpresoraSimulada
{
    Disponible,
    SinPapel,
    Incierta,
}

/// <summary>Un cambio de modo elegido en Configuración, para su registro (D9 de escribir-bitacoras-externas).</summary>
public sealed record CambioModoImpresora(ModoImpresoraSimulada Antes, ModoImpresoraSimulada Despues);

/// <summary>
/// La impresora de la demostración (D11 de atender-seleccion-y-confirmacion): escribe cada comprobante como texto en
/// la carpeta comprobantes, con el nombre «TicketId-copia.txt». El modo se elige en la franja de la pantalla.
/// </summary>
public sealed class ImpresoraSimulada : IImpresoraComprobantes
{
    public ImpresoraSimulada(string carpeta)
    {
        Carpeta = carpeta;
    }

    public string Carpeta { get; }

    public ModoImpresoraSimulada Modo { get; set; } = ModoImpresoraSimulada.Disponible;

    /// <summary>El encabezado y el pie que lleva cada comprobante desde el próximo que sale (D13 de configurar-puesto-por-pestanas).</summary>
    public TextosComprobante Textos { get; set; } = TextosComprobante.PorDefecto;

    /// <summary>El último comprobante que aceptó como enviado, para mostrarlo en pantalla (D7 de presentar-atencion-tactil).</summary>
    public ComprobanteAImprimir? UltimoEnviado { get; private set; }

    public RespuestaImpresora Enviar(ComprobanteAImprimir comprobante)
    {
        if (Modo == ModoImpresoraSimulada.SinPapel)
        {
            return RespuestaImpresora.Fallido("PAPER_OUT");
        }

        try
        {
            Directory.CreateDirectory(Carpeta);
            var texto = new StringBuilder()
                .AppendLine(Textos.Encabezado + " (demostración, datos sintéticos)")
                .AppendLine(comprobante.Leyenda ?? "ORIGINAL")
                .AppendLine("Comprobante: " + comprobante.TicketId)
                .AppendLine("Copia: " + comprobante.NumeroCopia)
                .AppendLine(comprobante.DatosImpresion);
            if (Textos.Pie.Length > 0)
            {
                texto.AppendLine(Textos.Pie);
            }

            File.WriteAllText(Path.Combine(Carpeta, $"{comprobante.TicketId}-{comprobante.NumeroCopia}.txt"), texto.ToString(), Encoding.UTF8);
        }
        catch (IOException)
        {
            // Sin carpeta donde escribir, la impresora simulada no puede probar que el papel salió.
            return RespuestaImpresora.Incierto();
        }

        // En «Incierta» el archivo se escribe pero la impresora no lo confirma: el papel pudo haber salido (RB08).
        if (Modo == ModoImpresoraSimulada.Incierta)
        {
            return RespuestaImpresora.Incierto();
        }

        UltimoEnviado = comprobante;
        return RespuestaImpresora.Enviado();
    }
}
