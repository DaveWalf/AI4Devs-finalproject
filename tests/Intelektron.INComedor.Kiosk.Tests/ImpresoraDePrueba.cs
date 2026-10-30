using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

public enum ModoImpresoraDePrueba
{
    Disponible,
    SinPapel,
    Incierta,
}

/// <summary>El único doble de las pruebas del Kiosk (D12): guarda lo que recibe y responde según su modo.</summary>
public sealed class ImpresoraDePrueba : IImpresoraComprobantes
{
    public ModoImpresoraDePrueba Modo { get; set; } = ModoImpresoraDePrueba.Disponible;

    public List<ComprobanteAImprimir> Recibidos { get; } = [];

    public RespuestaImpresora Enviar(ComprobanteAImprimir comprobante)
    {
        Recibidos.Add(comprobante);
        return Modo switch
        {
            ModoImpresoraDePrueba.Disponible => RespuestaImpresora.Enviado(),
            ModoImpresoraDePrueba.SinPapel => RespuestaImpresora.Fallido("PAPER_OUT"),
            _ => RespuestaImpresora.Incierto(),
        };
    }
}
