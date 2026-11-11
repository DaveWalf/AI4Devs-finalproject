// Lo único que el puesto hace fuera de Blazor, para el teclado en pantalla de Personas (D4 de editar-raciones-en-personas):
// enfocar un campo y ponerle el valor como propiedad del DOM, sin atributo ni árbol de render, al código de una persona
// nueva, que no se dibuja ni se vuelve a mostrar (D10 de administrar-personas-y-opciones). Los campos se buscan por su
// data-campo, sin referencias de Blazor: una referencia agrega a la página un identificador al azar.
window.incomedor = window.incomedor || {};
window.incomedor.enfocar = function (selector) {
    var campo = document.querySelector(selector);
    if (campo) {
        campo.focus();
    }
};
window.incomedor.ponerValor = function (selector, valor) {
    var campo = document.querySelector(selector);
    if (campo) {
        campo.value = valor;
    }
};
