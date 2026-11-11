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

// El teclado general (D2 y D3 de operar-solo-con-la-pantalla): avisa a la pantalla cuando un campo de texto recibió el
// foco por un toque de la pantalla táctil (con un mouse o un teclado físico no hace falta) y escribe en el campo con el
// foco las teclas que se tocan, como si se hubieran tipeado: el evento input le da el valor a Blazor.
window.incomedor.esCampoDeTexto = function (elemento) {
    if (!elemento || elemento.tagName !== 'INPUT' || elemento.readOnly || elemento.disabled) {
        return false;
    }

    var tipo = (elemento.getAttribute('type') || 'text').toLowerCase();
    return ['text', 'email', 'search', 'number', 'tel', 'url'].indexOf(tipo) >= 0 && !elemento.closest('[data-teclado-propio]');
};
// Escucha el foco desde que carga la página y avisa a la pantalla por un método estático de .NET, así la pantalla no
// llama a JS al dibujarse.
(function () {
    window.incomedor._ultimoToque = 0;
    window.incomedor._tecladoAbierto = false;
    var avisarANet = function (abrir) {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('Intelektron.INComedor.Kiosk', 'CampoTocado', abrir).catch(function () { });
        }
    };
    // El último puntero manda: un clic de mouse después de un toque no abre el teclado.
    document.addEventListener('pointerdown', function (evento) {
        window.incomedor._ultimoToque = evento.pointerType === 'touch' || evento.pointerType === 'pen' ? Date.now() : 0;
    }, true);
    var avisar = function () {
        var campo = document.activeElement;
        var tocado = Date.now() - window.incomedor._ultimoToque < 1500;
        var abrir = window.incomedor.esCampoDeTexto(campo) && (tocado || window.incomedor._tecladoAbierto);
        if (abrir !== window.incomedor._tecladoAbierto) {
            window.incomedor._tecladoAbierto = abrir;
            avisarANet(abrir);
        }

        if (abrir) {
            setTimeout(function () { campo.scrollIntoView({ block: 'nearest' }); }, 150);
        }
    };
    document.addEventListener('focusin', avisar);
    document.addEventListener('focusout', function () { setTimeout(avisar, 0); });
})();
window.incomedor.teclear = function (tecla) {
    var campo = document.activeElement;
    if (!window.incomedor.esCampoDeTexto(campo)) {
        return;
    }

    var texto = tecla === 'ESPACIO' ? ' ' : tecla;
    var inicio, fin, conSeleccion = true;
    try {
        inicio = campo.selectionStart;
        fin = campo.selectionEnd;
        conSeleccion = inicio !== null;
    } catch (e) {
        conSeleccion = false;
    }

    if (!conSeleccion) {
        inicio = fin = campo.value.length;
    }

    if (tecla === 'BORRAR') {
        if (inicio === fin) {
            if (inicio === 0) {
                return;
            }

            inicio = inicio - 1;
        }

        campo.value = campo.value.slice(0, inicio) + campo.value.slice(fin);
    } else {
        if (campo.maxLength > 0 && campo.value.length - (fin - inicio) + texto.length > campo.maxLength) {
            return;
        }

        campo.value = campo.value.slice(0, inicio) + texto + campo.value.slice(fin);
        inicio = inicio + texto.length;
    }

    if (conSeleccion) {
        try {
            campo.setSelectionRange(inicio, inicio);
        } catch (e) {
            // Un tipo sin selección (el de correo): el cursor queda al final.
        }
    }

    campo.dispatchEvent(new Event('input', { bubbles: true }));
};
