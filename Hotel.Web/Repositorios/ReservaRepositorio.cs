using Hotel.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Web.Repositorios;

public class ReservaRepositorio
{
    private readonly HotelContext _context;

    public ReservaRepositorio(HotelContext context)
    {
        _context = context;
    }

    public async Task<List<Habitacion>> ListarDisponiblesAsync(DateOnly ingreso, DateOnly salida)
    {
        return await _context.Habitacions
            .AsNoTracking()
            .Include(h => h.IdTipoHabitacionNavigation)
            .Where(h => h.Activo && !h.Reservas.Any(r =>
                r.Estado != "Cancelada" &&
                r.FechaIngreso < salida &&
                r.FechaSalida > ingreso))
            .OrderBy(h => h.Numero)
            .ToListAsync();
    }

    public async Task<bool> HabitacionOcupadaAsync(int idHabitacion, DateOnly ingreso, DateOnly salida)
    {
        return await _context.Reservas
            .AnyAsync(r => r.IdHabitacion == idHabitacion &&
                r.Estado != "Cancelada" &&
                r.FechaIngreso < salida &&
                r.FechaSalida > ingreso);
    }

    public async Task<List<Reserva>> ListarAsync(string? estado)
    {
        IQueryable<Reserva> consulta = _context.Reservas
            .AsNoTracking()
            .Include(r => r.IdHuespedNavigation)
            .Include(r => r.IdHabitacionNavigation)
                .ThenInclude(h => h.IdTipoHabitacionNavigation);

        if (!string.IsNullOrWhiteSpace(estado))
            consulta = consulta.Where(r => r.Estado == estado);

        return await consulta
            .OrderByDescending(r => r.FechaIngreso)
            .ThenByDescending(r => r.IdReserva)
            .ToListAsync();
    }

    public async Task<Reserva?> ObtenerAsync(int id)
    {
        return await _context.Reservas
            .AsNoTracking()
            .Include(r => r.IdHuespedNavigation)
            .Include(r => r.IdHabitacionNavigation)
                .ThenInclude(h => h.IdTipoHabitacionNavigation)
            .FirstOrDefaultAsync(r => r.IdReserva == id);
    }

    public async Task<List<Huesped>> ObtenerHuespedesParaListaAsync()
    {
        return await _context.Huespeds
            .AsNoTracking()
            .Where(h => h.Activo)
            .OrderBy(h => h.Apellidos)
            .ThenBy(h => h.Nombres)
            .Select(h => new Huesped
            {
                IdHuesped = h.IdHuesped,
                Dni = h.Dni,
                Nombres = h.Nombres,
                Apellidos = h.Apellidos
            })
            .ToListAsync();
    }

    public async Task<List<Habitacion>> ObtenerHabitacionesParaListaAsync()
    {
        return await _context.Habitacions
            .AsNoTracking()
            .Where(h => h.Activo)
            .OrderBy(h => h.Numero)
            .Select(h => new Habitacion
            {
                IdHabitacion = h.IdHabitacion,
                Numero = h.Numero,
                IdTipoHabitacionNavigation = new TipoHabitacion
                {
                    Nombre = h.IdTipoHabitacionNavigation.Nombre,
                    PrecioNoche = h.IdTipoHabitacionNavigation.PrecioNoche
                }
            })
            .ToListAsync();
    }

    public async Task<bool> ExisteHuespedActivoAsync(int idHuesped)
    {
        return await _context.Huespeds.AnyAsync(h => h.IdHuesped == idHuesped && h.Activo);
    }

    public async Task<bool> ExisteHabitacionActivaAsync(int idHabitacion)
    {
        return await _context.Habitacions.AnyAsync(h => h.IdHabitacion == idHabitacion && h.Activo);
    }

    public async Task<string?> ObtenerNumeroHabitacionAsync(int idHabitacion)
    {
        return await _context.Habitacions
            .AsNoTracking()
            .Where(h => h.IdHabitacion == idHabitacion)
            .Select(h => h.Numero)
            .FirstOrDefaultAsync();
    }

    public async Task CrearAsync(Reserva reserva)
    {
        reserva.Estado = "Pendiente";
        _context.Reservas.Add(reserva);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> CancelarAsync(int id)
    {
        var reserva = await _context.Reservas.FindAsync(id);
        if (reserva == null || (reserva.Estado != "Pendiente" && reserva.Estado != "Confirmada"))
            return false;

        reserva.Estado = "Cancelada";
        await _context.SaveChangesAsync();
        return true;
    }
}
