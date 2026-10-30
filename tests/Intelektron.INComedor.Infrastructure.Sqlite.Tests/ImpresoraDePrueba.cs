namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public enum ModoImpresoraDePrueba
{
    Disponible,
    SinPapel,
    Incierta,
    Caida,
}

/// <summary>
/// El único doble de estas pruebas (D12): guarda lo que recibe y responde según su modo. «Caida» lanza una
/// excepción para simular que el proceso cae mientras la impresora todavía no respondió.
/// </summary>
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
            ModoImpresoraDePrueba.Incierta => RespuestaImpresora.Incierto(),
            _ => throw new CaidaSimuladaException(),
        };
    }
}

public sealed class CaidaSimuladaException : Exception
{
    public CaidaSimuladaException() : base("Caída simulada durante el envío a la impresora.")
    {
    }
}
