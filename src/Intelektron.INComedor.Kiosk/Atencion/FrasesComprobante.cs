namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Las 50 frases cálidas del pie del comprobante (D4 de enriquecer-comprobante): fijas, sin datos de nadie, de hasta 60
/// caracteres. La de un comprobante sale de su identificador, así el original, sus copias y la pantalla dicen la misma.
/// </summary>
public static class FrasesComprobante
{
    public const int LargoMaximo = 60;

    public static IReadOnlyList<string> Todas { get; } =
    [
        "¡Que lo disfrutes!",
        "Buen provecho, y que tengas un gran día.",
        "Comer rico también es cuidarse.",
        "Una pausa a tiempo hace mejor todo el día.",
        "Gracias por compartir la mesa con nosotros.",
        "Que esta comida te dé energía para lo que sigue.",
        "Hoy es un buen día para disfrutar sin apuro.",
        "Lo hicimos pensando en vos.",
        "Que la tarde te encuentre con buen ánimo.",
        "Comer en compañía siempre sabe mejor.",
        "Un plato caliente, una pausa merecida.",
        "Recargá energías: te lo ganaste.",
        "Que cada bocado te devuelva una sonrisa.",
        "Tomate tu tiempo: la pausa también es trabajo.",
        "Gracias por elegir comer bien hoy.",
        "Que tengas una jornada liviana y linda.",
        "¡Buen provecho y hasta la próxima!",
        "La mejor sobremesa es la que se comparte.",
        "Comé tranquilo: lo importante puede esperar un rato.",
        "Que tu día siga tan bien como este plato.",
        "Una sonrisa también se sirve caliente.",
        "Disfrutá: este momento es tuyo.",
        "Gracias por tu buena energía de siempre.",
        "Que el resto del día sea tan rico como este.",
        "Pausa, respiro y a disfrutar.",
        "Cada comida es una buena excusa para charlar.",
        "Hoy también te merecés algo rico.",
        "Que esta pausa te deje con ganas de seguir.",
        "Nada como un buen plato para empezar de nuevo.",
        "Que tengas un muy buen provecho.",
        "Gracias por pasar por el comedor.",
        "El que come bien, trabaja mejor.",
        "Un ratito para vos, un plato para disfrutar.",
        "Que el día te sonría tanto como esta comida.",
        "Buena comida, buena compañía, buen día.",
        "¡A disfrutar, que la pausa es corta!",
        "Que esta comida te acompañe toda la tarde.",
        "Gracias por cuidar este espacio entre todos.",
        "Comer juntos nos hace un mejor equipo.",
        "Que el sabor de hoy te alegre el día.",
        "Una buena comida arregla muchas cosas.",
        "Hoy la cocina se puso linda para vos.",
        "Que esta pausa sea tan buena como merecés.",
        "Desconectá un rato: el plato te espera.",
        "Gracias por la paciencia en la fila.",
        "Lo rico se disfruta mejor sin apuro.",
        "Que la energía de este plato te dure todo el día.",
        "Una comida casera para un día de trabajo.",
        "Gracias por ser parte de este comedor.",
        "Nos vemos en la próxima comida.",
    ];

    /// <summary>La frase de un comprobante: sus primeros cuatro bytes, módulo 50; un GUID v4 los tiene al azar.</summary>
    public static string Para(Guid comprobante) =>
        Todas[(int)(BitConverter.ToUInt32(comprobante.ToByteArray(), 0) % (uint)Todas.Count)];
}
