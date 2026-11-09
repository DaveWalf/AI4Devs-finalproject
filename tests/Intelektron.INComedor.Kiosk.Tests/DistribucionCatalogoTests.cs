using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La distribución por cantidad en la máquina, sin la ventana (D1 y D2 de presentar-atencion-tactil): cuántas páginas y
/// cuántas columnas lleva cada una, y el cargo de una opción sin importe. SQLite real con la semilla de nueve o de quince.
/// </summary>
public class DistribucionCatalogoTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose() => _archivo.Dispose();

    [Theory(DisplayName = "atencion-puesto · Hasta diez opciones en una página: ocho en una columna, nueve y diez en dos, sin controles de página")]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(10, 2)]
    public void Hasta_diez_opciones_en_una_pagina(int cantidad, int columnas)
    {
        var maquina = Seleccion(cantidad);

        Assert.Equal(cantidad, maquina.PaginaActual.Count);
        Assert.Equal(1, maquina.TotalPaginas);
        Assert.Equal(columnas, maquina.ColumnasPagina);
        Assert.False(maquina.PuedeAvanzar);
        Assert.False(maquina.PuedeRetroceder);
    }

    [Fact(DisplayName = "AT16 · Quince opciones: una página de diez en dos columnas y otra de cinco en una columna, cada opción una sola vez")]
    public void Quince_opciones_en_dos_paginas()
    {
        var maquina = Seleccion(15);

        Assert.Equal(10, MaquinaAtencion.TamanoPagina);
        Assert.Equal(2, maquina.TotalPaginas);
        var primera = maquina.PaginaActual.Select(o => o.Id).ToList();
        Assert.Equal(10, primera.Count);
        Assert.Equal(2, maquina.ColumnasPagina);

        maquina.Siguiente();

        Assert.Equal(5, maquina.PaginaActual.Count);
        Assert.Equal(1, maquina.ColumnasPagina);
        Assert.Equal(15, primera.Concat(maquina.PaginaActual.Select(o => o.Id)).Distinct().Count());
    }

    [Fact(DisplayName = "atencion-puesto · Opción sin cargo: con importe cero el cargo es vacío; con importe, «ARS 1.500,00» como siempre")]
    public void Cargo_cero_es_vacio()
    {
        Assert.Equal(string.Empty, MensajesAtencion.Cargo("ARS", 2, 0));
        Assert.Equal("ARS 1.500,00", MensajesAtencion.Cargo("ARS", 2, 150000));
    }

    [Fact(DisplayName = "atencion-puesto · Con el catálogo de nueve, las tres opciones sin cargo llegan a la selección con el cargo vacío")]
    public void Las_tres_sin_cargo_llegan_vacias()
    {
        var maquina = Seleccion(9);

        Assert.Equal(["Merienda", "Refrigerio", "Yogurt"], maquina.PaginaActual.Where(o => o.Cargo.Length == 0).Select(o => o.Nombre).Order().ToList());
        Assert.All(maquina.PaginaActual.Where(o => o.Cargo.Length > 0), o => Assert.StartsWith("ARS ", o.Cargo));
    }

    /// <summary>Una persona en la selección con un catálogo de 8, 9 o 10 opciones (sobre la semilla de nueve) o de 15.</summary>
    private MaquinaAtencion Seleccion(int cantidad)
    {
        var maquina = new MaquinaAtencion(new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: cantidad != 15));
        maquina.Iniciar();
        if (cantidad == 8)
        {
            CatalogoDePrueba.Quitar(_archivo.Ruta, CatalogoDePrueba.Yogurt);
        }
        else if (cantidad == 10)
        {
            CatalogoDePrueba.Agregar(_archivo.Ruta);
        }

        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar("0001"));
        return maquina;
    }
}
