using Hotel.Web.Models;
using Hotel.Web.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hotel.Web.Controllers;

public class HabitacionesController : Controller
{
    private readonly HabitacionRepositorio _habitaciones;

    public HabitacionesController(HabitacionRepositorio habitaciones)
    {
        _habitaciones = habitaciones;
    }

    public async Task<IActionResult> Index(int? idTipo)
    {
        var tipos = await _habitaciones.ObtenerTiposParaListaAsync();
        ViewBag.Tipos = new SelectList(tipos, nameof(TipoHabitacion.IdTipoHabitacion), nameof(TipoHabitacion.Nombre), idTipo);
        return View(await _habitaciones.ListarAsync(idTipo));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var habitacion = await _habitaciones.ObtenerConReservasAsync(id.Value);
        if (habitacion == null) return NotFound();

        return View(habitacion);
    }

    public async Task<IActionResult> Create()
    {
        await CargarTiposAsync();
        return View(new Habitacion { Piso = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Numero,Piso,IdTipoHabitacion")] Habitacion habitacion)
    {
        ModelState.Remove(nameof(Habitacion.IdTipoHabitacionNavigation));
        await ValidarReglasAsync(habitacion);

        if (!ModelState.IsValid)
        {
            await CargarTiposAsync(habitacion.IdTipoHabitacion);
            return View(habitacion);
        }

        await _habitaciones.CrearAsync(habitacion);
        TempData["Mensaje"] = $"Habitación {habitacion.Numero} registrada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var habitacion = await _habitaciones.ObtenerAsync(id.Value);
        if (habitacion == null) return NotFound();

        await CargarTiposAsync(habitacion.IdTipoHabitacion);
        return View(habitacion);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("IdHabitacion,Numero,Piso,IdTipoHabitacion")] Habitacion habitacion)
    {
        if (id != habitacion.IdHabitacion) return NotFound();

        ModelState.Remove(nameof(Habitacion.IdTipoHabitacionNavigation));
        await ValidarReglasAsync(habitacion);

        if (!ModelState.IsValid)
        {
            await CargarTiposAsync(habitacion.IdTipoHabitacion);
            return View(habitacion);
        }

        if (!await _habitaciones.ActualizarAsync(habitacion))
            return NotFound();

        TempData["Mensaje"] = $"Habitación {habitacion.Numero} actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var habitacion = await _habitaciones.ObtenerAsync(id.Value);
        if (habitacion == null) return NotFound();

        ViewBag.ReservasVigentes = await _habitaciones.ContarReservasVigentesAsync(id.Value);
        return View(habitacion);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var habitacion = await _habitaciones.ObtenerAsync(id);
        if (habitacion == null) return NotFound();

        var cantidad = await _habitaciones.ContarReservasVigentesAsync(id);
        if (cantidad > 0)
        {
            ViewBag.ReservasVigentes = cantidad;
            ModelState.AddModelError(string.Empty,
                $"No se puede dar de baja: la habitación tiene {cantidad} reserva(s) pendiente(s) o confirmada(s).");
            return View(habitacion);
        }

        await _habitaciones.DesactivarAsync(id);
        TempData["Mensaje"] = $"Habitación {habitacion.Numero} eliminada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidarReglasAsync(Habitacion habitacion)
    {
        if (string.IsNullOrWhiteSpace(habitacion.Numero))
        {
            ModelState.Remove(nameof(Habitacion.Numero));
            ModelState.AddModelError(nameof(Habitacion.Numero), "El número es obligatorio.");
        }
        else
        {
            habitacion.Numero = habitacion.Numero.Trim();

            if (habitacion.Numero.Length > 10)
                ModelState.AddModelError(nameof(Habitacion.Numero), "El número no puede superar los 10 caracteres.");
            else if (await _habitaciones.ExisteNumeroAsync(habitacion.Numero, habitacion.IdHabitacion))
                ModelState.AddModelError(nameof(Habitacion.Numero), $"Ya existe una habitación con el número {habitacion.Numero}.");
        }

        if (habitacion.Piso < 1 || habitacion.Piso > 50)
            ModelState.AddModelError(nameof(Habitacion.Piso), "El piso debe estar entre 1 y 50.");

        if (habitacion.IdTipoHabitacion <= 0)
            ModelState.AddModelError(nameof(Habitacion.IdTipoHabitacion), "Seleccione un tipo de habitación.");
        else if (!await _habitaciones.ExisteTipoActivoAsync(habitacion.IdTipoHabitacion))
            ModelState.AddModelError(nameof(Habitacion.IdTipoHabitacion), "El tipo seleccionado no existe o está inactivo.");
    }

    private async Task CargarTiposAsync(int? seleccionado = null)
    {
        var tipos = await _habitaciones.ObtenerTiposParaListaAsync();
        var opciones = new List<SelectListItem>();
        foreach (var tipo in tipos)
        {
            opciones.Add(new SelectListItem(
                $"{tipo.Nombre} · {tipo.PrecioNoche:C}",
                tipo.IdTipoHabitacion.ToString(),
                tipo.IdTipoHabitacion == seleccionado));
        }
        ViewBag.Tipos = opciones;
    }
}
