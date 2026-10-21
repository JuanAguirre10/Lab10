using Hotel.Web.Models;
using Hotel.Web.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hotel.Web.Controllers;

public class ReservasController : Controller
{
    private static readonly string[] Estados = ["Pendiente", "Confirmada", "Cancelada", "Finalizada"];

    private readonly ReservaRepositorio _reservas;

    public ReservasController(ReservaRepositorio reservas)
    {
        _reservas = reservas;
    }

    public async Task<IActionResult> Index(string? estado)
    {
        ViewBag.Estado = estado;
        ViewBag.Estados = new SelectList(Estados, estado);
        return View(await _reservas.ListarAsync(estado));
    }

    public async Task<IActionResult> Disponibles(DateOnly? ingreso, DateOnly? salida)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = ingreso ?? hoy;
        var hasta = salida ?? desde.AddDays(1);

        ViewBag.Ingreso = desde;
        ViewBag.Salida = hasta;
        ViewBag.Noches = hasta.DayNumber - desde.DayNumber;

        if (!ModelState.IsValid)
        {
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, "Ingrese fechas válidas.");
        }

        if (hasta <= desde)
            ModelState.AddModelError(string.Empty, "La fecha de salida debe ser posterior a la de ingreso.");

        if (desde < hoy)
            ModelState.AddModelError(string.Empty, "La fecha de ingreso no puede ser anterior a hoy.");

        if (!ModelState.IsValid)
            return View(new List<Habitacion>());

        return View(await _reservas.ListarDisponiblesAsync(desde, hasta));
    }

    public async Task<IActionResult> Create(int? idHabitacion, DateOnly? ingreso, DateOnly? salida)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = ingreso ?? hoy;

        var reserva = new Reserva
        {
            IdHabitacion = idHabitacion ?? 0,
            FechaIngreso = desde,
            FechaSalida = salida ?? desde.AddDays(1)
        };

        await CargarListasAsync(reserva.IdHuesped, reserva.IdHabitacion);
        return View(reserva);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("IdHuesped,IdHabitacion,FechaIngreso,FechaSalida,Observaciones")] Reserva reserva)
    {
        ModelState.Remove(nameof(Reserva.IdHabitacionNavigation));
        ModelState.Remove(nameof(Reserva.IdHuespedNavigation));
        ModelState.Remove(nameof(Reserva.Estado));
        await ValidarReglasAsync(reserva);

        if (!ModelState.IsValid)
        {
            await CargarListasAsync(reserva.IdHuesped, reserva.IdHabitacion);
            return View(reserva);
        }

        await _reservas.CrearAsync(reserva);

        var creada = await _reservas.ObtenerAsync(reserva.IdReserva);
        TempData["Mensaje"] = creada == null
            ? "Reserva registrada correctamente."
            : $"Reserva registrada para {creada.IdHuespedNavigation.Nombres} {creada.IdHuespedNavigation.Apellidos} en la habitación {creada.IdHabitacionNavigation.Numero}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Cancelar(int? id)
    {
        if (id == null) return NotFound();

        var reserva = await _reservas.ObtenerAsync(id.Value);
        if (reserva == null) return NotFound();

        if (!EsCancelable(reserva))
        {
            TempData["Error"] = reserva.Estado == "Cancelada"
                ? $"La reserva N.º {reserva.IdReserva} ya estaba cancelada."
                : $"La reserva N.º {reserva.IdReserva} está {reserva.Estado.ToLowerInvariant()} y ya no se puede cancelar.";
            return RedirectToAction(nameof(Index));
        }

        return View(reserva);
    }

    [HttpPost, ActionName("Cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarConfirmado(int id)
    {
        if (await _reservas.CancelarAsync(id))
            TempData["Mensaje"] = $"Reserva N.º {id} cancelada.";
        else
            TempData["Error"] = $"La reserva N.º {id} no existe o ya no se puede cancelar.";

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarReglasAsync(Reserva reserva)
    {
        if (reserva.IdHuesped <= 0)
            ModelState.AddModelError(nameof(Reserva.IdHuesped), "Seleccione un huésped.");
        else if (!await _reservas.ExisteHuespedActivoAsync(reserva.IdHuesped))
            ModelState.AddModelError(nameof(Reserva.IdHuesped), "El huésped seleccionado no existe o está inactivo.");

        if (reserva.IdHabitacion <= 0)
            ModelState.AddModelError(nameof(Reserva.IdHabitacion), "Seleccione una habitación.");
        else if (!await _reservas.ExisteHabitacionActivaAsync(reserva.IdHabitacion))
            ModelState.AddModelError(nameof(Reserva.IdHabitacion), "La habitación seleccionada no existe o está inactiva.");

        var fechasLeidas =
            ModelState.GetFieldValidationState(nameof(Reserva.FechaIngreso)) != ModelValidationState.Invalid &&
            ModelState.GetFieldValidationState(nameof(Reserva.FechaSalida)) != ModelValidationState.Invalid;

        if (fechasLeidas)
        {
            if (reserva.FechaSalida <= reserva.FechaIngreso)
                ModelState.AddModelError(nameof(Reserva.FechaSalida), "La fecha de salida debe ser posterior a la de ingreso.");

            if (reserva.FechaIngreso < DateOnly.FromDateTime(DateTime.Today))
                ModelState.AddModelError(nameof(Reserva.FechaIngreso), "La fecha de ingreso no puede ser anterior a hoy.");
        }

        reserva.Observaciones = string.IsNullOrWhiteSpace(reserva.Observaciones) ? null : reserva.Observaciones.Trim();
        if (reserva.Observaciones?.Length > 200)
            ModelState.AddModelError(nameof(Reserva.Observaciones), "Las observaciones no pueden superar los 200 caracteres.");

        if (ModelState.IsValid &&
            await _reservas.HabitacionOcupadaAsync(reserva.IdHabitacion, reserva.FechaIngreso, reserva.FechaSalida))
        {
            var numero = await _reservas.ObtenerNumeroHabitacionAsync(reserva.IdHabitacion);
            ModelState.AddModelError(string.Empty, $"La habitación {numero} ya está reservada en esas fechas.");
        }
    }

    private async Task CargarListasAsync(int? idHuesped = null, int? idHabitacion = null)
    {
        var huespedes = new List<SelectListItem>();
        foreach (var h in await _reservas.ObtenerHuespedesParaListaAsync())
        {
            huespedes.Add(new SelectListItem(
                $"{h.Apellidos}, {h.Nombres} ({h.Dni})",
                h.IdHuesped.ToString(),
                h.IdHuesped == idHuesped));
        }
        ViewBag.Huespedes = huespedes;

        var habitaciones = new List<SelectListItem>();
        foreach (var h in await _reservas.ObtenerHabitacionesParaListaAsync())
        {
            habitaciones.Add(new SelectListItem(
                $"{h.Numero} · {h.IdTipoHabitacionNavigation.Nombre} · {h.IdTipoHabitacionNavigation.PrecioNoche:C}",
                h.IdHabitacion.ToString(),
                h.IdHabitacion == idHabitacion));
        }
        ViewBag.Habitaciones = habitaciones;
    }

    private static bool EsCancelable(Reserva reserva)
    {
        return reserva.Estado == "Pendiente" || reserva.Estado == "Confirmada";
    }
}
