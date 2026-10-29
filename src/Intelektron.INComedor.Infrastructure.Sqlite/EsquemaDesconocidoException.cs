namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>La base tiene una versión de esquema mayor que la última migración conocida (D6).</summary>
public sealed class EsquemaDesconocidoException : Exception
{
    public EsquemaDesconocidoException(int versionEncontrada, int ultimaVersionConocida)
        : base($"La base tiene la versión de esquema {versionEncontrada} y la última conocida es {ultimaVersionConocida}.")
    {
        VersionEncontrada = versionEncontrada;
        UltimaVersionConocida = ultimaVersionConocida;
    }

    public int VersionEncontrada { get; }

    public int UltimaVersionConocida { get; }
}
