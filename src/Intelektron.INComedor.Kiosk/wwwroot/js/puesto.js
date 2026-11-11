// Lo único que el puesto hace fuera de Blazor: para el teclado en pantalla de Personas (D4 de editar-raciones-en-personas),
// enfocar un campo y ponerle el valor como propiedad del DOM, sin atributo ni árbol de render, al código de una persona
// nueva, que no se dibuja ni se vuelve a mostrar (D10 de administrar-personas-y-opciones). Los campos se buscan por su
// data-campo, sin referencias de Blazor: una referencia agrega a la página un identificador al azar. Más abajo, el teclado
// general de operar-solo-con-la-pantalla.
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
        window.incomedor._tecladoAbierto = abrir;
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('Intelektron.INComedor.Kiosk', 'CampoTocado', abrir).catch(function () { });
        }
    };
    // Un campo tocado lleva inputmode="none" hasta perder el foco, así el teclado táctil de Windows no se abre encima del
    // general; el teclado físico escribe igual (D2).
    var silenciar = function (campo) {
        if (!campo.hasAttribute('data-modo-previo')) {
            campo.setAttribute('data-modo-previo', campo.getAttribute('inputmode') || '');
            campo.setAttribute('inputmode', 'none');
        }
    };
    var restituir = function (campo) {
        if (campo && campo.hasAttribute && campo.hasAttribute('data-modo-previo')) {
            var previo = campo.getAttribute('data-modo-previo');
            campo.removeAttribute('data-modo-previo');
            if (previo) {
                campo.setAttribute('inputmode', previo);
            } else {
                campo.removeAttribute('inputmode');
            }
        }
    };
    // El último puntero manda: un clic de mouse después de un toque no abre el teclado. Con el teclado abierto y el foco
    // fuera de los campos (el campo salió de la pantalla sin avisar), un toque fuera del teclado lo cierra.
    document.addEventListener('pointerdown', function (evento) {
        var toque = evento.pointerType === 'touch' || evento.pointerType === 'pen';
        window.incomedor._ultimoToque = toque ? Date.now() : 0;
        if (toque && window.incomedor.esCampoDeTexto(evento.target)) {
            silenciar(evento.target);
        }

        if (window.incomedor._tecladoAbierto && !window.incomedor.esCampoDeTexto(document.activeElement)
            && !(evento.target.closest && evento.target.closest('.teclado-general'))) {
            avisarANet(false);
        }
    }, true);
    // El campo queda a la vista dos veces: la segunda, cuando la pantalla ya se acortó para el teclado (D5).
    var desplazar = function (campo) {
        [150, 400].forEach(function (espera) {
            setTimeout(function () {
                if (document.activeElement === campo) {
                    campo.scrollIntoView({ block: 'nearest' });
                }
            }, espera);
        });
    };
    var avisar = function () {
        var campo = document.activeElement;
        var tocado = Date.now() - window.incomedor._ultimoToque < 1500;
        var abrir = window.incomedor.esCampoDeTexto(campo) && (tocado || window.incomedor._tecladoAbierto);
        // Un foco por un toque avisa siempre: la pantalla pudo haber cerrado el teclado al salir del reposo (D2).
        if (abrir !== window.incomedor._tecladoAbierto || (abrir && tocado)) {
            avisarANet(abrir);
        }

        if (abrir) {
            silenciar(campo);
            desplazar(campo);
        }
    };
    document.addEventListener('focusin', avisar);
    document.addEventListener('focusout', function (evento) {
        var campo = evento.target;
        restituir(campo);
        // Lo escrito con las teclas llega también como change al salir del campo, como con un teclado físico: los campos
        // que Blazor lee con onchange, como los de Opciones, reciben el valor antes del toque en «Guardar» (D3).
        if (campo && campo.hasAttribute && campo.hasAttribute('data-tecleado')) {
            campo.removeAttribute('data-tecleado');
            campo.dispatchEvent(new Event('change', { bubbles: true }));
        }

        setTimeout(avisar, 0);
    });
})();
window.incomedor.teclear = function (tecla) {
    var campo = document.activeElement;
    if (!window.incomedor.esCampoDeTexto(campo)) {
        return;
    }

    var texto = tecla === 'ESPACIO' ? ' ' : tecla;
    // Un campo numérico recibe sólo dígitos: otra tecla lo vaciaría al sanear su valor, y un teclado físico no la deja entrar.
    if (tecla !== 'BORRAR' && (campo.getAttribute('type') || '').toLowerCase() === 'number' && !/^[0-9]$/.test(texto)) {
        return;
    }

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

    campo.setAttribute('data-tecleado', '1');
    campo.dispatchEvent(new Event('input', { bubbles: true }));
};
