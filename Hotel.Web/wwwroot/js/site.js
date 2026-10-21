document.querySelectorAll("form[data-rango-fechas]").forEach(function (form) {
    var ingreso = form.querySelector("[name='ingreso']");
    var salida = form.querySelector("[name='salida']");
    if (!ingreso || !salida) {
        return;
    }
    ingreso.addEventListener("change", function () {
        if (!ingreso.value) {
            return;
        }
        var fecha = new Date(ingreso.value + "T00:00:00");
        fecha.setDate(fecha.getDate() + 1);
        var minimo = fecha.getFullYear() + "-" +
            String(fecha.getMonth() + 1).padStart(2, "0") + "-" +
            String(fecha.getDate()).padStart(2, "0");
        salida.min = minimo;
        if (!salida.value || salida.value < minimo) {
            salida.value = minimo;
        }
    });
});
