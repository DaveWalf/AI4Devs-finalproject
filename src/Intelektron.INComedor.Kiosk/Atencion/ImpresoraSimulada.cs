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

    /// <summary>El número y el nombre del puesto que lleva cada comprobante desde el próximo (D3 de enriquecer-comprobante).</summary>
    public DatosPuesto Puesto { get; set; } = DatosPuesto.PorDefecto;

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
            // El puesto debajo del encabezado; el número y el comensal de los datos guardados, si los tiene; el operador de
            // una copia; y al pie la frase del comprobante, antes del pie configurable (D6 de enriquecer-comprobante).
            var texto = new StringBuilder().AppendLine(Textos.Encabezado + " (demostración, datos sintéticos)");
            if (Puesto.Linea is { } puesto)
            {
                texto.AppendLine(puesto);
            }

            texto.AppendLine(comprobante.Leyenda ?? "ORIGINAL")
                .AppendLine("Comprobante: " + comprobante.TicketId);
            if (ComprobanteEnPantalla.LeerDatos(comprobante) is { } datos)
            {
                texto.AppendLine("Número: " + datos.Numero);
                if (datos.Comensal is { } comensal)
                {
                    texto.AppendLine("Comensal: " + comensal);
                }
            }

            texto.AppendLine("Copia: " + comprobante.NumeroCopia);
            // Una copia lleva quién la habilitó o, si se hizo con la clave del día, la marca en su lugar (D2 de
            // reimprimir-sin-codigo); nunca las dos.
            if (comprobante.SinValidez)
            {
                texto.AppendLine(ComprobanteEnPantalla.MarcaSinValidez);
            }
            else if (comprobante.Habilito is { } habilito)
            {
                texto.AppendLine("Habilitó: " + habilito);
            }

            texto.AppendLine(comprobante.DatosImpresion)
                .AppendLine(FrasesComprobante.Para(comprobante.TicketId));
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
