// Lo único que el puesto hace fuera de Blazor: poner el valor de un campo como propiedad del DOM, sin atributo ni árbol de
// render. Lo usa el teclado en pantalla para el código de una persona nueva, que no se dibuja ni se vuelve a mostrar
// (D4 de editar-raciones-en-personas y D10 de administrar-personas-y-opciones).
window.incomedor = window.incomedor || {};
window.incomedor.ponerValor = function (campo, valor) {
    if (campo) {
        campo.value = valor;
    }
};
