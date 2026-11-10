namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Una fila de <see cref="ListaDesplegable"/>: el valor que se aplica al elegirla y el texto que se ve. El valor nunca es un
/// código de identificación (D3 de ordenar-configuracion): en el combo es la credencial, y en los filtros, la posición.
/// </summary>
public sealed record EntradaLista(string Valor, string Texto);
