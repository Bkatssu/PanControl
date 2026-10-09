/* ===========================================================================
   PanControl - Capa de comunicacion con la API
   ---------------------------------------------------------------------------
   Aqui estan todas las funciones que hablan con el backend en C#.
   Las pantallas solo llaman a estas funciones y no se preocupan por los
   detalles de las peticiones HTTP.
   =========================================================================== */

// La API vive en el mismo servidor que estas paginas, por eso la ruta es vacia.
const API = "";

/* ---------------------------------------------------------------------------
   Funcion base: hace la peticion y maneja los errores de forma uniforme.
   --------------------------------------------------------------------------- */
async function peticion(ruta, opciones = {}) {
    const respuesta = await fetch(API + ruta, {
        headers: { "Content-Type": "application/json" },
        ...opciones
    });

    // 204 = sin contenido
    let datos = null;
    const texto = await respuesta.text();
    if (texto) {
        try { datos = JSON.parse(texto); } catch { datos = texto; }
    }

    if (!respuesta.ok) {
        const mensaje = (datos && (datos.error || datos.title)) || `Error ${respuesta.status}`;
        throw new Error(mensaje);
    }

    return datos;
}

const api = {
    // ---------------------------------------------------------- Usuarios
    login: (nombreUsuario, contrasena) =>
        peticion("/api/usuarios/login", {
            method: "POST",
            body: JSON.stringify({ nombreUsuario, contrasena })
        }),

    usuarios: () => peticion("/api/usuarios"),

    // ---------------------------------------------------------- Productos
    productos: () => peticion("/api/productos"),
    receta: (id) => peticion(`/api/productos/${id}/receta`),

    // ---------------------------------------------------------- Materias primas
    materiasPrimas: () => peticion("/api/materiasprimas"),

    entradaMateriaPrima: (id, cantidad, idUsuario) =>
        peticion(`/api/materiasprimas/${id}/entrada?cantidad=${cantidad}&idUsuario=${idUsuario}`, {
            method: "POST"
        }),

    // ---------------------------------------------------------- Ventas
    registrarVenta: (venta) =>
        peticion("/api/ventas", { method: "POST", body: JSON.stringify(venta) }),

    ventas: () => peticion("/api/ventas"),
    venta: (id) => peticion(`/api/ventas/${id}`),

    anularVenta: (id, idUsuario) =>
        peticion(`/api/ventas/${id}/anular?idUsuario=${idUsuario}`, { method: "POST" }),

    // ---------------------------------------------------------- Produccion
    registrarProduccion: (datos) =>
        peticion("/api/produccion", { method: "POST", body: JSON.stringify(datos) }),

    // ---------------------------------------------------------- Reportes
    ventasDelDia: () => peticion("/api/reportes/ventas-dia"),
    masVendidos: (top = 5) => peticion(`/api/reportes/productos-mas-vendidos?top=${top}`),
    existencias: () => peticion("/api/reportes/existencias"),
    bajoInventario: () => peticion("/api/reportes/bajo-inventario")
};

/* ===========================================================================
   Sesion del usuario
   ---------------------------------------------------------------------------
   Se guarda en sessionStorage: dura mientras la pestana este abierta.
   =========================================================================== */
const sesion = {
    guardar(usuario) {
        sessionStorage.setItem("pancontrol_usuario", JSON.stringify(usuario));
    },

    obtener() {
        const dato = sessionStorage.getItem("pancontrol_usuario");
        return dato ? JSON.parse(dato) : null;
    },

    cerrar() {
        sessionStorage.removeItem("pancontrol_usuario");
        window.location.href = "index.html";
    },

    // Se llama al inicio de cada pantalla: si no hay sesion, manda al login.
    // Si se indican roles permitidos y el usuario no los tiene, lo devuelve
    // a la pantalla que le corresponde.
    exigir(rolesPermitidos = null) {
        const usuario = this.obtener();

        if (!usuario) {
            window.location.href = "index.html";
            return null;
        }

        if (rolesPermitidos && !rolesPermitidos.includes(usuario.rol)) {
            alert("No tienes permiso para entrar a esta pantalla.");
            window.location.href = pantallaInicial(usuario.rol);
            return null;
        }

        return usuario;
    }
};

// A que pantalla entra cada rol despues de iniciar sesion
function pantallaInicial(rol) {
    if (rol === "Cajero") return "ventas.html";
    if (rol === "Produccion") return "produccion.html";
    return "admin.html";   // Administrador
}

/* ===========================================================================
   Utilidades compartidas
   =========================================================================== */

// Formatea un numero como quetzales: 16 -> "Q16.00"
function moneda(valor) {
    return "Q" + Number(valor || 0).toFixed(2);
}

// Formatea una fecha ISO como "07/10/2026 22:51"
function fechaHora(iso) {
    if (!iso) return "";
    const f = new Date(iso);
    const dd = String(f.getDate()).padStart(2, "0");
    const mm = String(f.getMonth() + 1).padStart(2, "0");
    const hh = String(f.getHours()).padStart(2, "0");
    const mi = String(f.getMinutes()).padStart(2, "0");
    return `${dd}/${mm}/${f.getFullYear()} ${hh}:${mi}`;
}

// Evita que texto del usuario rompa el HTML
function limpiar(texto) {
    const d = document.createElement("div");
    d.textContent = texto ?? "";
    return d.innerHTML;
}

// Muestra un mensaje en pantalla durante unos segundos
function avisar(idContenedor, texto, tipo = "info", segundos = 5) {
    const caja = document.getElementById(idContenedor);
    if (!caja) return;

    caja.className = "mensaje " + tipo;
    caja.textContent = texto;
    caja.classList.remove("oculto");

    if (segundos > 0) {
        clearTimeout(caja._temporizador);
        caja._temporizador = setTimeout(() => caja.classList.add("oculto"), segundos * 1000);
    }
}

/* ===========================================================================
   Barra de navegacion comun a todas las pantallas
   =========================================================================== */
function dibujarBarra(paginaActual) {
    const usuario = sesion.obtener();
    if (!usuario) return;

    // Que opciones ve cada rol
    const opciones = {
        "Administrador": [
            ["admin.html", "Panel"],
            ["ventas.html", "Ventas"],
            ["inventario.html", "Inventario"],
            ["produccion.html", "Produccion"]
        ],
        "Cajero": [
            ["ventas.html", "Ventas"],
            ["inventario.html", "Inventario"]
        ],
        "Produccion": [
            ["produccion.html", "Produccion"],
            ["inventario.html", "Inventario"]
        ]
    };

    const enlaces = (opciones[usuario.rol] || [])
        .map(([destino, texto]) => {
            const activo = destino === paginaActual ? ' class="activo"' : "";
            return `<a href="${destino}"${activo}>${texto}</a>`;
        })
        .join("");

    document.getElementById("barra").innerHTML = `
        <div class="marca">Pan<span>Control</span></div>
        <nav>${enlaces}</nav>
        <div class="usuario">
            <span>${limpiar(usuario.nombreCompleto)}</span>
            <span class="rol">${limpiar(usuario.rol)}</span>
            <button class="secundario chico" onclick="sesion.cerrar()">Salir</button>
        </div>`;
}
