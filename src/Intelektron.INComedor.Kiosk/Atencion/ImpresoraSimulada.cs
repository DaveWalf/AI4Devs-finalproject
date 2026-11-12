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
                .AppendLine("INComedor · comprobante (demostración, datos sintéticos)")
                .AppendLine(comprobante.Leyenda ?? "ORIGINAL")
                .AppendLine("Comprobante: " + comprobante.TicketId)
                .AppendLine("Copia: " + comprobante.NumeroCopia)
                .AppendLine(comprobante.DatosImpresion)
                .ToString();
            File.WriteAllText(Path.Combine(Carpeta, $"{comprobante.TicketId}-{comprobante.NumeroCopia}.txt"), texto, Encoding.UTF8);
        }
        catch (IOException)
        {
            // Sin carpeta donde escribir, la impresora simulada no puede probar que el papel salió.
            return RespuestaImpresora.Incierto();
        }

        // En «Incierta» el archivo se escribe pero la impresora no lo confirma: el papel pudo haber salido (RB08).
        return Modo == ModoImpresoraSimulada.Incierta ? RespuestaImpresora.Incierto() : RespuestaImpresora.Enviado();
    }
}
